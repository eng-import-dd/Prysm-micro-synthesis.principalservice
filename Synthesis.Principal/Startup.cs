using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nancy.Owin;
using Synthesis.ApplicationInsights.AspCoreNet;
using Synthesis.AspNetCore.Security.Middleware;
using Synthesis.Nancy.Autofac;
using Synthesis.Nancy.Autofac.Module.Middleware.AspNetCore;
using Synthesis.Tracking.Web;

namespace Synthesis.PrincipalService
{
    public class Startup
    {
        private const string AllowAllOrigins = "AllowAllOrigins";
        public IConfiguration Configuration { get; private set; }

        public ILifetimeScope AutofacContainer { get; private set; }

        public Startup(IConfiguration configuration)
        {
            var builder = new ConfigurationBuilder()
                .AddConfiguration(configuration)
                .AddJsonFile("secrets/principalsettings", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        // ConfigureContainer is where you can register things directly
        // with Autofac. This runs after ConfigureServices so the things
        // here will override registrations made in ConfigureServices.
        // Don't build the container; that gets done for you by the factory.
        public void ConfigureContainer(ContainerBuilder builder)
        {
            builder.RegisterInstance(Configuration).As<IConfiguration>();
            // Register your own things directly with Autofac, like:
            builder.RegisterModule<PrincipalAutofacModule>();
        }
        
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<KestrelServerOptions>(options => { options.AllowSynchronousIO = true; });
            // we should probably restrict this to the most narrow scope possible, but
            // it's setup this way since we are porting existing functionality to get that
            // working.  This will be revisited after we have everything running again.
            services.AddCors(options => options.AddPolicy(AllowAllOrigins, 
                builder => builder.AllowAnyOrigin()));
            services.AddOptions();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            AutofacContainer = app.ApplicationServices.GetAutofacRoot();
            
            app.UseCors(AllowAllOrigins);
            app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
            app.UseMiddleware<CorrelationScopeMiddleware>();
            app.UseMiddleware<SynthesisAuthenticationMiddleware>();
            app.UseApplicationInsightsTracking();
            app.UseMiddleware<ImpersonateTenantMiddleware>();
            app.UseMiddleware<GuestContextMiddleware>();
            app.UseOwin(x => 
                x.UseNancy(opt => 
                    opt.Bootstrapper = new AutofacNancyBootstrapper(AutofacContainer)));
        }
    }
}
