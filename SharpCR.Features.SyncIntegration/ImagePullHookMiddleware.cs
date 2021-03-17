using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;

namespace SharpCR.Features.SyncIntegration
{
    public class ImagePullHookMiddleware: IMiddleware
    {
        public Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            // var imagePull = context.Request
            throw new System.NotImplementedException();
        }
    }
}