﻿using PowerStore.Core;
using PowerStore.Core.Caching;
using PowerStore.Domain.Catalog;
using PowerStore.Domain.Media;
using PowerStore.Domain.Vendors;
using PowerStore.Services.Catalog;
using PowerStore.Services.Localization;
using PowerStore.Services.Media;
using PowerStore.Services.Queries.Models.Catalog;
using PowerStore.Services.Seo;
using PowerStore.Web.Features.Models.Catalog;
using PowerStore.Web.Features.Models.Common;
using PowerStore.Web.Features.Models.Products;
using PowerStore.Web.Models.Catalog;
using PowerStore.Web.Models.Media;
using MediatR;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PowerStore.Web.Features.Handlers.Catalog
{
    public class GetVendorHandler : IRequestHandler<GetVendor, VendorModel>
    {
        private readonly IMediator _mediator;
        private readonly IPictureService _pictureService;
        private readonly ILocalizationService _localizationService;
        private readonly ISpecificationAttributeService _specificationAttributeService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ICacheBase _cacheBase;
        private readonly IWebHelper _webHelper;

        private readonly VendorSettings _vendorSettings;
        private readonly MediaSettings _mediaSettings;
        private readonly CatalogSettings _catalogSettings;

        public GetVendorHandler(
            IMediator mediator,
            IPictureService pictureService,
            ILocalizationService localizationService,
            VendorSettings vendorSettings,
            MediaSettings mediaSettings,
            ISpecificationAttributeService specificationAttributeService,
            IHttpContextAccessor httpContextAccessor,
            ICacheBase cacheManager,
            IWebHelper webHelper,
            CatalogSettings catalogSettings)
        {
            _mediator = mediator;
            _pictureService = pictureService;
            _localizationService = localizationService;
            _vendorSettings = vendorSettings;
            _mediaSettings = mediaSettings;
            _specificationAttributeService = specificationAttributeService;
            _httpContextAccessor = httpContextAccessor;
            _cacheBase = cacheManager;
            _webHelper = webHelper;
            _catalogSettings = catalogSettings;
        }

        public async Task<VendorModel> Handle(GetVendor request, CancellationToken cancellationToken)
        {
            var model = new VendorModel {
                Id = request.Vendor.Id,
                Name = request.Vendor.GetLocalized(x => x.Name, request.Language.Id),
                Description = request.Vendor.GetLocalized(x => x.Description, request.Language.Id),
                MetaKeywords = request.Vendor.GetLocalized(x => x.MetaKeywords, request.Language.Id),
                MetaDescription = request.Vendor.GetLocalized(x => x.MetaDescription, request.Language.Id),
                MetaTitle = request.Vendor.GetLocalized(x => x.MetaTitle, request.Language.Id),
                SeName = request.Vendor.GetSeName(request.Language.Id),
                AllowCustomersToContactVendors = _vendorSettings.AllowCustomersToContactVendors,
                GenericAttributes = request.Vendor.GenericAttributes
            };

            model.Address = await _mediator.Send(new GetVendorAddress() {
                Language = request.Language,
                Address = request.Vendor.Address,
                ExcludeProperties = false,
            });

            //prepare picture model
            var pictureModel = new PictureModel {
                Id = request.Vendor.PictureId,
                FullSizeImageUrl = await _pictureService.GetPictureUrl(request.Vendor.PictureId),
                ImageUrl = await _pictureService.GetPictureUrl(request.Vendor.PictureId, _mediaSettings.VendorThumbPictureSize),
                Title = string.Format(_localizationService.GetResource("Media.Vendor.ImageLinkTitleFormat"), model.Name),
                AlternateText = string.Format(_localizationService.GetResource("Media.Vendor.ImageAlternateTextFormat"), model.Name)
            };
            model.PictureModel = pictureModel;

            //view/sorting/page size
            var options = await _mediator.Send(new GetViewSortSizeOptions() {
                Command = request.Command,
                PagingFilteringModel = request.Command,
                Language = request.Language,
                AllowCustomersToSelectPageSize = request.Vendor.AllowCustomersToSelectPageSize,
                PageSize = request.Vendor.PageSize,
                PageSizeOptions = request.Vendor.PageSizeOptions
            });
            model.PagingFilteringContext = options.command;

            IList<string> alreadyFilteredSpecOptionIds = await model.PagingFilteringContext.SpecificationFilter.GetAlreadyFilteredSpecOptionIds
              (_httpContextAccessor, _specificationAttributeService);

            //products
            var products = (await _mediator.Send(new GetSearchProductsQuery() {
                LoadFilterableSpecificationAttributeOptionIds = !_catalogSettings.IgnoreFilterableSpecAttributeOption,
                Customer = request.Customer,
                VendorId = request.Vendor.Id,
                StoreId = request.Store.Id,
                FilteredSpecs = alreadyFilteredSpecOptionIds,
                VisibleIndividuallyOnly = true,
                OrderBy = (ProductSortingEnum)request.Command.OrderBy,
                PageIndex = request.Command.PageNumber - 1,
                PageSize = request.Command.PageSize
            }));

            model.Products = (await _mediator.Send(new GetProductOverview() {
                Products = products.products,
                PrepareSpecificationAttributes = _catalogSettings.ShowSpecAttributeOnCatalogPages
            })).ToList();

            model.PagingFilteringContext.LoadPagedList(products.products);

            //specs
            await model.PagingFilteringContext.SpecificationFilter.PrepareSpecsFilters(alreadyFilteredSpecOptionIds,
                products.filterableSpecificationAttributeOptionIds,
                _specificationAttributeService, _webHelper, _cacheBase, request.Language.Id);

            return model;
        }
    }
}
