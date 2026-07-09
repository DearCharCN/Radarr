using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Profiles.AudioPreferences;
using NzbDrone.Core.Profiles.AudioScoring;
using NzbDrone.Core.Profiles.Qualities;
using NzbDrone.Core.Profiles.ReleaseFilters;
using Radarr.Http;
using Radarr.Http.REST;
using Radarr.Http.REST.Attributes;

namespace Radarr.Api.V3.Profiles.Quality
{
    [V3ApiController]
    public class QualityProfileController : RestController<QualityProfileResource>
    {
        private readonly IQualityProfileService _qualityProfileService;

        public QualityProfileController(IQualityProfileService qualityProfileService,
                                        ICustomFormatService formatService,
                                        IReleaseFilterProfileService releaseFilterProfileService,
                                        IAudioLanguagePreferenceService audioLanguagePreferenceService,
                                        IAudioScoreProfileService audioScoreProfileService,
                                        ICustomFormatMutexGroupService customFormatMutexGroupService)
        {
            _qualityProfileService = qualityProfileService;

            SharedValidator.RuleFor(c => c.Name).NotEmpty();

            // TODO: Need to validate the cutoff is allowed and the ID/quality ID exists
            // TODO: Need to validate the Items to ensure groups have names and at no item has no name, no items and no quality
            SharedValidator.RuleFor(c => c.MinUpgradeFormatScore).GreaterThanOrEqualTo(1);
            SharedValidator.RuleFor(c => c.Cutoff).ValidCutoff();
            SharedValidator.RuleFor(c => c.Items).ValidItems();

            SharedValidator.RuleFor(c => c.FormatItems).Must(items =>
            {
                var all = formatService.All().Select(f => f.Id).ToList();
                var ids = items.Select(i => i.Format);

                return all.Except(ids).Empty();
            }).WithMessage("All Custom Formats and no extra ones need to be present inside your Profile! Try refreshing your browser.");

            SharedValidator.RuleFor(c => c.ReleaseFilterProfileId).Must(id =>
            {
                return !id.HasValue || id.Value == 0 || releaseFilterProfileService.Exists(id.Value);
            }).WithMessage("Release Filter Profile does not exist");

            SharedValidator.RuleFor(c => c.AudioLanguagePreferenceId).Must(id =>
            {
                return !id.HasValue || id.Value == 0 || audioLanguagePreferenceService.Exists(id.Value);
            }).WithMessage("Audio Language Preference does not exist");

            SharedValidator.RuleFor(c => c.AudioScoreProfileId).Must(id =>
            {
                return !id.HasValue || id.Value == 0 || audioScoreProfileService.Exists(id.Value);
            }).WithMessage("Audio Score Profile does not exist");

            SharedValidator.RuleFor(c => c.CustomFormatMutexGroupIds).Must(ids =>
            {
                return ids == null || ids.All(id => id > 0 && customFormatMutexGroupService.Exists(id));
            }).WithMessage("Custom Format Mutex Group does not exist");

            SharedValidator.RuleFor(c => c).Custom((profile, context) =>
            {
                if (profile.FormatItems.Where(x => x.Score > 0).Sum(x => x.Score) < profile.MinFormatScore &&
                    profile.FormatItems.Max(x => x.Score) < profile.MinFormatScore)
                {
                    context.AddFailure("Minimum Custom Format Score can never be satisfied");
                }
            });
        }

        [RestPostById]
        [Consumes("application/json")]
        public ActionResult<QualityProfileResource> Create([FromBody] QualityProfileResource resource)
        {
            var model = resource.ToModel();
            model = _qualityProfileService.Add(model);
            return Created(model.Id);
        }

        [RestDeleteById]
        public void DeleteProfile(int id)
        {
            _qualityProfileService.Delete(id);
        }

        [RestPutById]
        [Consumes("application/json")]
        public ActionResult<QualityProfileResource> Update([FromBody] QualityProfileResource resource)
        {
            var model = resource.ToModel();

            _qualityProfileService.Update(model);

            return Accepted(model.Id);
        }

        protected override QualityProfileResource GetResourceById(int id)
        {
            return _qualityProfileService.Get(id).ToResource();
        }

        [HttpGet]
        public List<QualityProfileResource> GetAll()
        {
            return _qualityProfileService.All().ToResource();
        }
    }
}
