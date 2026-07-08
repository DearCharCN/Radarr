using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Movies.Translations;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.Qualities;

namespace NzbDrone.Core.IndexerSearch
{
    public interface ISearchForReleases
    {
        Task<List<DownloadDecision>> MovieSearch(int movieId, bool userInvokedSearch, bool interactiveSearch);
        Task<List<DownloadDecision>> MovieSearch(Movie movie, bool userInvokedSearch, bool interactiveSearch);
    }

    public class ReleaseSearchService : ISearchForReleases
    {
        private const int MediaInfoWindowSize = 4;
        private static readonly TimeSpan MediaInfoWaitTimeout = TimeSpan.FromSeconds(120);
        private static readonly TimeSpan MediaInfoPollInterval = TimeSpan.FromSeconds(5);

        private readonly IIndexerFactory _indexerFactory;
        private readonly IHttpClient _httpClient;
        private readonly IMakeDownloadDecision _makeDownloadDecision;
        private readonly IMovieService _movieService;
        private readonly IMovieTranslationService _movieTranslationService;
        private readonly IQualityProfileService _qualityProfileService;
        private readonly Logger _logger;

        public ReleaseSearchService(IIndexerFactory indexerFactory,
                                IHttpClient httpClient,
                                IMakeDownloadDecision makeDownloadDecision,
                                IMovieService movieService,
                                IMovieTranslationService movieTranslationService,
                                IQualityProfileService qualityProfileService,
                                Logger logger)
        {
            _indexerFactory = indexerFactory;
            _httpClient = httpClient;
            _makeDownloadDecision = makeDownloadDecision;
            _movieService = movieService;
            _movieTranslationService = movieTranslationService;
            _qualityProfileService = qualityProfileService;
            _logger = logger;
        }

        public async Task<List<DownloadDecision>> MovieSearch(int movieId, bool userInvokedSearch, bool interactiveSearch)
        {
            var movie = _movieService.GetMovie(movieId);
            movie.MovieMetadata.Value.Translations = _movieTranslationService.GetAllTranslationsForMovieMetadata(movie.MovieMetadataId);

            return await MovieSearch(movie, userInvokedSearch, interactiveSearch);
        }

        public async Task<List<DownloadDecision>> MovieSearch(Movie movie, bool userInvokedSearch, bool interactiveSearch)
        {
            var downloadDecisions = new List<DownloadDecision>();

            var searchSpec = Get<MovieSearchCriteria>(movie, userInvokedSearch, interactiveSearch);

            var decisions = await Dispatch(indexer => indexer.Fetch(searchSpec), searchSpec);
            downloadDecisions.AddRange(decisions);

            return DeDupeDecisions(downloadDecisions);
        }

        private TSpec Get<TSpec>(Movie movie, bool userInvokedSearch, bool interactiveSearch)
            where TSpec : SearchCriteriaBase, new()
        {
            var spec = new TSpec
            {
                Movie = movie,
                UserInvokedSearch = userInvokedSearch,
                InteractiveSearch = interactiveSearch
            };

            var wantedLanguages = _qualityProfileService.GetAcceptableLanguages(movie.QualityProfileId);
            var translations = _movieTranslationService.GetAllTranslationsForMovieMetadata(movie.MovieMetadataId);

            var queryTranslations = new List<string>
            {
                movie.MovieMetadata.Value.Title,
                movie.MovieMetadata.Value.OriginalTitle
            };

            // Add Translation of wanted languages to search query
            foreach (var translation in translations.Where(a => wantedLanguages.Contains(a.Language)))
            {
                queryTranslations.Add(translation.Title);
            }

            spec.SceneTitles = queryTranslations.Where(t => t.IsNotNullOrWhiteSpace()).Distinct(StringComparer.InvariantCultureIgnoreCase).ToList();

            return spec;
        }

        private async Task<List<DownloadDecision>> Dispatch(Func<IIndexer, Task<IList<ReleaseInfo>>> searchAction, SearchCriteriaBase criteriaBase)
        {
            var indexers = criteriaBase.InteractiveSearch ?
                _indexerFactory.InteractiveSearchEnabled() :
                _indexerFactory.AutomaticSearchEnabled();

            // Filter indexers to untagged indexers and indexers with intersecting tags
            indexers = indexers.Where(i => i.Definition.Tags.Empty() || i.Definition.Tags.Intersect(criteriaBase.Movie.Tags).Any()).ToList();

            _logger.ProgressInfo("Searching indexers for {0}. {1} active indexers", criteriaBase, indexers.Count);

            async Task<List<ReleaseInfo>> FetchReports()
            {
                var tasks = indexers.Select(indexer => DispatchIndexer(searchAction, indexer, criteriaBase));
                var batch = await Task.WhenAll(tasks);

                return batch.SelectMany(x => x).ToList();
            }

            var reports = await FetchReports();

            if (!criteriaBase.InteractiveSearch)
            {
                reports = await WaitForMediaInfoCompletion(reports, FetchReports, criteriaBase);
            }

            _logger.ProgressDebug("Total of {0} reports were found for {1} from {2} indexers", reports.Count, criteriaBase, indexers.Count);

            // Update the last search time for movie if at least 1 indexer was searched.
            if (indexers.Any())
            {
                var lastSearchTime = DateTime.UtcNow;
                _logger.Debug("Setting last search time to: {0}", lastSearchTime);

                criteriaBase.Movie.LastSearchTime = lastSearchTime;
                _movieService.UpdateLastSearchTime(criteriaBase.Movie);
            }

            return _makeDownloadDecision.GetSearchDecision(reports, criteriaBase).ToList();
        }

        private async Task<IList<ReleaseInfo>> DispatchIndexer(Func<IIndexer, Task<IList<ReleaseInfo>>> searchAction, IIndexer indexer, SearchCriteriaBase criteriaBase)
        {
            try
            {
                return await searchAction(indexer);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error while searching for {0}", criteriaBase);
            }

            return Array.Empty<ReleaseInfo>();
        }

        private async Task<List<ReleaseInfo>> WaitForMediaInfoCompletion(List<ReleaseInfo> reports, Func<Task<List<ReleaseInfo>>> fetchReports, SearchCriteriaBase criteriaBase)
        {
            if (!HasPendingMediaInfo(reports))
            {
                return reports;
            }

            var started = DateTime.UtcNow;

            _logger.ProgressInfo("Waiting for additional media data before processing automatic search results for {0}", criteriaBase);

            while (HasPendingMediaInfo(reports) && DateTime.UtcNow - started < MediaInfoWaitTimeout)
            {
                var enrichedAny = EnrichPendingMediaInfo(reports);

                if (!enrichedAny)
                {
                    reports = await fetchReports();
                }

                if (HasPendingMediaInfo(reports))
                {
                    await Task.Delay(MediaInfoPollInterval);
                }
            }

            if (HasPendingMediaInfo(reports))
            {
                _logger.Warn("Timed out waiting for additional media data for {0}; continuing with the latest available search results", criteriaBase);
                CancelPendingMediaInfo(reports);
            }

            return reports;
        }

        private bool EnrichPendingMediaInfo(List<ReleaseInfo> reports)
        {
            var enrichedAny = false;
            var activeHandles = reports.Count(x => x.MediaInfoStatus == "pending" && x.MediaInfoHandleId.IsNotNullOrWhiteSpace());
            var skippedForWindow = 0;
            var pendingReports = reports.Count(x => x.MediaInfoStatus == "pending");
            _logger.Debug("Radarr automatic mediaInfo enrichment pass: pending {0}, active handles {1}/{2}", pendingReports, activeHandles, MediaInfoWindowSize);

            foreach (var report in reports.Where(x => x.MediaInfoStatus == "pending").ToList())
            {
                var hadActiveHandle = report.MediaInfoHandleId.IsNotNullOrWhiteSpace();

                if (!hadActiveHandle && activeHandles >= MediaInfoWindowSize)
                {
                    skippedForWindow++;
                    continue;
                }

                if (report.ProwlarrIndexerId <= 0)
                {
                    report.MediaInfoStatus = "unavailable";
                    report.MediaInfoProgressStatus = null;
                    continue;
                }

                var indexer = _indexerFactory.Get(report.IndexerId);
                var settings = indexer?.Settings as NewznabSettings;

                if (settings == null)
                {
                    report.MediaInfoStatus = "unavailable";
                    report.MediaInfoProgressStatus = null;
                    continue;
                }

                _logger.Debug("Radarr automatic mediaInfo Prowlarr request starting: release {0}, indexer {1}, prowlarr indexer {2}, existing handle {3}, active handles {4}/{5}, search {6}",
                    report.Guid,
                    report.IndexerId,
                    report.ProwlarrIndexerId,
                    report.MediaInfoHandleId,
                    activeHandles,
                    MediaInfoWindowSize,
                    report.MediaInfoSearchId);

                var request = new HttpRequestBuilder(BuildProwlarrMediaInfoUrl(settings))
                    .Post()
                    .Build();

                request.Headers.ContentType = "application/json";
                request.SetContent(new
                {
                    report.Guid,
                    IndexerId = report.ProwlarrIndexerId,
                    report.MediaInfoHandleId,
                    report.MediaInfoSearchId
                }.ToJson());
                request.ContentSummary = $"{{ \"guid\": \"{report.Guid}\", \"indexerId\": {report.ProwlarrIndexerId}, \"mediaInfoHandleId\": \"{report.MediaInfoHandleId}\", \"mediaInfoSearchId\": \"{report.MediaInfoSearchId}\" }}";
                request.SuppressHttpError = true;

                if (settings.ApiKey.IsNotNullOrWhiteSpace())
                {
                    request.Headers.Set("X-Api-Key", settings.ApiKey);
                }

                var response = _httpClient.Post<ReleaseMediaInfoResult>(request);

                if (response.HasHttpError || response.Resource == null)
                {
                    _logger.Debug("Prowlarr mediaInfo request failed for automatic-search release '{0}' from indexer {1}", report.Guid, report.IndexerId);
                    report.MediaInfoStatus = "failed";
                    report.MediaInfoProgressStatus = null;
                    continue;
                }

                report.Subs = response.Resource.Subs ?? report.Subs;
                report.AudioInfo = response.Resource.AudioInfo ?? report.AudioInfo;
                report.MediaInfoStatus = response.Resource.MediaInfoStatus;
                report.MediaInfoHandleId = response.Resource.MediaInfoStatus == "pending" ? response.Resource.MediaInfoHandleId : null;
                report.MediaInfoSearchId = response.Resource.MediaInfoSearchId;
                report.MediaInfoProgressStatus = response.Resource.MediaInfoProgressStatus;
                report.MediaInfoProgressCompleted = response.Resource.MediaInfoProgressCompleted;
                report.MediaInfoProgressTotal = response.Resource.MediaInfoProgressTotal;
                enrichedAny = true;
                _logger.Debug("Radarr automatic mediaInfo Prowlarr request completed: release {0}, status {1}, handle {2}, progress {3}/{4} {5}",
                    report.Guid,
                    report.MediaInfoStatus,
                    report.MediaInfoHandleId,
                    report.MediaInfoProgressCompleted,
                    report.MediaInfoProgressTotal,
                    report.MediaInfoProgressStatus);

                if (report.MediaInfoStatus == "pending" && report.MediaInfoHandleId.IsNotNullOrWhiteSpace() && !hadActiveHandle)
                {
                    activeHandles++;
                }
                else if (report.MediaInfoStatus != "pending" && hadActiveHandle)
                {
                    activeHandles = Math.Max(0, activeHandles - 1);
                }
            }

            if (skippedForWindow > 0)
            {
                _logger.Debug("Radarr automatic mediaInfo enrichment skipped {0} pending releases because the active handle window is full", skippedForWindow);
            }

            return enrichedAny;
        }

        private void CancelPendingMediaInfo(List<ReleaseInfo> reports)
        {
            foreach (var report in reports.Where(x => x.MediaInfoStatus == "pending" && x.MediaInfoHandleId.IsNotNullOrWhiteSpace()))
            {
                _logger.Debug("Radarr automatic mediaInfo cancel requested: release {0}, indexer {1}, handle {2}",
                    report.Guid,
                    report.IndexerId,
                    report.MediaInfoHandleId);

                var indexer = _indexerFactory.Get(report.IndexerId);
                var settings = indexer?.Settings as NewznabSettings;

                if (settings == null)
                {
                    continue;
                }

                var request = new HttpRequestBuilder(BuildProwlarrMediaInfoCancelUrl(settings))
                    .Post()
                    .Build();

                request.Headers.ContentType = "application/json";
                request.SetContent(new
                {
                    report.MediaInfoHandleId
                }.ToJson());
                request.ContentSummary = $"{{ \"mediaInfoHandleId\": \"{report.MediaInfoHandleId}\" }}";
                request.SuppressHttpError = true;

                if (settings.ApiKey.IsNotNullOrWhiteSpace())
                {
                    request.Headers.Set("X-Api-Key", settings.ApiKey);
                }

                _httpClient.Post(request);
                report.MediaInfoHandleId = null;
            }
        }

        private static bool HasPendingMediaInfo(List<ReleaseInfo> reports)
        {
            return reports.Any(report => report.MediaInfoStatus == "pending" || report.MediaInfoProgressStatus == "pending");
        }

        private static string BuildProwlarrMediaInfoUrl(NewznabSettings settings)
        {
            return BuildProwlarrMediaInfoUrl(settings, false);
        }

        private static string BuildProwlarrMediaInfoCancelUrl(NewznabSettings settings)
        {
            return BuildProwlarrMediaInfoUrl(settings, true);
        }

        private static string BuildProwlarrMediaInfoUrl(NewznabSettings settings, bool cancel)
        {
            var baseUrl = settings.BaseUrl.TrimEnd('/');
            var apiPath = settings.ApiPath.IsNullOrWhiteSpace() ? "/api" : settings.ApiPath;
            var combinedUrl = $"{baseUrl}/{apiPath.TrimStart('/')}";
            var uriBuilder = new UriBuilder(combinedUrl);
            var segments = uriBuilder.Path
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            if (segments.Count > 0 && segments.Last().Equals("api", StringComparison.OrdinalIgnoreCase))
            {
                segments.RemoveAt(segments.Count - 1);
            }

            if (segments.Count > 0 && int.TryParse(segments.Last(), out _))
            {
                segments.RemoveAt(segments.Count - 1);
            }

            segments.Add("api");
            segments.Add("v1");
            segments.Add("search");
            segments.Add("mediaInfo");

            if (cancel)
            {
                segments.Add("cancel");
            }

            uriBuilder.Path = string.Join("/", segments);
            uriBuilder.Query = string.Empty;

            return uriBuilder.Uri.AbsoluteUri;
        }

        private class ReleaseMediaInfoResult
        {
            public List<string> Subs { get; set; }
            public List<ReleaseAudioInfo> AudioInfo { get; set; }
            public string MediaInfoStatus { get; set; }
            public string MediaInfoHandleId { get; set; }
            public string MediaInfoSearchId { get; set; }
            public string MediaInfoProgressStatus { get; set; }
            public int MediaInfoProgressCompleted { get; set; }
            public int MediaInfoProgressTotal { get; set; }
        }

        private List<DownloadDecision> DeDupeDecisions(List<DownloadDecision> decisions)
        {
            // De-dupe reports by guid so duplicate results aren't returned. Pick the one with the least rejections and higher indexer priority.
            return decisions.GroupBy(d => d.RemoteMovie.Release.Guid)
                .Select(d => d.OrderBy(v => v.Rejections.Count()).ThenBy(v => v.RemoteMovie?.Release?.IndexerPriority ?? IndexerDefinition.DefaultPriority).First())
                .ToList();
        }
    }
}
