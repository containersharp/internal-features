using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SharpCR.Features.SyncIntegration
{
    public class SyncIntegrationFeature : IFeature
    {
        public void ConfigureServices(IServiceCollection services, StartupContext context)
        {
            services.AddScoped<ImagePullHookMiddleware>();
        }

        public void ConfigureWebAppPipeline(IApplicationBuilder app, IServiceProvider appServices, StartupContext context)
        {
            app.UseMiddleware<ImagePullHookMiddleware>();
        }
    }
}
