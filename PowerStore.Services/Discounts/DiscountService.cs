﻿using PowerStore.Core;
using PowerStore.Domain;
using PowerStore.Core.Caching;
using PowerStore.Domain.Data;
using PowerStore.Domain.Catalog;
using PowerStore.Domain.Customers;
using PowerStore.Domain.Discounts;
using PowerStore.Domain.Orders;
using PowerStore.Core.Plugins;
using PowerStore.Services.Customers;
using PowerStore.Services.Discounts.Cache;
using PowerStore.Services.Events;
using PowerStore.Services.Localization;
using PowerStore.Services.Orders;
using MediatR;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PowerStore.Domain.Directory;
using PowerStore.Services.Directory;
using PowerStore.Core.Caching.Constants;

namespace PowerStore.Services.Discounts
{
    /// <summary>
    /// Discount service
    /// </summary>
    public partial class DiscountService : IDiscountService
    {
        
        #region Fields

        private readonly IRepository<Discount> _discountRepository;
        private readonly IRepository<DiscountCoupon> _discountCouponRepository;
        private readonly IRepository<DiscountUsageHistory> _discountUsageHistoryRepository;
        private readonly ICurrencyService _currencyService;
        private readonly ILocalizationService _localizationService;
        private readonly ICacheBase _cacheBase;
        private readonly IStoreContext _storeContext;
        private readonly IPluginFinder _pluginFinder;
        private readonly IMediator _mediator;
        private readonly ShoppingCartSettings _shoppingCartSettings;
        private readonly CatalogSettings _catalogSettings;

        #endregion

        #region Ctor

        /// <summary>
        /// Ctor
        /// </summary>
        public DiscountService(ICacheBase cacheManager,
            IRepository<Discount> discountRepository,
            IRepository<DiscountCoupon> discountCouponRepository,
            IRepository<DiscountUsageHistory> discountUsageHistoryRepository,
            ICurrencyService currencyService,
            ILocalizationService localizationService,
            IStoreContext storeContext,
            IPluginFinder pluginFinder,
            IMediator mediator,
            ShoppingCartSettings shoppingCartSettings,
            CatalogSettings catalogSettings
            )
        {
            _cacheBase = cacheManager;
            _discountRepository = discountRepository;
            _discountCouponRepository = discountCouponRepository;
            _discountUsageHistoryRepository = discountUsageHistoryRepository;
            _currencyService = currencyService;
            _localizationService = localizationService;
            _storeContext = storeContext;
            _pluginFinder = pluginFinder;
            _mediator = mediator;
            _shoppingCartSettings = shoppingCartSettings;
            _catalogSettings = catalogSettings;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Delete discount
        /// </summary>
        /// <param name="discount">Discount</param>
        public virtual async Task DeleteDiscount(Discount discount)
        {
            if (discount == null)
                throw new ArgumentNullException("discount");

            var usagehistory = await GetAllDiscountUsageHistory(discount.Id);
            if (usagehistory.Count > 0)
                throw new ArgumentNullException("discount has a history");

            await _discountRepository.DeleteAsync(discount);

            await _cacheBase.RemoveByPrefix(CacheKey.DISCOUNTS_PATTERN_KEY);

            //event notification
            await _mediator.EntityDeleted(discount);
        }

        /// <summary>
        /// Gets a discount
        /// </summary>
        /// <param name="discountId">Discount identifier</param>
        /// <returns>Discount</returns>
        public virtual Task<Discount> GetDiscountById(string discountId)
        {
            string key = string.Format(CacheKey.DISCOUNTS_BY_ID_KEY, discountId);
            return _cacheBase.GetAsync(key, () => _discountRepository.GetByIdAsync(discountId));
        }

        /// <summary>
        /// Gets all discounts
        /// </summary>
        /// <param name="discountType">Discount type; null to load all discount</param>
        /// <param name="couponCode">Coupon code to find (exact match)</param>
        /// <param name="discountName">Discount name</param>
        /// <param name="showHidden">A value indicating whether to show hidden records</param>
        /// <returns>Discounts</returns>
        public virtual async Task<IList<Discount>> GetAllDiscounts(DiscountType? discountType,
            string storeId = "", string couponCode = "", string discountName = "", bool showHidden = false)
        {
            //we load all discounts, and filter them by passed "discountType" parameter later
            //we do it because we know that this method is invoked several times per HTTP request with distinct "discountType" parameter
            //that's why let's access the database only once
            string key = string.Format(CacheKey.DISCOUNTS_ALL_KEY, showHidden, storeId, couponCode, discountName);
            var result = await _cacheBase.GetAsync(key, () =>
            {
                var query = _discountRepository.Table;
                if (!showHidden)
                {
                    //The function 'CurrentUtcDateTime' is not supported by SQL Server Compact. 
                    //That's why we pass the date value
                    var nowUtc = DateTime.UtcNow;
                    query = query.Where(d =>
                        (!d.StartDateUtc.HasValue || d.StartDateUtc <= nowUtc)
                        && (!d.EndDateUtc.HasValue || d.EndDateUtc >= nowUtc)
                        && d.IsEnabled);
                }
                if (!string.IsNullOrEmpty(storeId) && !_catalogSettings.IgnoreStoreLimitations)
                {
                    //Store mapping
                    query = from p in query
                            where !p.LimitedToStores || p.Stores.Contains(storeId)
                            select p;
                }
                if (!string.IsNullOrEmpty(couponCode))
                {
                    var _coupon = _discountCouponRepository.Table.FirstOrDefault(x => x.CouponCode == couponCode);
                    if (_coupon != null)
                        query = query.Where(d => d.Id == _coupon.DiscountId);
                }
                if (!string.IsNullOrEmpty(discountName))
                {
                    query = query.Where(d => d.Name != null && d.Name.ToLower().Contains(discountName.ToLower()));
                }
                query = query.OrderBy(d => d.Name);

                var discounts = query.ToListAsync();
                return discounts;
            });
            //we know that this method is usually inkoved multiple times
            //that's why we filter discounts by type on the application layer
            if (discountType.HasValue)
            {
                result = result.Where(d => d.DiscountType == discountType.Value).ToList();
            }
            return result;
        }

        /// <summary>
        /// Inserts a discount
        /// </summary>
        /// <param name="discount">Discount</param>
        public virtual async Task InsertDiscount(Discount discount)
        {
            if (discount == null)
                throw new ArgumentNullException("discount");

            await _discountRepository.InsertAsync(discount);

            await _cacheBase.RemoveByPrefix(CacheKey.DISCOUNTS_PATTERN_KEY);

            //event notification
            await _mediator.EntityInserted(discount);
        }

        /// <summary>
        /// Updates the discount
        /// </summary>
        /// <param name="discount">Discount</param>
        public virtual async Task UpdateDiscount(Discount discount)
        {
            if (discount == null)
                throw new ArgumentNullException("discount");

            foreach (var req in discount.DiscountRequirements)
            {
                req.DiscountId = discount.Id;
            }

            await _discountRepository.UpdateAsync(discount);

            await _cacheBase.RemoveByPrefix(CacheKey.DISCOUNTS_PATTERN_KEY);

            //event notification
            await _mediator.EntityUpdated(discount);
        }

        /// <summary>
        /// Delete discount requirement
        /// </summary>
        /// <param name="discountRequirement">Discount requirement</param>
        public virtual async Task DeleteDiscountRequirement(DiscountRequirement discountRequirement)
        {
            if (discountRequirement == null)
                throw new ArgumentNullException("discountRequirement");

            var discount = await _discountRepository.GetByIdAsync(discountRequirement.DiscountId);
            if (discount == null)
                throw new ArgumentNullException("discount");
            var req = discount.DiscountRequirements.FirstOrDefault(x => x.Id == discountRequirement.Id);
            if (req == null)
                throw new ArgumentNullException("discountRequirement");

            discount.DiscountRequirements.Remove(req);
            await UpdateDiscount(discount);

            await _cacheBase.RemoveByPrefix(CacheKey.DISCOUNTS_PATTERN_KEY);

            //event notification
            await _mediator.EntityDeleted(discountRequirement);
        }

        /// <summary>
        /// Load discount by system name
        /// </summary>
        /// <param name="systemName">System name</param>
        /// <returns>Found discount</returns>
        public virtual IDiscount LoadDiscountPluginBySystemName(string systemName)
        {
            var discountPlugins = LoadAllDiscountPlugins();
            foreach (var discountPlugin in discountPlugins)
            {
                var rules = discountPlugin.GetRequirementRules();

                if (!rules.Any(x => x.SystemName == systemName))
                    continue;
                return discountPlugin;
            }
            return null;
        }

        /// <summary>
        /// Load all discount requirement rules
        /// </summary>
        /// <returns>Discount requirement rules</returns>
        public virtual IList<IDiscount> LoadAllDiscountPlugins()
        {
            var discountPlugins = _pluginFinder.GetPlugins<IDiscount>();
            return discountPlugins.ToList();
        }


        /// <summary>
        /// Get discount by coupon code
        /// </summary>
        /// <param name="couponCode">Coupon code</param>
        /// <param name="showHidden">A value indicating whether to show hidden records</param>
        /// <returns>Discount</returns>
        public virtual async Task<Discount> GetDiscountByCouponCode(string couponCode, bool showHidden = false)
        {
            if (String.IsNullOrWhiteSpace(couponCode))
                return null;

            var builder = Builders<DiscountCoupon>.Filter;
            var filter = builder.Eq(x => x.CouponCode, couponCode);
            var query = await _discountCouponRepository.Collection.FindAsync(filter);

            var coupon = query.FirstOrDefault();
            if (coupon == null)
                return null;

            var discount = await GetDiscountById(coupon.DiscountId);
            return discount;
        }

        /// <summary>
        /// Exist coupon code in discount
        /// </summary>
        /// <param name="couponCode"></param>
        /// <param name="discountId"></param>
        /// <returns></returns>
        public virtual async Task<bool> ExistsCodeInDiscount(string couponCode, string discountId, bool? used)
        {
            if (String.IsNullOrWhiteSpace(couponCode))
                return false;

            var builder = Builders<DiscountCoupon>.Filter;
            var filter = builder.Eq(x => x.CouponCode, couponCode);
            filter = filter & builder.Eq(x => x.DiscountId, discountId);
            if (used.HasValue)
                filter = filter & builder.Eq(x => x.Used, used.Value);
            var query = await _discountCouponRepository.Collection.FindAsync(filter);
            if (query.Any())
                return true;
            else
                return false;
        }

        /// <summary>
        /// Get all coupon codes for discount
        /// </summary>
        /// <param name="discountId"></param>
        /// <param name="pageIndex"></param>
        /// <param name="pageSize"></param>
        /// <returns></returns>
        public virtual async Task<IPagedList<DiscountCoupon>> GetAllCouponCodesByDiscountId(string discountId, int pageIndex = 0, int pageSize = int.MaxValue)
        {
            var query = _discountCouponRepository.Table;

            if (!String.IsNullOrEmpty(discountId))
                query = query.Where(duh => duh.DiscountId == discountId);
            query = query.OrderByDescending(c => c.CouponCode);
            return await PagedList<DiscountCoupon>.Create(query, pageIndex, pageSize);
        }


        /// <summary>
        /// Gets a discount
        /// </summary>
        /// <param name="discountId">Discount identifier</param>
        /// <returns>Discount</returns>
        public virtual Task<DiscountCoupon> GetDiscountCodeById(string id)
        {
            return _discountCouponRepository.GetByIdAsync(id);
        }

        /// <summary>
        /// Get discount code by discount code
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public async Task<DiscountCoupon> GetDiscountCodeByCode(string couponCode)
        {
            var builder = Builders<DiscountCoupon>.Filter;
            var filter = builder.Eq(x => x.CouponCode, couponCode);
            var query = await _discountCouponRepository.Collection.FindAsync(filter);
            return query.FirstOrDefault();
        }


        /// <summary>
        /// Delete discount code
        /// </summary>
        /// <param name="coupon"></param>
        public virtual async Task DeleteDiscountCoupon(DiscountCoupon coupon)
        {
            await _discountCouponRepository.DeleteAsync(coupon);
        }

        /// <summary>
        /// Insert discount code
        /// </summary>
        /// <param name="coupon"></param>
        public virtual async Task InsertDiscountCoupon(DiscountCoupon coupon)
        {
            await _discountCouponRepository.InsertAsync(coupon);
        }

        /// <summary>
        /// Update discount code - set as used or not
        /// </summary>
        /// <param name="coupon"></param>
        public virtual async Task DiscountCouponSetAsUsed(string couponCode, bool used)
        {
            if (string.IsNullOrEmpty(couponCode))
                return;

            var coupon = await GetDiscountCodeByCode(couponCode);
            if (coupon != null)
            {
                if (used)
                {
                    coupon.Used = used;
                    coupon.Qty = coupon.Qty + 1;
                }
                else
                {
                    coupon.Qty = coupon.Qty - 1;
                    coupon.Used = coupon.Qty > 0 ? true : false;
                }
                await _discountCouponRepository.UpdateAsync(coupon);
            }
        }

        /// <summary>
        /// Cancel discount if order was canceled or deleted
        /// </summary>
        /// <param name="orderId"></param>
        public virtual async Task CancelDiscount(string orderId)
        {
            var discountUsage = await _discountUsageHistoryRepository.Table.Where(x => x.OrderId == orderId).ToListAsync();
            foreach (var item in discountUsage)
            {
                await DiscountCouponSetAsUsed(item.CouponCode, false);
                item.Canceled = true;
                await UpdateDiscountUsageHistory(item);
            }
        }

        /// <summary>
        /// Validate discount
        /// </summary>
        /// <param name="discount">Discount</param>
        /// <param name="customer">Customer</param>
        /// <returns>Discount validation result</returns>
        public virtual async Task<DiscountValidationResult> ValidateDiscount(Discount discount, Customer customer)
        {
            if (discount == null)
                throw new ArgumentNullException("discount");

            string[] couponCodesToValidate = null;
            if (customer != null)
                couponCodesToValidate = customer.ParseAppliedCouponCodes(SystemCustomerAttributeNames.DiscountCoupons);

            return await ValidateDiscount(discount, customer, couponCodesToValidate);
        }

        /// <summary>
        /// Validate discount
        /// </summary>
        /// <param name="discount">Discount</param>
        /// <param name="customer">Customer</param>
        /// <param name="couponCodeToValidate">Coupon code to validate</param>
        /// <returns>Discount validation result</returns>
        public virtual async Task<DiscountValidationResult> ValidateDiscount(Discount discount, Customer customer, string couponCodeToValidate)
        {
            if (!String.IsNullOrEmpty(couponCodeToValidate))
            {
                return await ValidateDiscount(discount, customer, new string[] { couponCodeToValidate });
            }
            else
                return await ValidateDiscount(discount, customer, new string[0]);

        }

        /// <summary>
        /// Validate discount
        /// </summary>
        /// <param name="discount">Discount</param>
        /// <param name="customer">Customer</param>
        /// <param name="couponCodesToValidate">Coupon codes to validate</param>
        /// <returns>Discount validation result</returns>
        public virtual async Task<DiscountValidationResult> ValidateDiscount(Discount discount, Customer customer, string[] couponCodesToValidate)
        {
            if (discount == null)
                throw new ArgumentNullException("discount");

            if (customer == null)
                throw new ArgumentNullException("customer");

            var result = new DiscountValidationResult();

            //check is enabled
            if (!discount.IsEnabled)
                return result;

            //do not allow use discount in the current store
            if (discount.LimitedToStores && !discount.Stores.Any(x => _storeContext.CurrentStore.Id == x))
            {
                result.UserError = _localizationService.GetResource("ShoppingCart.Discount.CannotBeUsedInStore");
                return result;
            }

            //check coupon code
            if (discount.RequiresCouponCode)
            {
                if (couponCodesToValidate == null || !couponCodesToValidate.Any())
                    return result;
                var exists = false;
                foreach (var item in couponCodesToValidate)
                {
                    if (discount.Reused)
                    {
                        if (await ExistsCodeInDiscount(item, discount.Id, null))
                        {
                            result.CouponCode = item;
                            exists = true;
                        }
                    }
                    else
                    {
                        if (await ExistsCodeInDiscount(item, discount.Id, false))
                        {
                            result.CouponCode = item;
                            exists = true;
                        }
                    }
                }
                if (!exists)
                    return result;
            }

            //Do not allow discounts applied to order subtotal or total when a customer has gift cards in the cart.
            //Otherwise, this customer can purchase gift cards with discount and get more than paid ("free money").
            if (discount.DiscountType == DiscountType.AssignedToOrderSubTotal ||
                discount.DiscountType == DiscountType.AssignedToOrderTotal)
            {
                var cart = customer.ShoppingCartItems
                    .Where(sci => sci.ShoppingCartType == ShoppingCartType.ShoppingCart)
                    .LimitPerStore(_shoppingCartSettings.CartsSharedBetweenStores, _storeContext.CurrentStore.Id)
                    .ToList();

                var hasGiftCards = cart.Any(x => x.IsGiftCard);
                if (hasGiftCards)
                {
                    result.UserError = _localizationService.GetResource("ShoppingCart.Discount.CannotBeUsedWithGiftCards");
                    return result;
                }
            }

            //check date range
            DateTime now = DateTime.UtcNow;
            if (discount.StartDateUtc.HasValue)
            {
                DateTime startDate = DateTime.SpecifyKind(discount.StartDateUtc.Value, DateTimeKind.Utc);
                if (startDate.CompareTo(now) > 0)
                {
                    result.UserError = _localizationService.GetResource("ShoppingCart.Discount.NotStartedYet");
                    return result;
                }
            }
            if (discount.EndDateUtc.HasValue)
            {
                DateTime endDate = DateTime.SpecifyKind(discount.EndDateUtc.Value, DateTimeKind.Utc);
                if (endDate.CompareTo(now) < 0)
                {
                    result.UserError = _localizationService.GetResource("ShoppingCart.Discount.Expired");
                    return result;
                }
            }

            //discount limitation
            switch (discount.DiscountLimitation)
            {
                case DiscountLimitationType.NTimesOnly:
                    {
                        var usedTimes = await GetAllDiscountUsageHistory(discount.Id, null, null, false, 0, 1);
                        if (usedTimes.TotalCount >= discount.LimitationTimes)
                            return result;
                    }
                    break;
                case DiscountLimitationType.NTimesPerCustomer:
                    {
                        if (customer.IsRegistered())
                        {
                            var usedTimes = await GetAllDiscountUsageHistory(discount.Id, customer.Id, null, false, 0, 1);
                            if (usedTimes.TotalCount >= discount.LimitationTimes)
                            {
                                result.UserError = _localizationService.GetResource("ShoppingCart.Discount.CannotBeUsedAnymore");
                                return result;
                            }
                        }
                    }
                    break;
                case DiscountLimitationType.Unlimited:
                default:
                    break;
            }

            //discount requirements
            string keyReq = string.Format(DiscountRequirementEventConsumer.DISCOUNT_REQUIREMENT_MODEL_KEY, discount.Id);
            var requirements = await _cacheBase.GetAsync(keyReq, async () =>
            {
                return await Task.FromResult(discount.DiscountRequirements.ToList());
            });
            foreach (var req in requirements)
            {
                //load a plugin
                var discountRequirementPlugin = LoadDiscountPluginBySystemName(req.DiscountRequirementRuleSystemName);

                if (discountRequirementPlugin == null)
                    continue;

                if (!_pluginFinder.AuthenticateStore(discountRequirementPlugin.PluginDescriptor, _storeContext.CurrentStore.Id))
                    continue;

                var ruleRequest = new DiscountRequirementValidationRequest {
                    DiscountRequirementId = req.Id,
                    DiscountId = req.DiscountId,
                    Customer = customer,
                    Store = _storeContext.CurrentStore
                };

                var singleRequirementRule = discountRequirementPlugin.GetRequirementRules().Single(x => x.SystemName == req.DiscountRequirementRuleSystemName);
                var ruleResult = await singleRequirementRule.CheckRequirement(ruleRequest);
                if (!ruleResult.IsValid)
                {
                    result.UserError = ruleResult.UserError;
                    return result;
                }
            }

            result.IsValid = true;
            return result;

            // });

            //return validationResult;
        }
        /// <summary>
        /// Gets a discount usage history record
        /// </summary>
        /// <param name="discountUsageHistoryId">Discount usage history record identifier</param>
        /// <returns>Discount usage history</returns>
        public virtual Task<DiscountUsageHistory> GetDiscountUsageHistoryById(string discountUsageHistoryId)
        {
            return _discountUsageHistoryRepository.GetByIdAsync(discountUsageHistoryId);
        }

        /// <summary>
        /// Gets all discount usage history records
        /// </summary>
        /// <param name="discountId">Discount identifier; null to load all records</param>
        /// <param name="customerId">Customer identifier; null to load all records</param>
        /// <param name="orderId">Order identifier; null to load all records</param>
        /// <param name="pageIndex">Page index</param>
        /// <param name="pageSize">Page size</param>
        /// <returns>Discount usage history records</returns>
        public virtual async Task<IPagedList<DiscountUsageHistory>> GetAllDiscountUsageHistory(string discountId = "",
            string customerId = "", string orderId = "", bool? canceled = null,
            int pageIndex = 0, int pageSize = int.MaxValue)
        {
            var query = _discountUsageHistoryRepository.Table;

            if (!String.IsNullOrEmpty(discountId))
                query = query.Where(duh => duh.DiscountId == discountId);
            if (!String.IsNullOrEmpty(customerId))
                query = query.Where(duh => duh.CustomerId == customerId);
            if (!String.IsNullOrEmpty(orderId))
                query = query.Where(duh => duh.OrderId == orderId);
            if (canceled.HasValue)
                query = query.Where(duh => duh.Canceled == canceled.Value);
            query = query.OrderByDescending(c => c.CreatedOnUtc);
            return await PagedList<DiscountUsageHistory>.Create(query, pageIndex, pageSize);
        }

        /// <summary>
        /// Insert discount usage history record
        /// </summary>
        /// <param name="discountUsageHistory">Discount usage history record</param>
        public virtual async Task InsertDiscountUsageHistory(DiscountUsageHistory discountUsageHistory)
        {
            if (discountUsageHistory == null)
                throw new ArgumentNullException("discountUsageHistory");

            await _discountUsageHistoryRepository.InsertAsync(discountUsageHistory);

            //Support for couponcode
            await DiscountCouponSetAsUsed(discountUsageHistory.CouponCode, true);

            await _cacheBase.RemoveByPrefix(CacheKey.DISCOUNTS_PATTERN_KEY);

            //event notification
            await _mediator.EntityInserted(discountUsageHistory);
        }


        /// <summary>
        /// Update discount usage history record
        /// </summary>
        /// <param name="discountUsageHistory">Discount usage history record</param>
        public virtual async Task UpdateDiscountUsageHistory(DiscountUsageHistory discountUsageHistory)
        {
            if (discountUsageHistory == null)
                throw new ArgumentNullException("discountUsageHistory");

            await _discountUsageHistoryRepository.UpdateAsync(discountUsageHistory);

            await _cacheBase.RemoveByPrefix(CacheKey.DISCOUNTS_PATTERN_KEY);

            //event notification
            await _mediator.EntityUpdated(discountUsageHistory);
        }

        /// <summary>
        /// Delete discount usage history record
        /// </summary>
        /// <param name="discountUsageHistory">Discount usage history record</param>
        public virtual async Task DeleteDiscountUsageHistory(DiscountUsageHistory discountUsageHistory)
        {
            if (discountUsageHistory == null)
                throw new ArgumentNullException("discountUsageHistory");

            await _discountUsageHistoryRepository.DeleteAsync(discountUsageHistory);

            await _cacheBase.RemoveByPrefix(CacheKey.DISCOUNTS_PATTERN_KEY);

            //event notification
            await _mediator.EntityDeleted(discountUsageHistory);
        }

        /// <summary>
        /// Get discount amount from plugin
        /// </summary>
        /// <param name="discount">Discount</param>
        /// <param name="amount">Amount</param>
        /// <param name="currency">currency</param>
        /// <param name="customer">Customer</param>
        /// <param name="product">Product</param>
        public async Task<decimal> GetDiscountAmount(Discount discount, Customer customer, Currency currency, Product product, decimal amount)
        {
            if (discount == null)
                throw new ArgumentNullException("discount");

            //calculate discount amount
            decimal result = decimal.Zero;
            if (!discount.CalculateByPlugin)
            {
                if (discount.UsePercentage)
                    result = (decimal)((((float)amount) * ((float)discount.DiscountPercentage)) / 100f);
                else
                {
                    result = await _currencyService.ConvertFromPrimaryStoreCurrency(discount.DiscountAmount, currency);
                }
            }
            else
            {
                result = await GetDiscountAmountProvider(discount, customer, product, amount);
            }

            //validate maximum disocunt amount
            if (discount.UsePercentage &&
                discount.MaximumDiscountAmount.HasValue &&
                result > discount.MaximumDiscountAmount.Value)
                result = discount.MaximumDiscountAmount.Value;

            if (result < decimal.Zero)
                result = decimal.Zero;

            return result;
        }

        /// <summary>
        /// Get preferred discount (with maximum discount value)
        /// </summary>
        /// <param name="discounts">A list of discounts to check</param>
        /// <param name="customer">customer</param>
        /// <param name="currency">currency</param>
        /// <param name="product"></param>
        /// <param name="amount">Amount</param>
        /// <param name="discountAmount"></param>
        /// <returns>Preferred discount</returns>
        public virtual async Task<(List<AppliedDiscount> appliedDiscount, decimal discountAmount)> GetPreferredDiscount(
            IList<AppliedDiscount> discounts, Customer customer, Currency currency, Product product,
            decimal amount)
        {
            if (discounts == null)
                throw new ArgumentNullException("discounts");

            var appliedDiscount = new List<AppliedDiscount>();
            var discountAmount = decimal.Zero;
            if (!discounts.Any())
                return (appliedDiscount, discountAmount);

            //first we check simple discounts
            foreach (var applieddiscount in discounts)
            {
                var discount = await GetDiscountById(applieddiscount.DiscountId);
                decimal currentDiscountValue = await GetDiscountAmount(discount, customer, currency, product, amount);
                if (currentDiscountValue > discountAmount)
                {
                    discountAmount = currentDiscountValue;
                    appliedDiscount.Clear();
                    appliedDiscount.Add(applieddiscount);
                }
            }
            //now let's check cumulative discounts
            //right now we calculate discount values based on the original amount value
            //please keep it in mind if you're going to use discounts with "percentage"
            var cumulativeDiscounts = discounts.Where(x => x.IsCumulative).ToList();
            if (cumulativeDiscounts.Count > 1)
            {
                var cumulativeDiscountAmount = decimal.Zero;
                foreach (var item in cumulativeDiscounts)
                {
                    var discount = await GetDiscountById(item.DiscountId);
                    cumulativeDiscountAmount += await GetDiscountAmount(discount, customer, currency, product, amount);
                }
                if (cumulativeDiscountAmount > discountAmount)
                {
                    discountAmount = cumulativeDiscountAmount;

                    appliedDiscount.Clear();
                    appliedDiscount.AddRange(cumulativeDiscounts);
                }
            }

            return (appliedDiscount, discountAmount);
        }
        /// <summary>
        /// Get preferred discount (with maximum discount value)
        /// </summary>
        /// <param name="discounts">A list of discounts to check</param>
        /// <param name="customer"></param>
        /// <param name="currency">currency</param>
        /// <param name="amount">Amount</param>
        /// <param name="discountAmount"></param>
        /// <returns>Preferred discount</returns>
        public virtual async Task<(List<AppliedDiscount> appliedDiscount, decimal discountAmount)> GetPreferredDiscount(
            IList<AppliedDiscount> discounts,
            Customer customer,
            Currency currency,
            decimal amount)
        {
            return await GetPreferredDiscount(discounts, customer, currency, null, amount);
        }

        /// <summary>
        /// Get amount from discount amount provider 
        /// </summary>
        /// <param name="discount"></param>
        /// <param name="amount"></param>
        /// <returns></returns>
        public virtual async Task<decimal> GetDiscountAmountProvider(Discount discount, Customer customer, Product product, decimal amount)
        {
            var discountAmountProvider = LoadDiscountAmountProviderBySystemName(discount.DiscountPluginName);
            if (discountAmountProvider == null)
                return 0;
            return await discountAmountProvider.DiscountAmount(discount, customer, product, amount);
        }


        public virtual IDiscountAmountProvider LoadDiscountAmountProviderBySystemName(string systemName)
        {
            var descriptor = _pluginFinder.GetPluginDescriptorBySystemName<IDiscountAmountProvider>(systemName);
            if (descriptor != null)
                return descriptor.Instance<IDiscountAmountProvider>(_pluginFinder.ServiceProvider);

            return null;
        }

        /// <summary>
        /// Get all discount amount providers
        /// </summary>
        /// <returns></returns>
        public IList<IDiscountAmountProvider> LoadDiscountAmountProviders()
        {
            var discountAmountProviders = _pluginFinder.GetPlugins<IDiscountAmountProvider>();
            return discountAmountProviders
                .OrderBy(tp => tp.PluginDescriptor)
                .ToList();
        }

        #endregion
    }
}
