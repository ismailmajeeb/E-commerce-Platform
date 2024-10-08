﻿using PowerStore.Api.Commands.Models.Catalog;
using PowerStore.Api.DTOs.Catalog;
using PowerStore.Api.Extensions;
using PowerStore.Services.Catalog;
using PowerStore.Services.Localization;
using PowerStore.Services.Logging;
using MediatR;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
namespace PowerStore.Api.Commands.Handlers.Catalog
{
    public class UpdateSpecificationAttributeCommandHandler : IRequestHandler<UpdateSpecificationAttributeCommand, SpecificationAttributeDto>
    {
        private readonly ISpecificationAttributeService _specificationAttributeService;
        private readonly ICustomerActivityService _customerActivityService;
        private readonly ILocalizationService _localizationService;

        public UpdateSpecificationAttributeCommandHandler(
            ISpecificationAttributeService specificationAttributeService,
            ICustomerActivityService customerActivityService,
            ILocalizationService localizationService)
        {
            _specificationAttributeService = specificationAttributeService;
            _customerActivityService = customerActivityService;
            _localizationService = localizationService;
        }

        public async Task<SpecificationAttributeDto> Handle(UpdateSpecificationAttributeCommand request, CancellationToken cancellationToken)
        {
            var specificationAttribute = await _specificationAttributeService.GetSpecificationAttributeById(request.Model.Id);
            foreach (var option in specificationAttribute.SpecificationAttributeOptions)
            {
                if (request.Model.SpecificationAttributeOptions.FirstOrDefault(x => x.Id == option.Id) == null)
                {
                    await _specificationAttributeService.DeleteSpecificationAttributeOption(option);
                }
            }
            specificationAttribute = request.Model.ToEntity(specificationAttribute);
            await _specificationAttributeService.UpdateSpecificationAttribute(specificationAttribute);

            //activity log
            await _customerActivityService.InsertActivity("EditSpecAttribute",
                specificationAttribute.Id, _localizationService.GetResource("ActivityLog.EditSpecAttribute"), specificationAttribute.Name);

            return specificationAttribute.ToModel();
        }
    }
}
