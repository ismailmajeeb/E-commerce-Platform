﻿using PowerStore.Framework.Localization;
using PowerStore.Framework.Mapping;
using PowerStore.Core.ModelBinding;
using PowerStore.Core.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using PowerStore.Framework.Mvc.Models;

namespace PowerStore.Web.Areas.Admin.Models.Knowledgebase
{
    public class KnowledgebaseArticleModel : BaseEntityModel, ILocalizedModel<KnowledgebaseArticleLocalizedModel>, IAclMappingModel, IStoreMappingModel
    {
        public KnowledgebaseArticleModel()
        {
            Categories = new List<SelectListItem>();
            Locales = new List<KnowledgebaseArticleLocalizedModel>();
            AvailableCustomerRoles = new List<CustomerRoleModel>();
            AvailableStores = new List<StoreModel>();
        }

        [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.KnowledgebaseArticle.Fields.Name")]
        public string Name { get; set; }

        [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.KnowledgebaseArticle.Fields.Content")]
        public string Content { get; set; }

        [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.KnowledgebaseArticle.Fields.ParentCategoryId")]
        public string ParentCategoryId { get; set; }

        [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.KnowledgebaseArticle.Fields.DisplayOrder")]
        public int DisplayOrder { get; set; }

        [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.KnowledgebaseArticle.Fields.Published")]
        public bool Published { get; set; }

        [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.KnowledgebaseArticle.Fields.MetaKeywords")]
        public string MetaKeywords { get; set; }

        [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.KnowledgebaseArticle.Fields.MetaDescription")]
        public string MetaDescription { get; set; }

        [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.KnowledgebaseArticle.Fields.MetaTitle")]
        public string MetaTitle { get; set; }

        [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.KnowledgebaseArticle.Fields.SeName")]
        public string SeName { get; set; }

        [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.KnowledgebaseArticle.Fields.ShowOnHomepage")]
        public bool ShowOnHomepage { get; set; }

        [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.KnowledgebaseArticle.Fields.AllowComments")]
        public bool AllowComments { get; set; }

        public List<SelectListItem> Categories { get; set; }

        public IList<KnowledgebaseArticleLocalizedModel> Locales { get; set; }

        [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.KnowledgebaseArticle.Fields.SubjectToAcl")]
        public bool SubjectToAcl { get; set; }

        [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.KnowledgebaseArticle.Fields.AclCustomerRoles")]
        public List<CustomerRoleModel> AvailableCustomerRoles { get; set; }

        public string[] SelectedCustomerRoleIds { get; set; }

        [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.KnowledgebaseCategory.Fields.LimitedToStores")]
        public bool LimitedToStores { get; set; }
        [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.KnowledgebaseCategory.Fields.AvailableStores")]
        public List<StoreModel> AvailableStores { get; set; }
        public string[] SelectedStoreIds { get; set; }

        public partial class ActivityLogModel : BaseEntityModel
        {
            [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.ActivityLogType")]
            public string ActivityLogTypeName { get; set; }
            [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.ActivityLog.Comment")]
            public string Comment { get; set; }
            [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.ActivityLog.CreatedOn")]
            public DateTime CreatedOn { get; set; }
            [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.ActivityLog.Customer")]
            public string CustomerId { get; set; }
            public string CustomerEmail { get; set; }
        }

        public partial class AddRelatedArticleModel : BaseEntityModel
        {
            public AddRelatedArticleModel()
            {
                AvailableArticles = new List<SelectListItem>();
            }

            [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.KnowledgebaseArticle.Related.SearchArticleName")]
            public string SearchArticleName { get; set; }

            public string ArticleId { get; set; }

            public string[] SelectedArticlesIds { get; set; }

            public IList<SelectListItem> AvailableArticles { get; set; }
        }
    }

    public class KnowledgebaseArticleLocalizedModel : ILocalizedModelLocal, ISlugModelLocal
    {
        public string LanguageId { get; set; }

        [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.KnowledgebaseArticle.Fields.Name")]
        public string Name { get; set; }

        [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.KnowledgebaseArticle.Fields.Content")]
        public string Content { get; set; }

        [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.KnowledgebaseArticle.Fields.MetaKeywords")]
        public string MetaKeywords { get; set; }

        [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.KnowledgebaseArticle.Fields.MetaDescription")]
        public string MetaDescription { get; set; }

        [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.KnowledgebaseArticle.Fields.MetaTitle")]
        public string MetaTitle { get; set; }

        [PowerStoreResourceDisplayName("Admin.ContentManagement.Knowledgebase.KnowledgebaseArticle.Fields.SeName")]
        public string SeName { get; set; }
    }
}
