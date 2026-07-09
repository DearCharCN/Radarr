using System.Collections.Generic;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Profiles.AudioLanguageMappings;
using Radarr.Http;
using Radarr.Http.REST;
using Radarr.Http.REST.Attributes;

namespace Radarr.Api.V3.Profiles.AudioLanguageMapping
{
    [V3ApiController]
    public class AudioLanguageMappingController : RestController<AudioLanguageMappingResource>
    {
        private readonly IAudioLanguageMappingService _audioLanguageMappingService;

        public AudioLanguageMappingController(IAudioLanguageMappingService audioLanguageMappingService)
        {
            _audioLanguageMappingService = audioLanguageMappingService;

            SharedValidator.RuleFor(c => c.Language).NotNull();
            SharedValidator.RuleFor(c => c.Language.Id).NotEqual(Language.Unknown.Id).When(c => c.Language != null);
            SharedValidator.RuleFor(c => c.Aliases).NotNull().NotEmpty();
        }

        protected override AudioLanguageMappingResource GetResourceById(int id)
        {
            return _audioLanguageMappingService.Get(id).ToResource();
        }

        [HttpGet]
        public List<AudioLanguageMappingResource> GetAll()
        {
            return _audioLanguageMappingService.All().ToResource();
        }

        [RestPostById]
        [Consumes("application/json")]
        public ActionResult<AudioLanguageMappingResource> Create([FromBody] AudioLanguageMappingResource resource)
        {
            var model = _audioLanguageMappingService.Add(resource.ToModel());

            return Created(model.Id);
        }

        [RestPutById]
        [Consumes("application/json")]
        public ActionResult<AudioLanguageMappingResource> Update([FromBody] AudioLanguageMappingResource resource)
        {
            var model = resource.ToModel();

            _audioLanguageMappingService.Update(model);

            return Accepted(model.Id);
        }

        [RestDeleteById]
        public void Delete(int id)
        {
            _audioLanguageMappingService.Delete(id);
        }
    }
}
