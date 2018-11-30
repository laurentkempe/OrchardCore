using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Mvc.ViewFeatures.Internal;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Descriptors.ShapeTemplateStrategy;
using OrchardCore.DisplayManagement.Implementation;

namespace OrchardCore.DisplayManagement.Razor
{
    public class RazorShapeTemplateViewEngine : IShapeTemplateViewEngine
    {
        private readonly IOptions<MvcViewOptions> _options;
        private readonly ViewContextAccessor _viewContextAccessor;
        private readonly IServiceProvider _serviceProvider;
        private readonly ITempDataProvider _tempDataProvider;
        private readonly List<string> _templateFileExtensions = new List<string>(new[] { RazorViewEngine.ViewExtension });

        public RazorShapeTemplateViewEngine(
            IOptions<MvcViewOptions> options,
            IEnumerable<IRazorViewExtensionProvider> viewExtensionProviders,
            ViewContextAccessor viewContextAccessor,
            IServiceProvider serviceProvider,
            ITempDataProvider tempDataProvider)
        {
            _options = options;
            _viewContextAccessor = viewContextAccessor;
            _serviceProvider = serviceProvider;
            _tempDataProvider = tempDataProvider;
            _templateFileExtensions.AddRange(viewExtensionProviders.Select(x => x.ViewExtension));
        }

        public IEnumerable<string> TemplateFileExtensions
        {
            get
            {
                return _templateFileExtensions;
            }
        }

        public Task<IHtmlContent> RenderAsync(string relativePath, DisplayContext displayContext)
        {

            var viewName = "/" + relativePath;
            viewName = Path.ChangeExtension(viewName, RazorViewEngine.ViewExtension);

            var viewContext = _viewContextAccessor.ViewContext;

            //IHtmlContent htmlContent;

            //var originalPrefix = viewContext.ViewData.TemplateInfo.HtmlFieldPrefix;
            //viewContext.ViewData.TemplateInfo.HtmlFieldPrefix = displayContext.HtmlFieldPrefix;

            if (viewContext?.View != null)
            {
                viewContext = new ViewContext(viewContext, viewContext.View, viewContext.ViewData, viewContext.Writer);
                viewContext.ViewData.TemplateInfo.HtmlFieldPrefix = displayContext.HtmlFieldPrefix;

                var htmlHelper = MakeHtmlHelper(viewContext, viewContext.ViewData);
                return htmlHelper.PartialAsync(viewName, displayContext.Value);
            }
            else
            {
                // If the View is null, it means that the shape is being executed from a non-view origin / where no ViewContext was established by the view engine, but manually.
                // Manually creating a ViewContext works when working with Shape methods, but not when the shape is implemented as a Razor view template.
                // Horrible, but it will have to do for now.
                return RenderRazorViewAsync(viewName, displayContext);
            }

            //viewContext.ViewData.TemplateInfo.HtmlFieldPrefix = originalPrefix;
        }

        private async Task<IHtmlContent> RenderRazorViewAsync(string viewName, DisplayContext displayContext)
        {
            var viewEngines = _options.Value.ViewEngines;

            if (viewEngines.Count == 0)
            {
                throw new InvalidOperationException(string.Format("'{0}.{1}' must not be empty. At least one '{2}' is required to locate a view for rendering.",
                    typeof(MvcViewOptions).FullName,
                    nameof(MvcViewOptions.ViewEngines),
                    typeof(IViewEngine).FullName));
            }

            var viewEngine = viewEngines[0] as IRazorViewEngine;

            var result = await RenderViewToStringAsync(viewName, displayContext.Value, viewEngine);

            return new HtmlString(result);
        }

        public async Task<string> RenderViewToStringAsync(string viewName, object model, IRazorViewEngine viewEngine)
        {
            var actionContext = GetActionContext();
            var view = FindView(actionContext, viewName, viewEngine);

            using (var output = new StringWriter())
            {
                var viewContext = new ViewContext(
                    actionContext,
                    view,
                    new ViewDataDictionary(
                        metadataProvider: new EmptyModelMetadataProvider(),
                        modelState: new ModelStateDictionary())
                    {
                        Model = model
                    },
                    new TempDataDictionary(
                        actionContext.HttpContext,
                        _tempDataProvider),
                    output,
                    new HtmlHelperOptions());

                await view.RenderAsync(viewContext);

                return output.ToString();
            }
        }

        private IView FindView(ActionContext actionContext, string viewName, IRazorViewEngine viewEngine)
        {
            var getViewResult = viewEngine.GetView(executingFilePath: null, viewPath: viewName, isMainPage: true);
            if (getViewResult.Success)
            {
                return getViewResult.View;
            }

            var findViewResult = viewEngine.FindView(actionContext, viewName, isMainPage: true);
            if (findViewResult.Success)
            {
                return findViewResult.View;
            }

            var searchedLocations = getViewResult.SearchedLocations.Concat(findViewResult.SearchedLocations);
            var errorMessage = string.Join(
                System.Environment.NewLine,
                new[] { $"Unable to find view '{viewName}'. The following locations were searched:" }.Concat(searchedLocations)); ;

            throw new InvalidOperationException(errorMessage);
        }

        private ActionContext GetActionContext()
        {
            var httpContext = new DefaultHttpContext();
            httpContext.RequestServices = _serviceProvider;
            return new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        }

        private static IHtmlHelper MakeHtmlHelper(ViewContext viewContext, ViewDataDictionary viewData)
        {
            var newHelper = viewContext.HttpContext.RequestServices.GetRequiredService<IHtmlHelper>();

            var contextable = newHelper as IViewContextAware;
            if (contextable != null)
            {
                var newViewContext = new ViewContext(viewContext, viewContext.View, viewData, viewContext.Writer);
                contextable.Contextualize(newViewContext);
            }

            return newHelper;
        }
    }
}