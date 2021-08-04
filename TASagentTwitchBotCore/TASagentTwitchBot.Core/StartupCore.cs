using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;

using TASagentTwitchBot.Core.Web;

namespace TASagentTwitchBot.Core
{
    public class StartupCore
    {
        public StartupCore(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        /// <summary>
        /// Custom extension services
        /// If you are just adding or swapping functionality, register services here
        /// </summary>
        protected virtual void ConfigureCustomServices(IServiceCollection services) { }

        /// <summary>
        /// Post configuration method for forcing the construction of some singletons that register themselves.
        /// If you are just adding or swapping functionality, Construct Singletons here
        /// </summary>
        protected virtual void ConstructCustomSingletons(IServiceProvider serviceProvider) { }

        /// <summary>
        /// Configures custom middleware
        /// If you are just adding or swapping functionality, Configure custom middleware here
        /// </summary>
        protected virtual void ConfigureCustomMiddleware(IApplicationBuilder app) { }

        /// <summary>
        /// Mapping Hubs
        /// If you are just adding or swapping functionality, Mapping custom Hubs here
        /// </summary>
        protected virtual void BuildCustomEndpointRoutes(IEndpointRouteBuilder endpoints) { }

        protected virtual void ConfigureAddCustomAssemblies(IMvcBuilder builder) { }

        protected virtual void ConfigureCustomStaticFilesOverride(IApplicationBuilder app, IWebHostEnvironment env) { }
        protected virtual void ConfigureCustomStaticFilesSupplement(IApplicationBuilder app, IWebHostEnvironment env) { }

        protected virtual string[] GetExcludedFeatures() => Array.Empty<string>();

        protected virtual void ConfigureAddControllers(IServiceCollection services)
        {
            IMvcBuilder mvcBuilder = services.GetMvcBuilder();

            ConfigureAddCustomAssemblies(mvcBuilder);

            mvcBuilder.RegisterControllersWithoutFeatures(GetExcludedFeatures());
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        public virtual void ConfigureServices(IServiceCollection services)
        {
            ConfigureAddControllers(services);
            ConfigureCoreServicesInitial(services);
            ConfigureDatabases(services);
            ConfigureCoreServices(services);
            ConfigureCustomServices(services);
        }

        protected virtual void ConfigureCoreServicesInitial(IServiceCollection services)
        {
#if DEBUG
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "TwitchBotApplication", Version = "v1" });
            });
#endif

            services.AddSignalR();
        }

        protected virtual void ConfigureDatabases(IServiceCollection services) { }

        protected virtual void ConfigureCoreServices(IServiceCollection services)
        {
            //Construct or load BotConfiguration
            services
                .AddSingleton<Config.BotConfiguration>(Config.BotConfiguration.GetConfig());

            services
                .AddSingleton<IConfigurator, StandardConfigurator>();

            services
                .AddSingleton<ErrorHandler>()
                .AddSingleton<ApplicationManagement>();

            services
                .AddSingleton<ICommunication, CommunicationHandler>()
                .AddSingleton<IMessageAccumulator, MessageAccumulator>()
                .AddSingleton<View.IConsoleOutput, View.BasicView>()
                .AddSingleton<Timer.ITimerManager, Timer.TimerManager>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public virtual void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            SetupDatabase(app);
            ConfigureCoreInitial(app, env);
            ConfigureStaticFiles(app, env);
            ConfigureCoreMiddleware(app);
            ConfigureCustomMiddleware(app);

            app.UseEndpoints(BuildEndpointRoutes);

            ConstructCoreSingletons(app.ApplicationServices);
            ConstructCustomSingletons(app.ApplicationServices);
        }

        protected virtual void SetupDatabase(IApplicationBuilder app) { }

        protected virtual void BuildEndpointRoutes(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapControllers();

            BuildCoreEndpointRoutes(endpoints);
            BuildCustomEndpointRoutes(endpoints);
        }

        /// <summary>
        /// Mapping Core Hubs.
        /// Provided for more customizable behavior.
        /// </summary>
        protected virtual void BuildCoreEndpointRoutes(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapHub<Web.Hubs.MonitorHub>("/Hubs/Monitor");
            endpoints.MapHub<Web.Hubs.TimerHub>("/Hubs/Timer");
        }

        protected virtual void ConfigureCoreInitial(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
#if DEBUG
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "TwitchBotApplication v1"));
#endif
            }

            app.UseRouting();
            app.UseAuthorization();
        }

        protected virtual void ConfigureStaticFiles(IApplicationBuilder app, IWebHostEnvironment env)
        {
            ConfigureCustomStaticFilesOverride(app, env);
            ConfigureCoreLibraryContent(app, env);
            ConfigureCustomStaticFilesSupplement(app, env);
        }


        protected virtual void ConfigureCoreMiddleware(IApplicationBuilder app)
        {
            app.UseMiddleware<Web.Middleware.AuthCheckerMiddleware>();
        }

        protected virtual void ConstructCoreSingletons(IServiceProvider serviceProvider)
        {
            //Make sure required services are constructed
            serviceProvider.GetRequiredService<View.IConsoleOutput>();
        }

        protected virtual void ConfigureCoreLibraryContent(
            IApplicationBuilder app,
            IWebHostEnvironment env)
        {
            app.UseDefaultFiles();
            //Include project's wwwroot first
            app.UseStaticFiles();

            //Include TASagentTwitchBot.Core's wwwroot
            UseCoreLibraryContent(app, env, "TASagentTwitchBot.Core");
        }

        protected virtual void UseCoreLibraryContent(
            IApplicationBuilder app,
            IWebHostEnvironment env,
            string libraryName,
            bool useDefault = true)
        {
            string wwwRootPath;

            if (env.IsDevelopment())
            {
                //Navigate relative to the current path in Development
                wwwRootPath = Path.Combine(
                    Directory.GetParent(env.ContentRootPath).FullName,
                    "TASagentTwitchBotCore",
                    libraryName,
                    "wwwroot");
            }
            else
            {
                //Look in published "_content" directory
                wwwRootPath = Path.Combine(env.WebRootPath, "_content", libraryName);
            }

            PhysicalFileProvider fileProvider = new PhysicalFileProvider(wwwRootPath);

            if (useDefault)
            {
                app.UseDefaultFiles(new DefaultFilesOptions
                {
                    FileProvider = fileProvider,
                    RequestPath = ""
                });
            }

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = fileProvider,
                RequestPath = ""
            });
        }

        protected virtual void UseLibraryContent(
            IApplicationBuilder app,
            IWebHostEnvironment env,
            string libraryName,
            bool useDefault = true)
        {
            string wwwRootPath;

            if (env.IsDevelopment())
            {
                //Navigate relative to the current path in Development
                wwwRootPath = Path.Combine(
                    Directory.GetParent(env.ContentRootPath).FullName,
                    libraryName,
                    "wwwroot");
            }
            else
            {
                //Look in published "_content" directory
                wwwRootPath = Path.Combine(env.WebRootPath, "_content", libraryName);
            }

            PhysicalFileProvider fileProvider = new PhysicalFileProvider(wwwRootPath);

            if (useDefault)
            {
                app.UseDefaultFiles(new DefaultFilesOptions
                {
                    FileProvider = fileProvider,
                    RequestPath = ""
                });
            }

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = fileProvider,
                RequestPath = ""
            });
        }
    }
}
