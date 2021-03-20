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

            var originalRepo = context.RouteData.Values["repo"] as string;
            var request = context.HttpContext.Request;
            var requestHost = request.Host.Host;
            if (originalRepo == null ||
                !requestHost.EndsWith(_options.MirrorModeBaseDomain)
                || requestHost.Length <= _options.MirrorModeBaseDomain.Length)
            {
                return;
            }

            // foo.bar.base.domain  =>  foo.bar
            var registry = requestHost.Substring(0,requestHost.Length - _options.MirrorModeBaseDomain.Length - 1);
            context.RouteData.Values["repo"] = $"{registry}/{originalRepo}";
        }
    }
}