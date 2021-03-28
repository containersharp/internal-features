using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace SharpCR.Features.ReadOnly
{
    public class ReadOnlyMiddleware: IMiddleware
    {
        private static readonly HttpMethod[] UpdatingRequestMethods = new HttpMethod[]
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
                context.Response.StatusCode = 404;
                await context.Response.CompleteAsync();
                return;
            }
            
            await next(context);
        }
    }
}