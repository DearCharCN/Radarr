using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Languages;
using NzbDrone.Core.MediaFiles.BlurayDisc;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.MediaFiles.MovieImport.Aggregation;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Qualities;

namespace NzbDrone.Core.MediaFiles.MovieImport.Manual
{
    public interface IManualImportService
    {
        List<ManualImportItem> GetMediaFiles(int movieId);
        List<ManualImportItem> GetMediaFiles(string path, string downloadId, int? movieId, bool filterExistingFiles);
        ManualImportItem ReprocessItem(string path, string downloadId, int movieId, string releaseGroup, QualityModel quality, List<Language> languages, int indexerFlags, bool isDirectory);
    }

    public class ManualImportService : IExecute<ManualImportCommand>, IManualImportService
    {
        private readonly IDiskProvider _diskProvider;
        private readonly IParsingService _parsingService;
        private readonly IDiskScanService _diskScanService;
        private readonly IMakeImportDecision _importDecisionMaker;
        private readonly IMovieService _movieService;
        private readonly IImportApprovedMovie _importApprovedMovie;
        private readonly IAggregationService _aggregationService;
        private readonly ITrackedDownloadService _trackedDownloadService;
        private readonly IDownloadedMovieImportService _downloadedMovieImportService;
        private readonly IMediaFileService _mediaFileService;
        private readonly ICustomFormatCalculationService _formatCalculator;
        private readonly IConfigService _configService;
        private readonly IBlurayDiscDetector _blurayDiscDetector;
        private readonly IVideoFileInfoReader _videoFileInfoReader;
        private readonly IEventAggregator _eventAggregator;
        private readonly Logger _logger;

        public ManualImportService(IDiskProvider diskProvider,
                                   IParsingService parsingService,
                                   IDiskScanService diskScanService,
                                   IMakeImportDecision importDecisionMaker,
                                   IMovieService movieService,
                                   IAggregationService aggregationService,
                                   IImportApprovedMovie importApprovedMovie,
                                   ITrackedDownloadService trackedDownloadService,
                                   IDownloadedMovieImportService downloadedMovieImportService,
                                   IMediaFileService mediaFileService,
                                   ICustomFormatCalculationService formatCalculator,
                                   IConfigService configService,
                                   IBlurayDiscDetector blurayDiscDetector,
                                   IVideoFileInfoReader videoFileInfoReader,
                                   IEventAggregator eventAggregator,
                                   Logger logger)
        {
            _diskProvider = diskProvider;
            _parsingService = parsingService;
            _diskScanService = diskScanService;
            _importDecisionMaker = importDecisionMaker;
            _movieService = movieService;
            _aggregationService = aggregationService;
            _importApprovedMovie = importApprovedMovie;
            _trackedDownloadService = trackedDownloadService;
            _downloadedMovieImportService = downloadedMovieImportService;
            _mediaFileService = mediaFileService;
            _formatCalculator = formatCalculator;
            _configService = configService;
            _blurayDiscDetector = blurayDiscDetector;
            _videoFileInfoReader = videoFileInfoReader;
            _eventAggregator = eventAggregator;
            _logger = logger;
        }

        public List<ManualImportItem> GetMediaFiles(int movieId)
        {
            var movie = _movieService.GetMovie(movieId);
            var directoryInfo = new DirectoryInfo(movie.Path);
            var movieFiles = _mediaFileService.GetFilesByMovie(movieId);

            var items = movieFiles.Select(movieFile => MapItem(movieFile, movie, directoryInfo.Name)).ToList();

            var mediaFiles = _diskScanService.FilterPaths(movie.Path, _diskScanService.GetVideoFiles(movie.Path)).ToList();
            var unmappedFiles = MediaFileService.FilterExistingFiles(mediaFiles, movieFiles, movie);

            items.AddRange(unmappedFiles.Select(file =>
                new ManualImportItem
                {
                    Path = Path.Combine(movie.Path, file),
                    FolderName = directoryInfo.Name,
                    RelativePath = movie.Path.GetRelativePath(file),
                    Name = Path.GetFileNameWithoutExtension(file),
                    Movie = movie,
                    ReleaseGroup = string.Empty,
                    Quality = new QualityModel(Quality.Unknown),
                    Languages = new List<Language> { Language.Unknown },
                    Size = _diskProvider.GetFileSize(file),
                    Rejections = Enumerable.Empty<ImportRejection>()
                }));

            return items;
        }

        public List<ManualImportItem> GetMediaFiles(string path, string downloadId, int? movieId, bool filterExistingFiles)
        {
            if (downloadId.IsNotNullOrWhiteSpace())
            {
                var trackedDownload = _trackedDownloadService.Find(downloadId);

                if (trackedDownload == null)
                {
                    return new List<ManualImportItem>();
                }

                path = trackedDownload.ImportItem.OutputPath.FullPath;
            }

            if (!_diskProvider.FolderExists(path))
            {
                if (!_diskProvider.FileExists(path))
                {
                    return new List<ManualImportItem>();
                }

                var rootFolder = Path.GetDirectoryName(path);
                return new List<ManualImportItem> { ProcessFile(rootFolder, rootFolder, path, downloadId) };
            }

            return ProcessFolder(path, path, downloadId, movieId, filterExistingFiles);
        }

        public ManualImportItem ReprocessItem(string path, string downloadId, int movieId, string releaseGroup, QualityModel quality, List<Language> languages, int indexerFlags, bool isDirectory)
        {
            var rootFolder = Path.GetDirectoryName(path);
            var movie = _movieService.GetMovie(movieId);

            var languageParse = LanguageParser.ParseLanguages(path);

            if (languageParse.Count <= 1 && languageParse.First() == Language.Unknown && movie != null)
            {
                languageParse = new List<Language> { movie.MovieMetadata.Value.OriginalLanguage };
                _logger.Debug("Language couldn't be parsed from release, fallback to movie original language: {0}", movie.MovieMetadata.Value.OriginalLanguage.Name);
            }

            var downloadClientItem = GetTrackedDownload(downloadId)?.DownloadItem;
            var finalReleaseGroup = releaseGroup.IsNullOrWhiteSpace()
                ? ReleaseGroupParser.ParseReleaseGroup(path)
                : releaseGroup;
            var finalQuality = (quality?.Quality ?? Quality.Unknown) == Quality.Unknown ? QualityParser.ParseQuality(path) : quality;
            var finalLanguages =
                languages?.Count <= 1 && (languages?.SingleOrDefault() ?? Language.Unknown) == Language.Unknown
                    ? languageParse
                    : languages;

            var localMovie = isDirectory
                ? CreateBlurayLocalMovie(path, movie, downloadClientItem, finalReleaseGroup, finalQuality, finalLanguages, indexerFlags)
                : new LocalMovie
                {
                    Movie = movie,
                    FileMovieInfo = Parser.Parser.ParseMoviePath(path),
                    DownloadClientMovieInfo = downloadClientItem == null ? null : Parser.Parser.ParseMovieTitle(downloadClientItem.Title),
                    DownloadItem = downloadClientItem,
                    Path = path,
                    SceneSource = SceneSource(movie, rootFolder),
                    ExistingFile = movie.Path.IsParentPath(path),
                    Size = _diskProvider.GetFileSize(path),
                    ReleaseGroup = finalReleaseGroup,
                    Languages = finalLanguages,
                    Quality = finalQuality,
                    IndexerFlags = (IndexerFlags)indexerFlags
                };

            localMovie.CustomFormats = _formatCalculator.ParseCustomFormat(localMovie);
            localMovie.CustomFormatScore = localMovie.Movie?.QualityProfile?.CalculateCustomFormatScore(localMovie.CustomFormats) ?? 0;

            localMovie = _aggregationService.Augment(localMovie, downloadClientItem);

            localMovie.Movie = movie;
            localMovie.ReleaseGroup = finalReleaseGroup;
            localMovie.Quality = finalQuality;
            localMovie.Languages = finalLanguages;
            localMovie.IndexerFlags = (IndexerFlags)indexerFlags;

            return MapItem(GetManualImportDecision(localMovie, downloadClientItem), rootFolder, downloadId, null);
        }

        private List<ManualImportItem> ProcessFolder(string rootFolder, string baseFolder, string downloadId, int? movieId, bool filterExistingFiles)
        {
            DownloadClientItem downloadClientItem = null;
            Movie movie = null;

            var directoryInfo = new DirectoryInfo(baseFolder);

            if (movieId.HasValue)
            {
                movie = _movieService.GetMovie(movieId.Value);
            }
            else
            {
                try
                {
                    movie = _parsingService.GetMovie(directoryInfo.Name);
                }
                catch (MultipleMoviesFoundException e)
                {
                    _logger.Warn(e, "Unable to match movie by title");
                }
            }

            if (downloadId.IsNotNullOrWhiteSpace())
            {
                var trackedDownload = _trackedDownloadService.Find(downloadId);
                downloadClientItem = trackedDownload.DownloadItem;

                if (movie == null)
                {
                    movie = trackedDownload.RemoteMovie?.Movie;
                }
            }

            if (movie == null && IsManualImportBlurayFolder(baseFolder))
            {
                var localMovie = CreateBlurayLocalMovie(baseFolder, null, downloadClientItem, string.Empty, new QualityModel(Quality.Unknown), new List<Language> { Language.Unknown }, 0);

                return new List<ManualImportItem>
                {
                    MapItem(new ImportDecision(localMovie, new ImportRejection(ImportRejectionReason.UnknownMovie, "Unknown Movie")), rootFolder, downloadId, directoryInfo.Name)
                };
            }

            if (movie == null)
            {
                // Filter paths based on the rootFolder, so files in subfolders that should be ignored are ignored.
                // It will lead to some extra directories being checked for files, but it saves the processing of them and is cleaner than
                // teaching FilterPaths to know whether it's processing a file or a folder and changing it's filtering based on that.
                // If the movie is unknown for the directory and there are more than 100 files in the folder don't process the items before returning.
                var files = _diskScanService.FilterPaths(rootFolder, _diskScanService.GetVideoFiles(baseFolder, false));

                if (files.Count > 100)
                {
                    _logger.Warn("Unable to determine movie from folder name and found more than 100 files. Skipping parsing");

                    return ProcessDownloadDirectory(rootFolder, files);
                }

                var subfolders = _diskScanService.FilterPaths(rootFolder, _diskProvider.GetDirectories(baseFolder));

                var processedFiles = files.Select(file => ProcessFile(rootFolder, baseFolder, file, downloadId));
                var processedFolders = subfolders.SelectMany(subfolder => ProcessFolder(rootFolder, subfolder, downloadId, null, filterExistingFiles));

                return processedFiles.Concat(processedFolders).Where(i => i != null).ToList();
            }

            if (IsManualImportBlurayFolder(baseFolder))
            {
                return ProcessBlurayFolder(rootFolder, baseFolder, downloadId, movie, downloadClientItem, directoryInfo.Name);
            }

            var folderInfo = Parser.Parser.ParseMovieTitle(directoryInfo.Name);
            var movieFiles = _diskScanService.FilterPaths(rootFolder, _diskScanService.GetVideoFiles(baseFolder).ToList());
            var decisions = _importDecisionMaker.GetImportDecisions(movieFiles, movie, downloadClientItem, folderInfo, SceneSource(movie, baseFolder), filterExistingFiles);

            return decisions.Select(decision => MapItem(decision, rootFolder, downloadId, directoryInfo.Name)).ToList();
        }

        private List<ManualImportItem> ProcessBlurayFolder(string rootFolder, string baseFolder, string downloadId, Movie movie, DownloadClientItem downloadClientItem, string folderName)
        {
            var mainStreamFile = _blurayDiscDetector.GetMainPlaylistFile(baseFolder);
            var folderInfo = Parser.Parser.ParseMovieTitle(folderName);
            var totalSize = GetFolderSize(baseFolder);

            if (mainStreamFile == null)
            {
                var localMovie = CreateBlurayLocalMovie(baseFolder, movie, downloadClientItem, string.Empty, new QualityModel(Quality.Unknown), new List<Language> { Language.Unknown }, 0);

                return new List<ManualImportItem>
                {
                    MapItem(new ImportDecision(localMovie, new ImportRejection(ImportRejectionReason.Error, "Unable to find main stream file in Blu-ray disc folder")), rootFolder, downloadId, folderName)
                };
            }

            var decisions = _importDecisionMaker.GetImportDecisionsBluray(baseFolder, mainStreamFile, totalSize, movie, downloadClientItem, folderInfo);

            return decisions.Select(decision => MapItem(decision, rootFolder, downloadId, folderName)).ToList();
        }

        private bool IsManualImportBlurayFolder(string path)
        {
            return _configService.ImportBlurayFolders && _blurayDiscDetector.IsBlurayDiscFolder(path);
        }

        private long GetFolderSize(string path)
        {
            return _diskProvider.GetFiles(path, true).Sum(f => _diskProvider.GetFileSize(f));
        }

        private LocalMovie CreateBlurayLocalMovie(string path, Movie movie, DownloadClientItem downloadClientItem, string releaseGroup, QualityModel quality, List<Language> languages, int indexerFlags)
        {
            var mainStreamFile = _blurayDiscDetector.GetMainPlaylistFile(path);
            var folderName = new DirectoryInfo(path).Name;
            var localMovie = new LocalMovie
            {
                Movie = movie,
                DownloadClientMovieInfo = downloadClientItem == null ? null : Parser.Parser.ParseMovieTitle(downloadClientItem.Title),
                DownloadItem = downloadClientItem,
                FolderMovieInfo = Parser.Parser.ParseMovieTitle(folderName),
                FileMovieInfo = Parser.Parser.ParseMovieTitle(folderName),
                Path = path,
                Size = GetFolderSize(path),
                IsDirectory = true,
                BlurayMainStreamFile = mainStreamFile,
                ExistingFile = movie != null && movie.Path.IsParentPath(path),
                SceneSource = movie == null || !movie.Path.IsParentPath(path),
                OtherVideoFiles = false,
                ReleaseGroup = releaseGroup,
                Quality = quality,
                Languages = languages,
                IndexerFlags = (IndexerFlags)indexerFlags
            };

            if (mainStreamFile != null)
            {
                localMovie.MediaInfo = _videoFileInfoReader.GetMediaInfo(mainStreamFile);
            }

            return localMovie;
        }

        private ImportDecision GetManualImportDecision(LocalMovie localMovie, DownloadClientItem downloadClientItem)
        {
            if (localMovie.IsDirectory && localMovie.BlurayMainStreamFile == null)
            {
                return new ImportDecision(localMovie, new ImportRejection(ImportRejectionReason.Error, "Unable to find main stream file in Blu-ray disc folder"));
            }

            return _importDecisionMaker.GetDecision(localMovie, downloadClientItem);
        }

        private ManualImportItem ProcessFile(string rootFolder, string baseFolder, string file, string downloadId, Movie movie = null)
        {
            try
            {
                var trackedDownload = GetTrackedDownload(downloadId);
                var relativeFile = baseFolder.GetRelativePath(file);

                if (movie == null)
                {
                    movie = _parsingService.GetMovie(relativeFile.Split('\\', '/')[0]);
                }

                if (movie == null)
                {
                    movie = _parsingService.GetMovie(relativeFile);
                }

                if (trackedDownload != null && movie == null)
                {
                    movie = trackedDownload?.RemoteMovie?.Movie;
                }

                if (movie == null)
                {
                    var relativeParseInfo = Parser.Parser.ParseMoviePath(relativeFile);

                    if (relativeParseInfo != null)
                    {
                        movie = _movieService.FindByTitle(relativeParseInfo.PrimaryMovieTitle, relativeParseInfo.Year);
                    }
                }

                if (movie == null)
                {
                    var localMovie = new LocalMovie();
                    localMovie.Path = file;
                    localMovie.ReleaseGroup = ReleaseGroupParser.ParseReleaseGroup(file);
                    localMovie.Quality = QualityParser.ParseQuality(file);
                    localMovie.Languages = LanguageParser.ParseLanguages(file);
                    localMovie.Size = _diskProvider.GetFileSize(file);

                    return MapItem(new ImportDecision(localMovie,
                        new ImportRejection(ImportRejectionReason.UnknownMovie, "Unknown Movie")),
                        rootFolder,
                        downloadId,
                        null);
                }

                var importDecisions = _importDecisionMaker.GetImportDecisions(new List<string> { file }, movie, trackedDownload?.DownloadItem, null, SceneSource(movie, baseFolder));

                if (importDecisions.Any())
                {
                    return MapItem(importDecisions.First(), rootFolder, downloadId, null);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Failed to process file: {0}", file);
            }

            return new ManualImportItem
            {
                DownloadId = downloadId,
                Path = file,
                RelativePath = rootFolder.GetRelativePath(file),
                Name = Path.GetFileNameWithoutExtension(file),
                Size = _diskProvider.GetFileSize(file),
                Rejections = new List<ImportRejection>()
            };
        }

        private List<ManualImportItem> ProcessDownloadDirectory(string rootFolder, List<string> videoFiles)
        {
            var items = new List<ManualImportItem>();

            foreach (var file in videoFiles)
            {
                var localMovie = new LocalMovie();
                localMovie.Path = file;
                localMovie.Quality = new QualityModel(Quality.Unknown);
                localMovie.Languages = new List<Language> { Language.Unknown };
                localMovie.ReleaseGroup = ReleaseGroupParser.ParseReleaseGroup(file);
                localMovie.Size = _diskProvider.GetFileSize(file);

                items.Add(MapItem(new ImportDecision(localMovie), rootFolder, null, null));
            }

            return items;
        }

        private bool SceneSource(Movie movie, string folder)
        {
            return !(movie.Path.PathEquals(folder) || movie.Path.IsParentPath(folder));
        }

        private TrackedDownload GetTrackedDownload(string downloadId)
        {
            if (downloadId.IsNotNullOrWhiteSpace())
            {
                var trackedDownload = _trackedDownloadService.Find(downloadId);

                return trackedDownload;
            }

            return null;
        }

        private ManualImportItem MapItem(ImportDecision decision, string rootFolder, string downloadId, string folderName)
        {
            var item = new ManualImportItem();

            item.Path = decision.LocalMovie.Path;
            item.FolderName = folderName;
            item.RelativePath = rootFolder.PathEquals(decision.LocalMovie.Path) ? new DirectoryInfo(decision.LocalMovie.Path).Name : rootFolder.GetRelativePath(decision.LocalMovie.Path);
            item.Name = decision.LocalMovie.IsDirectory ? new DirectoryInfo(decision.LocalMovie.Path).Name : Path.GetFileNameWithoutExtension(decision.LocalMovie.Path);
            item.DownloadId = downloadId;
            item.IsDirectory = decision.LocalMovie.IsDirectory;

            item.Quality = decision.LocalMovie.Quality;
            item.Size = decision.LocalMovie.IsDirectory ? decision.LocalMovie.Size : _diskProvider.GetFileSize(decision.LocalMovie.Path);
            item.Languages = decision.LocalMovie.Languages;
            item.ReleaseGroup = decision.LocalMovie.ReleaseGroup;
            item.Rejections = decision.Rejections;
            item.IndexerFlags = (int)decision.LocalMovie.IndexerFlags;

            if (decision.LocalMovie.Movie != null)
            {
                item.Movie = decision.LocalMovie.Movie;

                item.CustomFormats = _formatCalculator.ParseCustomFormat(decision.LocalMovie);
                item.CustomFormatScore = item.Movie.QualityProfile?.CalculateCustomFormatScore(item.CustomFormats) ?? 0;
            }

            return item;
        }

        private ManualImportItem MapItem(MovieFile movieFile, Movie movie, string folderName)
        {
            var item = new ManualImportItem();

            item.Path = Path.Combine(movie.Path, movieFile.RelativePath);
            item.FolderName = folderName;
            item.RelativePath = movieFile.RelativePath;
            item.Name = movieFile.IsDirectory ? new DirectoryInfo(item.Path).Name : Path.GetFileNameWithoutExtension(movieFile.Path);
            item.IsDirectory = movieFile.IsDirectory;
            item.Movie = movie;
            item.ReleaseGroup = movieFile.ReleaseGroup;
            item.Quality = movieFile.Quality;
            item.Languages = movieFile.Languages;
            item.IndexerFlags = (int)movieFile.IndexerFlags;
            item.Size = movieFile.IsDirectory ? movieFile.Size : _diskProvider.GetFileSize(item.Path);
            item.Rejections = Enumerable.Empty<ImportRejection>();
            item.MovieFileId = movieFile.Id;
            item.CustomFormats = _formatCalculator.ParseCustomFormat(movieFile, movie);

            return item;
        }

        public void Execute(ManualImportCommand message)
        {
            _logger.ProgressTrace("Manually importing {0} files using mode {1}", message.Files.Count, message.ImportMode);

            var imported = new List<ImportResult>();
            var importedTrackedDownload = new List<ManuallyImportedFile>();

            for (var i = 0; i < message.Files.Count; i++)
            {
                _logger.ProgressTrace("Processing file {0} of {1}", i + 1, message.Files.Count);

                var file = message.Files[i];
                var movie = _movieService.GetMovie(file.MovieId);
                var existingFile = movie.Path.IsParentPath(file.Path);
                TrackedDownload trackedDownload = null;

                if (file.DownloadId.IsNotNullOrWhiteSpace())
                {
                    trackedDownload = _trackedDownloadService.Find(file.DownloadId);
                }

                var localMovie = file.IsDirectory
                    ? CreateBlurayLocalMovie(file.Path, movie, trackedDownload?.DownloadItem, file.ReleaseGroup, file.Quality, file.Languages, file.IndexerFlags)
                    : new LocalMovie
                    {
                        ExistingFile = existingFile,
                        FileMovieInfo = Parser.Parser.ParseMoviePath(file.Path) ?? new ParsedMovieInfo(),
                        DownloadClientMovieInfo = trackedDownload?.RemoteMovie?.ParsedMovieInfo,
                        DownloadItem = trackedDownload?.DownloadItem,
                        Path = file.Path,
                        ReleaseGroup = file.ReleaseGroup,
                        Quality = file.Quality,
                        Languages = file.Languages,
                        IndexerFlags = (IndexerFlags)file.IndexerFlags,
                        Movie = movie,
                        Size = 0
                    };

                if (file.FolderName.IsNotNullOrWhiteSpace())
                {
                    localMovie.FolderMovieInfo = Parser.Parser.ParseMovieTitle(file.FolderName);
                    localMovie.SceneSource = !existingFile;
                }

                localMovie = _aggregationService.Augment(localMovie, trackedDownload?.DownloadItem);

                localMovie.Movie = movie;
                localMovie.ReleaseGroup = file.ReleaseGroup;
                localMovie.Quality = file.Quality;
                localMovie.Languages = file.Languages;
                localMovie.IndexerFlags = (IndexerFlags)file.IndexerFlags;

                localMovie.CustomFormats = _formatCalculator.ParseCustomFormat(localMovie);
                localMovie.CustomFormatScore = localMovie.Movie.QualityProfile?.CalculateCustomFormatScore(localMovie.CustomFormats) ?? 0;

                var importDecision = GetManualImportDecision(localMovie, trackedDownload?.DownloadItem);

                if (trackedDownload == null)
                {
                    imported.AddRange(_importApprovedMovie.Import(new List<ImportDecision> { importDecision }, !existingFile, null, message.ImportMode));
                }
                else
                {
                    var importResult = _importApprovedMovie.Import(new List<ImportDecision> { importDecision }, true, trackedDownload.DownloadItem, message.ImportMode).First();

                    imported.Add(importResult);

                    importedTrackedDownload.Add(new ManuallyImportedFile
                    {
                        TrackedDownload = trackedDownload,
                        ImportResult = importResult
                    });
                }
            }

            _logger.ProgressTrace("Manually imported {0} files", imported.Count);

            foreach (var groupedTrackedDownload in importedTrackedDownload.GroupBy(i => i.TrackedDownload.DownloadItem.DownloadId).ToList())
            {
                var trackedDownload = groupedTrackedDownload.First().TrackedDownload;

                var importMovie = groupedTrackedDownload.First().ImportResult.ImportDecision.LocalMovie.Movie;
                var outputPath = trackedDownload.ImportItem.OutputPath.FullPath;

                if (_diskProvider.FolderExists(outputPath))
                {
                    if (_downloadedMovieImportService.ShouldDeleteFolder(
                            new DirectoryInfo(outputPath),
                            importMovie) && trackedDownload.DownloadItem.CanMoveFiles)
                    {
                        _diskProvider.DeleteFolder(outputPath, true);
                    }
                }

                if (groupedTrackedDownload.Select(c => c.ImportResult).Any(c => c.Result == ImportResultType.Imported))
                {
                    trackedDownload.State = TrackedDownloadState.Imported;
                    _eventAggregator.PublishEvent(new DownloadCompletedEvent(trackedDownload, importMovie.Id));
                }
            }
        }
    }
}
