using Microsoft.Owin.Cors;
using Microsoft.Owin.Extensions;
using Owin;
using Synthesis.Owin.Security;
using Synthesis.PrincipalService.Owin;
using Synthesis.Tracking.Web;

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
            app.UseMiddlewareFromContainer<CorrelationScopeMiddleware>();

            // This middleware performs our authentication and populates the user principal.
            app.UseMiddlewareFromContainer<SynthesisAuthenticationMiddleware>();
            app.UseMiddlewareFromContainer<ImpersonateTenantMiddleware>();
            app.UseStageMarker(PipelineStage.Authenticate);

            app.UseNancy(options =>
            {
                options.Bootstrapper = new PrincipalServiceBootstrapper();
            });
        }
    }
}
