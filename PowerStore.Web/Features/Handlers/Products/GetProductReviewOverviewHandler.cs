﻿using PowerStore.Core.Caching;
using PowerStore.Domain.Catalog;
using PowerStore.Services.Catalog;
using PowerStore.Web.Features.Models.Products;
using PowerStore.Web.Infrastructure.Cache;
using PowerStore.Web.Models.Catalog;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace PowerStore.Web.Features.Handlers.Products
{
    public class GetProductReviewOverviewHandler : IRequestHandler<GetProductReviewOverview, ProductReviewOverviewModel>
    {
        private readonly ICacheBase _cacheBase;
        private readonly IProductReviewService _productReviewService;
        private readonly CatalogSettings _catalogSettings;

        public GetProductReviewOverviewHandler(
            ICacheBase cacheManager,
            IProductReviewService productReviewService, 
            CatalogSettings catalogSettings)
        {
            _cacheBase = cacheManager;
            _productReviewService = productReviewService;
            _catalogSettings = catalogSettings;
        }

        public async Task<ProductReviewOverviewModel> Handle(GetProductReviewOverview request, CancellationToken cancellationToken)
        {
            ProductReviewOverviewModel productReview = null;

            if (_catalogSettings.ShowProductReviewsPerStore)
            {
                string cacheKey = string.Format(ModelCacheEventConst.PRODUCT_REVIEWS_MODEL_KEY, request.Product.Id, request.Store.Id);

                productReview = await _cacheBase.GetAsync(cacheKey, async () =>
                {
                    return new ProductReviewOverviewModel {
                        RatingSum = await _productReviewService.RatingSumProduct(request.Product.Id, _catalogSettings.ShowProductReviewsPerStore ? request.Store.Id : ""),
                        TotalReviews = await _productReviewService.TotalReviewsProduct(request.Product.Id, _catalogSettings.ShowProductReviewsPerStore ? request.Store.Id : ""),
                    };
                });
            }
            else
            {
                productReview = new ProductReviewOverviewModel() {
                    RatingSum = request.Product.ApprovedRatingSum,
                    TotalReviews = request.Product.ApprovedTotalReviews
                };
            }

            if (productReview != null)
            {
                productReview.ProductId = request.Product.Id;
                productReview.AllowCustomerReviews = request.Product.AllowCustomerReviews;
            }
            return productReview;
        }
    }
}
