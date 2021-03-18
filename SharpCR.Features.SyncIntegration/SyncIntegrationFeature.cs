using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace SharpCR.Features.SyncIntegration
{
    public class SyncIntegrationFeature : IFeature
    {
        public void ConfigureServices(IServiceCollection services, StartupContext context)
        {
            services.AddControllers(options =>
            {
                options.Filters.Add(typeof(MirrorModeRepoNameFilter));
            });
            services.AddScoped<ImagePullHookMiddleware>();
        }

        public void ConfigureWebAppPipeline(IApplicationBuilder app, IServiceProvider appServices, StartupContext context)
        {
            app.UseMiddleware<ImagePullHookMiddleware>();
        }
    }
}
