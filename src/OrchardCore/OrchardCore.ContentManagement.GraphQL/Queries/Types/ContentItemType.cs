using System.IO;
using System.Text.Encodings.Web;
using GraphQL.Types;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Apis.GraphQL;
using OrchardCore.ContentManagement.Display;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;

namespace OrchardCore.ContentManagement.GraphQL.Queries.Types
{
    public class ContentItemType : ObjectGraphType<ContentItem>
    {
        public ContentItemType()
        {
            Name = "ContentItemType";

            Field(ci => ci.ContentItemId).Description("Content item id");
            Field(ci => ci.ContentItemVersionId).Description("The content item version id");
            Field(ci => ci.ContentType).Description("Type of content");
            Field(ci => ci.DisplayText).Description("The display text of the content item");
            Field(ci => ci.Published).Description("Is the published version");
            Field(ci => ci.Latest).Description("Is the latest version");
            Field<DateTimeGraphType>("modifiedUtc", resolve: ci => ci.Source.ModifiedUtc, description: "The date and time of modification");
            Field<DateTimeGraphType>("publishedUtc", resolve: ci => ci.Source.PublishedUtc, description: "The date and time of publication");
            Field<DateTimeGraphType>("createdUtc", resolve: ci => ci.Source.CreatedUtc, description: "The date and time of creation");
            Field(ci => ci.Owner).Description("The owner of the content item");
            Field(ci => ci.Author).Description("The author of the content item");

            Field<StringGraphType>()
                .Name("render")
                .ResolveAsync(async context =>
                {
                    var userContext = (GraphQLContext)context.UserContext;
                    var serviceProvider = userContext.ServiceProvider;

                    // Build shape
                    var displayManager = serviceProvider.GetRequiredService<IContentItemDisplayManager>();
                    var updateModelAccessor = serviceProvider.GetRequiredService<IUpdateModelAccessor>();
                    var model = await displayManager.BuildDisplayAsync(context.Source, updateModelAccessor.ModelUpdater);

                    var displayHelper = serviceProvider.GetRequiredService<IDisplayHelperFactory>().CreateHelper();

                    using (var sw = new StringWriter())
                    {
                        var htmlContent = await displayHelper.ShapeExecuteAsync(model);
                        htmlContent.WriteTo(sw, HtmlEncoder.Default);
                        return sw.ToString();
                    }                        
                });            

            Interface<ContentItemInterface>();

            IsTypeOf = IsContentType;
        }

        private bool IsContentType(object obj)
        {
            return obj is ContentItem item && item.ContentType == Name;
        }
    }
}
