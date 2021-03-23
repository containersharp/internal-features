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
            if (Enum.TryParse(typeof(HttpMethod), context.Request.Method, out var httpMethodObj)
            && Array.IndexOf(UpdatingRequestMethods, (HttpMethod) httpMethodObj) > -1)
            {
                context.Response.StatusCode = 404;
                await context.Response.CompleteAsync();
                return;
            }
            
            await next(context);
        }
    }
}