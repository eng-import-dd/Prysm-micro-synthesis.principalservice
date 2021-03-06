using Microsoft.Owin.Cors;
using Microsoft.Owin.Extensions;
using Owin;
using Synthesis.ApplicationInsights.Owin;
using Synthesis.Owin.Security;
using Synthesis.Nancy.MicroService.Middleware;
using Synthesis.PrincipalService.Owin;
using Synthesis.Tracking.Web;
using System;

namespace Synthesis.PrincipalService
{
    public static class Startup
    {
        // This code configures Web API. The Startup class is specified as a type
        // parameter in the WebApp.Start method.
        public static void ConfigureApp(IAppBuilder app)
        {
            app.UseCors(CorsOptions.AllowAll);

            // Enables IoC for OwinMiddlware implementations. This method allows us to control
            // the order of our middleware.
            app.UseAutofacLifetimeScopeInjector(PrincipalServiceBootstrapper.RootContainer);

            app.UseMiddlewareFromContainer<GlobalExceptionHandlerMiddleware>();
            app.UseMiddlewareFromContainer<ResourceNotFoundMiddleware>();
            app.UseMiddlewareFromContainer<CorrelationScopeMiddleware>();

            // This middleware performs our authentication and populates the user principal.
            app.UseMiddlewareFromContainer<SynthesisAuthenticationMiddleware>();
            app.UseApplicationInsightsTracking();
            app.UseMiddlewareFromContainer<ImpersonateTenantMiddleware>();
            app.UseMiddlewareFromContainer<GuestContextMiddleware>();
            app.UseStageMarker(PipelineStage.Authenticate);

            if (app.Properties.TryGetValue("Microsoft.Owin.Host.HttpListener.OwinHttpListener", out var listener))
            {
                var l = listener as Microsoft.Owin.Host.HttpListener.OwinHttpListener;
                if (l != null)
                {
                    l.SetRequestQueueLimit(500);
                    l.SetRequestProcessingLimits(100 * Environment.ProcessorCount, Int32.MaxValue);
                }
            }

            if (app.Properties.TryGetValue("System.Net.HttpListener", out listener))
            {
                var l = listener as System.Net.HttpListener;
                if (l != null)
                {
                    l.TimeoutManager.IdleConnection = TimeSpan.FromSeconds(30);
                    l.TimeoutManager.RequestQueue = TimeSpan.FromSeconds(5);
                }
            }
            app.UseStageMarker(PipelineStage.MapHandler);
            app.UseNancy(options =>
            {
                options.Bootstrapper = new PrincipalServiceBootstrapper();
            });
        }
    }
}
