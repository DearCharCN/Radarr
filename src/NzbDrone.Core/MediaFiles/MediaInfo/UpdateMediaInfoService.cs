using System.IO;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles.BlurayDisc;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Movies;

namespace NzbDrone.Core.MediaFiles.MediaInfo
{
    public interface IUpdateMediaInfo
    {
        bool Update(MovieFile movieFile, Movie movie);
        bool UpdateMediaInfo(MovieFile movieFile, Movie movie);
    }

    public class UpdateMediaInfoService : IUpdateMediaInfo, IHandle<MovieScannedEvent>
    {
        private readonly IDiskProvider _diskProvider;
        private readonly IMediaFileService _mediaFileService;
        private readonly IVideoFileInfoReader _videoFileInfoReader;
        private readonly IBlurayDiscDetector _blurayDiscDetector;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public UpdateMediaInfoService(IDiskProvider diskProvider,
                                IMediaFileService mediaFileService,
                                IVideoFileInfoReader videoFileInfoReader,
                                IBlurayDiscDetector blurayDiscDetector,
                                IConfigService configService,
                                Logger logger)
        {
            _diskProvider = diskProvider;
            _mediaFileService = mediaFileService;
            _videoFileInfoReader = videoFileInfoReader;
            _blurayDiscDetector = blurayDiscDetector;
            _configService = configService;
            _logger = logger;
        }

        public void Handle(MovieScannedEvent message)
        {
            if (!_configService.EnableMediaInfo)
            {
                _logger.Debug("MediaInfo is disabled");
                return;
            }

            var allMediaFiles = _mediaFileService.GetFilesByMovie(message.Movie.Id);
            var filteredMediaFiles = allMediaFiles.Where(c =>
                c.MediaInfo == null ||
                c.MediaInfo.SchemaRevision < VideoFileInfoReader.MINIMUM_MEDIA_INFO_SCHEMA_REVISION).ToList();

            foreach (var mediaFile in filteredMediaFiles)
            {
                UpdateMediaInfo(mediaFile, message.Movie);
            }
        }

        public bool Update(MovieFile movieFile, Movie movie)
        {
            if (!_configService.EnableMediaInfo)
            {
                _logger.Debug("MediaInfo is disabled");
                return false;
            }

            return UpdateMediaInfo(movieFile, movie);
        }

        public bool UpdateMediaInfo(MovieFile movieFile, Movie movie)
        {
            var path = movieFile.Path.IsNotNullOrWhiteSpace() ? movieFile.Path : Path.Combine(movie.Path, movieFile.RelativePath);

            if (movieFile.IsDirectory)
            {
                if (!_diskProvider.FolderExists(path))
                {
                    _logger.Debug("Can't update MediaInfo because Blu-ray folder '{0}' does not exist", path);
                    return false;
                }

                var mainStreamFile = _blurayDiscDetector.GetMainPlaylistFile(path);

                if (mainStreamFile == null)
                {
                    _logger.Debug("Can't update MediaInfo because no main stream found in '{0}'", path);
                    return false;
                }

                var updatedMediaInfo = _videoFileInfoReader.GetMediaInfo(mainStreamFile);

                if (updatedMediaInfo == null)
                {
                    return false;
                }

                movieFile.MediaInfo = updatedMediaInfo;
                _mediaFileService.Update(movieFile);
                _logger.Debug("Updated MediaInfo for Blu-ray folder '{0}' via stream '{1}'", path, mainStreamFile);

                return true;
            }

            if (!_diskProvider.FileExists(path))
            {
                _logger.Debug("Can't update MediaInfo because '{0}' does not exist", path);
                return false;
            }

            var mediaInfo = _videoFileInfoReader.GetMediaInfo(path);

            if (mediaInfo == null)
            {
                return false;
            }

            movieFile.MediaInfo = mediaInfo;
            _mediaFileService.Update(movieFile);
            _logger.Debug("Updated MediaInfo for '{0}'", path);

            return true;
        }
    }
}
