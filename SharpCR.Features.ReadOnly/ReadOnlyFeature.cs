using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace SharpCR.Features.ReadOnly
{
    public class ReadOnlyFeature : IFeature
    {
        public void ConfigureServices(IServiceCollection services, StartupContext context)
        {
            services.AddScoped<ReadOnlyMiddleware>();
        }

        public void ConfigureWebAppPipeline(IApplicationBuilder app, IServiceProvider appServices, StartupContext context)
        {
            app.UseMiddleware<ReadOnlyMiddleware>();
        }
    }
}
