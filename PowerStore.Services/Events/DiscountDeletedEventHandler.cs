﻿using PowerStore.Core.Caching;
using PowerStore.Domain.Data;
using PowerStore.Domain.Catalog;
using PowerStore.Domain.Discounts;
using PowerStore.Domain.Vendors;
using PowerStore.Core.Events;
using MediatR;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Threading;
using System.Threading.Tasks;
using PowerStore.Core.Caching.Constants;

namespace PowerStore.Services.Events
{
    public class DiscountDeletedEventHandler : INotificationHandler<EntityDeleted<Discount>>
    {
        
        #region Fields

        private readonly IRepository<Product> _productRepository;
        private readonly IRepository<Category> _categoryRepository;
        private readonly IRepository<Manufacturer> _manufacturerRepository;
        private readonly IRepository<Vendor> _vendorRepository;
        private readonly IRepository<DiscountCoupon> _discountCouponRepository;
        private readonly ICacheBase _cacheBase;

        #endregion

        public DiscountDeletedEventHandler(
            IRepository<Product> productRepository,
            IRepository<Category> categoryRepository,
            IRepository<Manufacturer> manufacturerRepository,
            IRepository<Vendor> vendorRepository,
            IRepository<DiscountCoupon> discountCouponRepository,
            ICacheBase cacheManager
        )
        {
            _productRepository = productRepository;
            _categoryRepository = categoryRepository;
            _manufacturerRepository = manufacturerRepository;
            _vendorRepository = vendorRepository;
            _vendorRepository = vendorRepository;
            _discountCouponRepository = discountCouponRepository;
            _cacheBase = cacheManager;
        }

        public async Task Handle(EntityDeleted<Discount> notification, CancellationToken cancellationToken)
        {
            var discount = notification.Entity;

            var builder = Builders<BsonDocument>.Filter;
            if (discount.DiscountType == DiscountType.AssignedToSkus)
            {
                var builderproduct = Builders<Product>.Update;
                var updatefilter = builderproduct.Pull(x => x.AppliedDiscounts, discount.Id);
                await _productRepository.Collection.UpdateManyAsync(new BsonDocument(), updatefilter);
                await _cacheBase.RemoveByPrefix(CacheKey.PRODUCTS_PATTERN_KEY);
            }

            if (discount.DiscountType == DiscountType.AssignedToCategories)
            {
                var buildercategory = Builders<Category>.Update;
                var updatefilter = buildercategory.Pull(x => x.AppliedDiscounts, discount.Id);
                await _categoryRepository.Collection.UpdateManyAsync(new BsonDocument(), updatefilter);
                await _cacheBase.RemoveByPrefix(CacheKey.CATEGORIES_PATTERN_KEY);
            }

            if (discount.DiscountType == DiscountType.AssignedToManufacturers)
            {
                var buildermanufacturer = Builders<Manufacturer>.Update;
                var updatefilter = buildermanufacturer.Pull(x => x.AppliedDiscounts, discount.Id);
                await _manufacturerRepository.Collection.UpdateManyAsync(new BsonDocument(), updatefilter);
                await _cacheBase.RemoveByPrefix(CacheKey.MANUFACTURERS_PATTERN_KEY);
            }
            if (discount.DiscountType == DiscountType.AssignedToVendors)
            {
                var buildervendor = Builders<Vendor>.Update;
                var updatefilter = buildervendor.Pull(x => x.AppliedDiscounts, discount.Id);
                await _vendorRepository.Collection.UpdateManyAsync(new BsonDocument(), updatefilter);
                await _cacheBase.RemoveByPrefix(CacheKey.PRODUCTS_PATTERN_KEY);
            }

            //remove coupon codes
            var filtersCoupon = Builders<DiscountCoupon>.Filter;
            var filterCrp = filtersCoupon.Eq(x => x.DiscountId, discount.Id);

            await _discountCouponRepository.Collection.DeleteManyAsync(filterCrp);
        }
    }
}
