﻿using PowerStore.Core;
using PowerStore.Core.Caching;
using PowerStore.Domain;
using PowerStore.Domain.Catalog;
using PowerStore.Domain.Media;
using PowerStore.Services.Catalog;
using PowerStore.Services.Customers;
using PowerStore.Services.Localization;
using PowerStore.Services.Media;
using PowerStore.Services.Queries.Models.Catalog;
using PowerStore.Web.Extensions;
using PowerStore.Web.Features.Models.Catalog;
using PowerStore.Web.Features.Models.Products;
using PowerStore.Web.Infrastructure.Cache;
using PowerStore.Web.Models.Catalog;
using PowerStore.Web.Models.Media;
using MediatR;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PowerStore.Web.Features.Handlers.Catalog
{
    public class GetCategoryFeaturedProductsHandler : IRequestHandler<GetCategoryFeaturedProducts, IList<CategoryModel>>
    {
        private readonly ICategoryService _categoryService;
        private readonly ICacheBase _cacheBase;
        private readonly IPictureService _pictureService;
        private readonly ILocalizationService _localizationService;
        private readonly IMediator _mediator;
        private readonly MediaSettings _mediaSettings;
        private readonly CatalogSettings _catalogSettings;

        public GetCategoryFeaturedProductsHandler(
            ICategoryService categoryService,
            ICacheBase cacheManager,
            IPictureService pictureService,
            ILocalizationService localizationService,
            IMediator mediator,
            MediaSettings mediaSettings,
            CatalogSettings catalogSettings)
        {
            _categoryService = categoryService;
            _cacheBase = cacheManager;
            _pictureService = pictureService;
            _localizationService = localizationService;
            _mediator = mediator;
            _mediaSettings = mediaSettings;
            _catalogSettings = catalogSettings;
        }

        public async Task<IList<CategoryModel>> Handle(GetCategoryFeaturedProducts request, CancellationToken cancellationToken)
        {
            string categoriesCacheKey = string.Format(ModelCacheEventConst.CATEGORY_FEATURED_PRODUCTS_HOMEPAGE_KEY,
                string.Join(",", request.Customer.GetCustomerRoleIds()), request.Store.Id,
                request.Language.Id);

            var model = await _cacheBase.GetAsync(categoriesCacheKey, async () =>
            {
                var catlistmodel = new List<CategoryModel>();
                foreach (var x in await _categoryService.GetAllCategoriesFeaturedProductsOnHomePage())
                {
                    var catModel = x.ToModel(request.Language);
                    //prepare picture model
                    catModel.PictureModel = new PictureModel {
                        Id = x.PictureId,
                        FullSizeImageUrl = await _pictureService.GetPictureUrl(x.PictureId),
                        ImageUrl = await _pictureService.GetPictureUrl(x.PictureId, _mediaSettings.CategoryThumbPictureSize),
                        Title = string.Format(_localizationService.GetResource("Media.Category.ImageLinkTitleFormat"), catModel.Name),
                        AlternateText = string.Format(_localizationService.GetResource("Media.Category.ImageAlternateTextFormat"), catModel.Name)
                    };
                    catlistmodel.Add(catModel);
                }
                return catlistmodel;
            });


            foreach (var item in model)
            {
                //We cache a value indicating whether we have featured products
                IPagedList<Product> featuredProducts = null;
                string cacheKey = string.Format(ModelCacheEventConst.CATEGORY_HAS_FEATURED_PRODUCTS_KEY, item.Id,
                    string.Join(",", request.Customer.GetCustomerRoleIds()), request.Store.Id);

                var hasFeaturedProductsCache = await _cacheBase.GetAsync<bool?>(cacheKey, async () =>
                {
                    featuredProducts = (await _mediator.Send(new GetSearchProductsQuery() {
                        PageSize = _catalogSettings.LimitOfFeaturedProducts,
                        CategoryIds = new List<string> { item.Id },
                        Customer = request.Customer,
                        StoreId = request.Store.Id,
                        VisibleIndividuallyOnly = true,
                        FeaturedProducts = true
                    })).products;
                    return featuredProducts.Any();
                });

                if (hasFeaturedProductsCache.Value && featuredProducts == null)
                {
                    //cache indicates that the category has featured products
                    //let's load them
                    featuredProducts = (await _mediator.Send(new GetSearchProductsQuery() {
                        PageSize = _catalogSettings.LimitOfFeaturedProducts,
                        CategoryIds = new List<string> { item.Id },
                        Customer = request.Customer,
                        StoreId = request.Store.Id,
                        VisibleIndividuallyOnly = true,
                        FeaturedProducts = true
                    })).products;
                }
                if (featuredProducts != null && featuredProducts.Any())
                {
                    item.FeaturedProducts = (await _mediator.Send(new GetProductOverview() {
                        PrepareSpecificationAttributes = _catalogSettings.ShowSpecAttributeOnCatalogPages,
                        Products = featuredProducts,
                    })).ToList();
                }
            }

            return model;
        }
    }
}
