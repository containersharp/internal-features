using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace SharpCR.Features.CloudStorage
{
    public class CloudStorageFeature : IFeature
    {
        public void ConfigureServices(IServiceCollection services, StartupContext context)
        {
            var configuration = context.Configuration.GetSection("Features:CloudStorage")?.Get<CloudStorageConfiguration>() ?? new CloudStorageConfiguration();
            services.AddSingleton(Options.Create(configuration));
            services.AddSingleton<CloudBlobStorage>();

        }

        public void ConfigureWebAppPipeline(IApplicationBuilder app, IServiceProvider appServices, StartupContext context)
        {

        }
    }
}