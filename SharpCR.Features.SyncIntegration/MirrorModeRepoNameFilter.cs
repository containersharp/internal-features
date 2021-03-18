using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace SharpCR.Features.SyncIntegration
{
    public class MirrorModeRepoNameFilter : IActionFilter
    {
        private readonly SyncConfiguration _options;

        public MirrorModeRepoNameFilter(IOptions<SyncConfiguration> options)
        {
            _options = options.Value;
        }
            
        public void OnActionExecuted(ActionExecutedContext context)
        {
            
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (string.IsNullOrEmpty(_options.MirrorModeBaseDomain))
            {
                return;
            }

            var request = context.HttpContext.Request;
            if (!request.Host.Host.EndsWith(_options.MirrorModeBaseDomain))
            {
                return;
            }

            // foo.bar.base.domain
            var registry = request.Host.Host.Substring(0,request.Host.Host.Length - _options.MirrorModeBaseDomain.Length - 1);
            var repo = context.RouteData.Values["repo"] as string;
            context.RouteData.Values["repo"] = $"{registry}/{repo}";
        }
    }
}