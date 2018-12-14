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
                    l.GetRequestProcessingLimits(out int x, out int y);
                    l.SetRequestQueueLimit((256 * Environment.ProcessorCount) * 2);
                    l.SetRequestProcessingLimits(256 * Environment.ProcessorCount, y);
                }
            }

            if (app.Properties.TryGetValue("System.Net.HttpListener", out listener))
            {
                var l = listener as System.Net.HttpListener;
                if (l != null)
                {
                    l.TimeoutManager.IdleConnection = TimeSpan.FromSeconds(45);
                }
            }

            app.UseNancy(options =>
            {
                options.Bootstrapper = new PrincipalServiceBootstrapper();
            });
        }
    }
}
