﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PowerStore.Core.Caching;
using PowerStore.Domain.Catalog;
using PowerStore.Services.Catalog;
using PowerStore.Services.Customers;
using PowerStore.Services.Localization;
using PowerStore.Services.Seo;
using PowerStore.Web.Features.Models.Catalog;
using PowerStore.Web.Infrastructure.Cache;
using PowerStore.Web.Models.Catalog;
using MediatR;

namespace PowerStore.Web.Features.Handlers.Catalog
{
    public class GetManufacturerNavigationHandler : IRequestHandler<GetManufacturerNavigation, ManufacturerNavigationModel>
    {
        private readonly ICacheBase _cacheBase;
        private readonly IManufacturerService _manufacturerService;
        private readonly CatalogSettings _catalogSettings;

        public GetManufacturerNavigationHandler(ICacheBase cacheManager, 
            IManufacturerService manufacturerService, 
            CatalogSettings catalogSettings)
        {
            _cacheBase = cacheManager;
            _manufacturerService = manufacturerService;
            _catalogSettings = catalogSettings;
        }

        public async Task<ManufacturerNavigationModel> Handle(GetManufacturerNavigation request, CancellationToken cancellationToken)
        {
            string cacheKey = string.Format(ModelCacheEventConst.MANUFACTURER_NAVIGATION_MODEL_KEY,
                request.CurrentManufacturerId, request.Language.Id, string.Join(",", request.Customer.GetCustomerRoleIds()),
                request.Store.Id);
            var cacheModel = await _cacheBase.GetAsync(cacheKey, async () =>
            {
                var currentManufacturer = await _manufacturerService.GetManufacturerById(request.CurrentManufacturerId);
                var manufacturers = await _manufacturerService.GetAllManufacturers(pageSize: _catalogSettings.ManufacturersBlockItemsToDisplay, storeId: request.Store.Id);
                var model = new ManufacturerNavigationModel {
                    TotalManufacturers = manufacturers.TotalCount
                };

                foreach (var manufacturer in manufacturers)
                {
                    var modelMan = new ManufacturerBriefInfoModel {
                        Id = manufacturer.Id,
                        Name = manufacturer.GetLocalized(x => x.Name, request.Language.Id),
                        Icon = manufacturer.Icon,
                        SeName = manufacturer.GetSeName(request.Language.Id),
                        IsActive = currentManufacturer != null && currentManufacturer.Id == manufacturer.Id,
                    };
                    model.Manufacturers.Add(modelMan);
                }
                return model;
            });
            return cacheModel;
        }
    }
}
