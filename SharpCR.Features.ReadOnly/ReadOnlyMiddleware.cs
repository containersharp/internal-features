using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace SharpCR.Features.ReadOnly
{
    public class ReadOnlyMiddleware: IMiddleware
    {
        private readonly ILogger<ReadOnlyMiddleware> _logger;

        public ReadOnlyMiddleware(ILogger<ReadOnlyMiddleware> logger)
        {
            _logger = logger;
        }
        
        private static readonly HttpMethod[] UpdatingRequestMethods =
        {
            HttpMethod.Put,
            HttpMethod.Post,
            HttpMethod.Patch,
            HttpMethod.Delete
        };
        
        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var method = new HttpMethod(context.Request.Method);
            if (Array.IndexOf(UpdatingRequestMethods, method) > -1)
            {
                var uriPath = context.Request.Path.Value;
                _logger.LogInformation("Request rejected because this site is readonly {@req}", new {httpMethod = method.Method, uriPath });
                context.Response.StatusCode = 404;
                await context.Response.CompleteAsync();
                return;
            }
            
            await next(context);
        }
    }
}