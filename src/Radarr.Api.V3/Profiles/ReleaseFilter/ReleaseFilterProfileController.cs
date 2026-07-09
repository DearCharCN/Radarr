using System.Collections.Generic;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.Profiles.ReleaseFilters;
using Radarr.Http;
using Radarr.Http.REST;
using Radarr.Http.REST.Attributes;

namespace Radarr.Api.V3.Profiles.ReleaseFilter
{
    [V3ApiController]
    public class ReleaseFilterProfileController : RestController<ReleaseFilterProfileResource>
    {
        private readonly IReleaseFilterProfileService _releaseFilterProfileService;

        public ReleaseFilterProfileController(IReleaseFilterProfileService releaseFilterProfileService)
        {
            _releaseFilterProfileService = releaseFilterProfileService;

            SharedValidator.RuleFor(c => c.Name).NotEmpty();
            SharedValidator.RuleFor(c => c.Filter).NotNull();
        }

        protected override ReleaseFilterProfileResource GetResourceById(int id)
        {
            return _releaseFilterProfileService.Get(id).ToResource();
        }

        [HttpGet]
        public List<ReleaseFilterProfileResource> GetAll()
        {
            return _releaseFilterProfileService.All().ToResource();
        }

        [RestPostById]
        [Consumes("application/json")]
        public ActionResult<ReleaseFilterProfileResource> Create([FromBody] ReleaseFilterProfileResource resource)
        {
            var model = _releaseFilterProfileService.Add(resource.ToModel());

            return Created(model.Id);
        }

        [RestPutById]
        [Consumes("application/json")]
        public ActionResult<ReleaseFilterProfileResource> Update([FromBody] ReleaseFilterProfileResource resource)
        {
            var model = resource.ToModel();

            _releaseFilterProfileService.Update(model);

            return Accepted(model.Id);
        }

        [RestDeleteById]
        public void Delete(int id)
        {
            _releaseFilterProfileService.Delete(id);
        }
    }
}
