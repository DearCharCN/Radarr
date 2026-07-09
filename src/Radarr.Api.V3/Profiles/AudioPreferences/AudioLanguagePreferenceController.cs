using System.Collections.Generic;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Profiles.AudioPreferences;
using NzbDrone.Core.Profiles.AudioScoring;
using Radarr.Http;
using Radarr.Http.REST;
using Radarr.Http.REST.Attributes;

namespace Radarr.Api.V3.Profiles.AudioPreferences
{
    [V3ApiController]
    public class AudioLanguagePreferenceController : RestController<AudioLanguagePreferenceResource>
    {
        private readonly IAudioLanguagePreferenceService _audioLanguagePreferenceService;

        public AudioLanguagePreferenceController(IAudioLanguagePreferenceService audioLanguagePreferenceService,
                                                 IAudioScoreProfileService audioScoreProfileService)
        {
            _audioLanguagePreferenceService = audioLanguagePreferenceService;

            SharedValidator.RuleFor(c => c.Name).NotEmpty();
            SharedValidator.RuleFor(c => c.ScoreGapThreshold).GreaterThanOrEqualTo(0);
            SharedValidator.RuleFor(c => c.Entries).NotNull().NotEmpty();
            SharedValidator.RuleFor(c => c.AudioScoreProfileId).Must(id =>
            {
                return !id.HasValue || id.Value == 0 || audioScoreProfileService.Exists(id.Value);
            }).WithMessage("Audio Score Profile does not exist");
        }

        protected override AudioLanguagePreferenceResource GetResourceById(int id)
        {
            return _audioLanguagePreferenceService.Get(id).ToResource();
        }

        [HttpGet]
        public List<AudioLanguagePreferenceResource> GetAll()
        {
            return _audioLanguagePreferenceService.All().ToResource();
        }

        [RestPostById]
        [Consumes("application/json")]
        public ActionResult<AudioLanguagePreferenceResource> Create([FromBody] AudioLanguagePreferenceResource resource)
        {
            var model = _audioLanguagePreferenceService.Add(resource.ToModel());

            return Created(model.Id);
        }

        [RestPutById]
        [Consumes("application/json")]
        public ActionResult<AudioLanguagePreferenceResource> Update([FromBody] AudioLanguagePreferenceResource resource)
        {
            var model = resource.ToModel();

            _audioLanguagePreferenceService.Update(model);

            return Accepted(model.Id);
        }

        [RestDeleteById]
        public void Delete(int id)
        {
            _audioLanguagePreferenceService.Delete(id);
        }
    }
}
