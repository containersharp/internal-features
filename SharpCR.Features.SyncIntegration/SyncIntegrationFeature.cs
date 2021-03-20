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
            var configuration = context.Configuration.GetSection("Features:SyncIntegration")?.Get<SyncConfiguration>() ?? new SyncConfiguration();
            services.AddSingleton(Options.Create(configuration));
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
