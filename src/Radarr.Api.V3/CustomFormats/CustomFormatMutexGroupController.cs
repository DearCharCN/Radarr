using System.Collections.Generic;
using System.Linq;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.CustomFormats;
using Radarr.Http;
using Radarr.Http.REST;
using Radarr.Http.REST.Attributes;

namespace Radarr.Api.V3.CustomFormats
{
    [V3ApiController]
    public class CustomFormatMutexGroupController : RestController<CustomFormatMutexGroupResource>
    {
        private readonly ICustomFormatMutexGroupService _customFormatMutexGroupService;

        public CustomFormatMutexGroupController(ICustomFormatMutexGroupService customFormatMutexGroupService,
                                                ICustomFormatService customFormatService)
        {
            _customFormatMutexGroupService = customFormatMutexGroupService;

            SharedValidator.RuleFor(c => c.Name).NotEmpty();
            SharedValidator.RuleFor(c => c.CustomFormatIds).NotNull().NotEmpty();
            SharedValidator.RuleFor(c => c.CustomFormatIds).Must(ids =>
            {
                var customFormatIds = customFormatService.All().Select(format => format.Id).ToHashSet();

                return ids != null && ids.All(customFormatIds.Contains);
            }).WithMessage("Custom Format Mutex Group contains an unknown Custom Format");
        }

        protected override CustomFormatMutexGroupResource GetResourceById(int id)
        {
            return _customFormatMutexGroupService.Get(id).ToResource();
        }

        [HttpGet]
        public List<CustomFormatMutexGroupResource> GetAll()
        {
            return _customFormatMutexGroupService.All().ToResource();
        }

        [RestPostById]
        [Consumes("application/json")]
        public ActionResult<CustomFormatMutexGroupResource> Create([FromBody] CustomFormatMutexGroupResource resource)
        {
            var model = _customFormatMutexGroupService.Add(resource.ToModel());

            return Created(model.Id);
        }

        [RestPutById]
        [Consumes("application/json")]
        public ActionResult<CustomFormatMutexGroupResource> Update([FromBody] CustomFormatMutexGroupResource resource)
        {
            var model = resource.ToModel();

            _customFormatMutexGroupService.Update(model);

            return Accepted(model.Id);
        }

        [RestDeleteById]
        public void Delete(int id)
        {
            _customFormatMutexGroupService.Delete(id);
        }
    }
}
