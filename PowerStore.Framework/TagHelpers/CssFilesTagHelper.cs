﻿using PowerStore.Framework.UI;
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Threading.Tasks;

namespace PowerStore.Framework.TagHelpers
{
    [HtmlTargetElement("css-files", TagStructure = TagStructure.WithoutEndTag)]
    [HtmlTargetElement("css-files", Attributes = AttributeNameLocation)]
    public class CssFilesTagHelper : TagHelper
    {
        private const string AttributeNameLocation = "asp-location";
        [HtmlAttributeName(AttributeNameLocation)]
        public ResourceLocation Location { get; set; }

        public bool? BundleFiles { get; set; } = null;

        private readonly IPageHeadBuilder _pageHeadBuilder;

        public CssFilesTagHelper(IPageHeadBuilder pageHeadBuilder)
        {
            _pageHeadBuilder = pageHeadBuilder;
        }

        public override Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            output.Content.SetHtmlContent(_pageHeadBuilder.GenerateCssFiles(Location, BundleFiles));
            output.TagName = null;
            return Task.CompletedTask;
        }
    }
}