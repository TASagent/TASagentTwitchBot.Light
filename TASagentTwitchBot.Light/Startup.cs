using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using TASagentTwitchBot.Plugin.ControllerSpy.Web;

namespace TASagentTwitchBot.Light
{
    public class Startup : Core.StartupCore
    {
        public Startup(IConfiguration configuration)
            : base(configuration)
        {
        }

        protected override void ConfigureAddCustomAssemblies(IMvcBuilder builder) =>
            builder.AddControllerSpyControllerAssembly();

        protected override string[] GetExcludedFeatures() => new string[] { "TTS", "Audio" };

        protected override void ConfigureCustomServices(IServiceCollection services)
        {
            //Register core application
            services.AddSingleton<LightDemoApplication>();

            //Controller Overlay
            services.RegisterControllerSpyServices();
        }

        protected override void BuildCustomEndpointRoutes(IEndpointRouteBuilder endpoints)
        {
            endpoints.RegisterControllerSpyEndpoints();
        }

        protected override void ConfigureCustomStaticFilesSupplement(IApplicationBuilder app, IWebHostEnvironment env)
        {
            UseCoreLibraryContent(app, env, "TASagentTwitchBot.Plugin.ControllerSpy");
        }
    }
}
