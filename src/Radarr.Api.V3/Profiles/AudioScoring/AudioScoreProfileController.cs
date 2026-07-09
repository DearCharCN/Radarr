using System.Collections.Generic;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Profiles.AudioScoring;
using Radarr.Http;
using Radarr.Http.REST;
using Radarr.Http.REST.Attributes;

namespace Radarr.Api.V3.Profiles.AudioScoring
{
    [V3ApiController]
    public class AudioScoreProfileController : RestController<AudioScoreProfileResource>
    {
        private readonly IAudioScoreProfileService _audioScoreProfileService;

        public AudioScoreProfileController(IAudioScoreProfileService audioScoreProfileService)
        {
            _audioScoreProfileService = audioScoreProfileService;

            SharedValidator.RuleFor(c => c.Name).NotEmpty();
            SharedValidator.RuleFor(c => c.Rules).NotNull();
            SharedValidator.RuleFor(c => c.MutexGroups).NotNull();
        }

        protected override AudioScoreProfileResource GetResourceById(int id)
        {
            return _audioScoreProfileService.Get(id).ToResource();
        }

        [HttpGet]
        public List<AudioScoreProfileResource> GetAll()
        {
            return _audioScoreProfileService.All().ToResource();
        }

        [RestPostById]
        [Consumes("application/json")]
        public ActionResult<AudioScoreProfileResource> Create([FromBody] AudioScoreProfileResource resource)
        {
            var model = _audioScoreProfileService.Add(resource.ToModel());

            return Created(model.Id);
        }

        [RestPutById]
        [Consumes("application/json")]
        public ActionResult<AudioScoreProfileResource> Update([FromBody] AudioScoreProfileResource resource)
        {
            var model = resource.ToModel();

            _audioScoreProfileService.Update(model);

            return Accepted(model.Id);
        }

        [RestDeleteById]
        public void Delete(int id)
        {
            _audioScoreProfileService.Delete(id);
        }
    }
}
