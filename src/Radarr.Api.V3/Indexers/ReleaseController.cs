using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.EnsureThat;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.Download;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Indexers.Newznab;
using NzbDrone.Core.IndexerSearch;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Profiles.AudioLanguageMappings;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Validation;
using Radarr.Http;
using Radarr.Http.Extensions;
using HttpStatusCode = System.Net.HttpStatusCode;

namespace Radarr.Api.V3.Indexers
{
    [V3ApiController]
    public class ReleaseController : ReleaseControllerBase
    {
        private readonly IFetchAndParseRss _rssFetcherAndParser;
        private readonly ISearchForReleases _releaseSearchService;
        private readonly IMakeDownloadDecision _downloadDecisionMaker;
        private readonly IPrioritizeDownloadDecision _prioritizeDownloadDecision;
        private readonly IDownloadService _downloadService;
        private readonly IMovieService _movieService;
        private readonly IIndexerFactory _indexerFactory;
        private readonly IAudioLanguageMappingService _audioLanguageMappingService;
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        private readonly ICached<RemoteMovie> _remoteMovieCache;

        public ReleaseController(IFetchAndParseRss rssFetcherAndParser,
                             ISearchForReleases releaseSearchService,
                             IMakeDownloadDecision downloadDecisionMaker,
                             IPrioritizeDownloadDecision prioritizeDownloadDecision,
                             IDownloadService downloadService,
                             IMovieService movieService,
                             IIndexerFactory indexerFactory,
                             IAudioLanguageMappingService audioLanguageMappingService,
                             IHttpClient httpClient,
                             ICacheManager cacheManager,
                             IQualityProfileService qualityProfileService,
                             Logger logger)
            : base(qualityProfileService)
        {
            _rssFetcherAndParser = rssFetcherAndParser;
            _releaseSearchService = releaseSearchService;
            _downloadDecisionMaker = downloadDecisionMaker;
            _prioritizeDownloadDecision = prioritizeDownloadDecision;
            _downloadService = downloadService;
            _movieService = movieService;
            _indexerFactory = indexerFactory;
            _audioLanguageMappingService = audioLanguageMappingService;
            _httpClient = httpClient;
            _logger = logger;

            PostValidator.RuleFor(s => s.IndexerId).ValidId();
            PostValidator.RuleFor(s => s.Guid).NotEmpty();

            _remoteMovieCache = cacheManager.GetCache<RemoteMovie>(GetType(), "remoteMovies");
        }

        [HttpPost]
        [Consumes("application/json")]
        public async Task<object> DownloadRelease([FromBody] ReleaseResource release)
        {
            var remoteMovie = _remoteMovieCache.Find(GetCacheKey(release));

            if (remoteMovie == null)
            {
                _logger.Debug("Couldn't find requested release in cache, cache timeout probably expired.");

                throw new NzbDroneClientException(HttpStatusCode.NotFound, "Couldn't find requested release in cache, try searching again");
            }

            try
            {
                if (release.ShouldOverride == true)
                {
                    Ensure.That(release.MovieId, () => release.MovieId).IsNotNull();
                    Ensure.That(release.Quality, () => release.Quality).IsNotNull();
                    Ensure.That(release.Languages, () => release.Languages).IsNotNull();

                    // Clone the remote episode so we don't overwrite anything on the original
                    remoteMovie = new RemoteMovie
                    {
                        Release = remoteMovie.Release,
                        ParsedMovieInfo = remoteMovie.ParsedMovieInfo.JsonClone(),
                        MovieRequested = remoteMovie.MovieRequested,
                        DownloadAllowed = remoteMovie.DownloadAllowed,
                        SeedConfiguration = remoteMovie.SeedConfiguration,
                        CustomFormats = remoteMovie.CustomFormats,
                        CustomFormatScore = remoteMovie.CustomFormatScore,
                        MovieMatchType = remoteMovie.MovieMatchType,
                        ReleaseSource = remoteMovie.ReleaseSource
                    };

                    remoteMovie.Movie = _movieService.GetMovie(release.MovieId!.Value);
                    remoteMovie.ParsedMovieInfo.Quality = release.Quality;
                    remoteMovie.Languages = release.Languages;
                }

                if (remoteMovie.Movie == null)
                {
                    if (release.MovieId.HasValue)
                    {
                        var movie = _movieService.GetMovie(release.MovieId.Value);

                        remoteMovie.Movie = movie;
                    }
                    else
                    {
                        throw new NzbDroneClientException(HttpStatusCode.NotFound, "Unable to find matching movie, will need to be manually provided");
                    }
                }

                await _downloadService.DownloadReport(remoteMovie, release.DownloadClientId);
            }
            catch (ReleaseDownloadException ex)
            {
                _logger.Error(ex, ex.Message);
                throw new NzbDroneClientException(HttpStatusCode.Conflict, "Getting release from indexer failed");
            }

            return release;
        }

        [HttpPost("mediaInfo")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public ActionResult<ReleaseMediaInfoResource> GetReleaseMediaInfo([FromBody] ReleaseMediaInfoResource release)
        {
            if (release == null || release.Guid.IsNullOrWhiteSpace() || release.IndexerId <= 0)
            {
                throw new NzbDroneClientException(HttpStatusCode.BadRequest, "Invalid release mediaInfo request");
            }

            var remoteMovie = _remoteMovieCache.Find(GetCacheKey(release.IndexerId, release.Guid));

            if (remoteMovie == null)
            {
                _logger.Debug("Couldn't find requested release mediaInfo source in cache, cache timeout probably expired.");

                throw new NzbDroneClientException(HttpStatusCode.NotFound, "Couldn't find requested release in cache, try searching again");
            }

            var indexer = _indexerFactory.Get(release.IndexerId);
            var settings = indexer?.Settings as NewznabSettings;

            if (settings == null)
            {
                throw new NzbDroneClientException(HttpStatusCode.BadRequest, "Release mediaInfo is only available for Newznab/Torznab indexers");
            }

            var request = new HttpRequestBuilder(BuildProwlarrMediaInfoUrl(settings))
                .Post()
                .Build();
            var prowlarrIndexerId = release.ProwlarrIndexerId > 0 ? release.ProwlarrIndexerId : remoteMovie.Release.ProwlarrIndexerId;

            if (prowlarrIndexerId <= 0)
            {
                throw new NzbDroneClientException(HttpStatusCode.BadRequest, "Release mediaInfo is missing the Prowlarr indexer id");
            }

            _logger.Debug("Radarr mediaInfo proxy request starting: remoteIp {0}, host {1}, release {2}, indexer {3}, prowlarr indexer {4}, existing handle {5}, search {6}",
                Request.GetRemoteIP(),
                Request.Host.Value,
                release.Guid,
                release.IndexerId,
                prowlarrIndexerId,
                release.MediaInfoHandleId,
                release.MediaInfoSearchId);

            request.Headers.ContentType = "application/json";
            request.SetContent(new
            {
                release.Guid,
                IndexerId = prowlarrIndexerId,
                release.MediaInfoHandleId,
                release.MediaInfoSearchId
            }.ToJson());
            request.ContentSummary = $"{{ \"guid\": \"{release.Guid}\", \"indexerId\": {prowlarrIndexerId}, \"mediaInfoHandleId\": \"{release.MediaInfoHandleId}\", \"mediaInfoSearchId\": \"{release.MediaInfoSearchId}\" }}";
            request.SuppressHttpError = true;

            if (settings.ApiKey.IsNotNullOrWhiteSpace())
            {
                request.Headers.Set("X-Api-Key", settings.ApiKey);
            }

            var response = _httpClient.Post<ReleaseMediaInfoResource>(request);

            if (response.HasHttpError)
            {
                _logger.Debug("Prowlarr mediaInfo request failed for release '{0}' from indexer {1} with status {2}", release.Guid, release.IndexerId, response.StatusCode);

                throw new NzbDroneClientException(HttpStatusCode.BadRequest, "Getting release mediaInfo from Prowlarr failed");
            }

            var result = response.Resource;

            if (result == null)
            {
                throw new NzbDroneClientException(HttpStatusCode.BadRequest, "Getting release mediaInfo from Prowlarr returned no data");
            }

            remoteMovie.Release.Subs = result.Subs ?? remoteMovie.Release.Subs;
            remoteMovie.Release.AudioInfo = result.AudioInfo ?? remoteMovie.Release.AudioInfo;
            remoteMovie.Release.MediaInfoStatus = result.MediaInfoStatus;
            remoteMovie.Release.MediaInfoHandleId = result.MediaInfoStatus == "pending" ? result.MediaInfoHandleId : null;
            remoteMovie.Release.MediaInfoSearchId = result.MediaInfoSearchId;
            remoteMovie.Release.MediaInfoProgressStatus = result.MediaInfoProgressStatus;
            remoteMovie.Release.MediaInfoProgressCompleted = result.MediaInfoProgressCompleted;
            remoteMovie.Release.MediaInfoProgressTotal = result.MediaInfoProgressTotal;
            remoteMovie.Release.ProwlarrIndexerId = prowlarrIndexerId;
            var chineseMediaPreference = ChineseMediaPreferenceEvaluator.Evaluate(remoteMovie, _audioLanguageMappingService);
            result.AudioInfo = remoteMovie.Release.AudioInfo;
            result.PreferredAudioInfo = chineseMediaPreference.SelectedAudio;
            result.AudioPreferenceScore = chineseMediaPreference.AudioPreferenceScore;
            result.HasChineseAudioOrSubtitle = chineseMediaPreference.HasChineseAudioOrSubtitle;
            _logger.Debug("Radarr mediaInfo proxy request completed: release {0}, indexer {1}, prowlarr indexer {2}, status {3}, handle {4}, progress {5}/{6} {7}",
                release.Guid,
                release.IndexerId,
                prowlarrIndexerId,
                result.MediaInfoStatus,
                result.MediaInfoHandleId,
                result.MediaInfoProgressCompleted,
                result.MediaInfoProgressTotal,
                result.MediaInfoProgressStatus);

            _remoteMovieCache.Set(GetCacheKey(release.IndexerId, release.Guid), remoteMovie, TimeSpan.FromMinutes(30));

            result.IndexerId = release.IndexerId;
            result.ProwlarrIndexerId = prowlarrIndexerId;

            return Ok(result);
        }

        [HttpPost("mediaInfo/cancel")]
        [Consumes("application/json")]
        [Produces("application/json")]
        public IActionResult CancelReleaseMediaInfo([FromBody] ReleaseMediaInfoResource release)
        {
            if (release == null || release.IndexerId <= 0 || release.MediaInfoHandleId.IsNullOrWhiteSpace())
            {
                return Ok();
            }

            _logger.Debug("Radarr mediaInfo cancel requested: remoteIp {0}, host {1}, release {2}, indexer {3}, prowlarr indexer {4}, handle {5}",
                Request.GetRemoteIP(),
                Request.Host.Value,
                release.Guid,
                release.IndexerId,
                release.ProwlarrIndexerId,
                release.MediaInfoHandleId);

            var indexer = _indexerFactory.Get(release.IndexerId);
            var settings = indexer?.Settings as NewznabSettings;

            if (settings == null)
            {
                return Ok();
            }

            var request = new HttpRequestBuilder(BuildProwlarrMediaInfoCancelUrl(settings))
                .Post()
                .Build();

            request.Headers.ContentType = "application/json";
            request.SetContent(new
            {
                release.MediaInfoHandleId
            }.ToJson());
            request.ContentSummary = $"{{ \"mediaInfoHandleId\": \"{release.MediaInfoHandleId}\" }}";
            request.SuppressHttpError = true;

            if (settings.ApiKey.IsNotNullOrWhiteSpace())
            {
                request.Headers.Set("X-Api-Key", settings.ApiKey);
            }

            _httpClient.Post(request);

            return Ok();
        }

        [HttpGet]
        [Produces("application/json")]
        public async Task<List<ReleaseResource>> GetReleases(int? movieId)
        {
            if (movieId.HasValue)
            {
                return await GetMovieReleases(movieId.Value);
            }

            return await GetRss();
        }

        private async Task<List<ReleaseResource>> GetMovieReleases(int movieId)
        {
            try
            {
                var decisions = await _releaseSearchService.MovieSearch(movieId, true, true);
                var prioritizedDecisions = _prioritizeDownloadDecision.PrioritizeDecisionsForMovies(decisions);

                return MapDecisions(prioritizedDecisions);
            }
            catch (SearchFailedException ex)
            {
                throw new NzbDroneClientException(HttpStatusCode.BadRequest, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Movie search failed: " + ex.Message);
                throw new NzbDroneClientException(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        private async Task<List<ReleaseResource>> GetRss()
        {
            var reports = await _rssFetcherAndParser.Fetch();
            var decisions = _downloadDecisionMaker.GetRssDecision(reports);
            var prioritizedDecisions = _prioritizeDownloadDecision.PrioritizeDecisionsForMovies(decisions);

            return MapDecisions(prioritizedDecisions);
        }

        protected override ReleaseResource MapDecision(DownloadDecision decision, int initialWeight)
        {
            var resource = base.MapDecision(decision, initialWeight);
            var chineseMediaPreference = ChineseMediaPreferenceEvaluator.Evaluate(decision.RemoteMovie, _audioLanguageMappingService);

            resource.AudioInfo = decision.RemoteMovie.Release.AudioInfo;
            resource.PreferredAudioInfo = chineseMediaPreference.SelectedAudio;
            resource.AudioPreferenceScore = chineseMediaPreference.AudioPreferenceScore;
            resource.HasChineseAudioOrSubtitle = chineseMediaPreference.HasChineseAudioOrSubtitle;

            _remoteMovieCache.Set(GetCacheKey(resource), decision.RemoteMovie, TimeSpan.FromMinutes(30));

            return resource;
        }

        private string GetCacheKey(ReleaseResource resource)
        {
            return string.Concat(resource.IndexerId, "_", resource.Guid);
        }

        private string GetCacheKey(int indexerId, string guid)
        {
            return string.Concat(indexerId, "_", guid);
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
    }
}
