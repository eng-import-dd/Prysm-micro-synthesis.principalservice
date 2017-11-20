using Autofac;
using Microsoft.ServiceFabric.Services.Runtime;
using Synthesis.ApplicationInsights;
using Synthesis.Configuration;
using Synthesis.Logging;
using Synthesis.Owin.Host;
using System;
using System.Diagnostics;
using System.Threading;

namespace Synthesis.PrincipalService
{
    internal static class Program
    {
        /// <summary>
        /// This is the entry point of the service host process.
        /// </summary>
        private static void Main(string[] args)
        {
            var rootContainer = PrincipalServiceBootstrapper.RootContainer;
            var settingsReader = rootContainer.Resolve<IAppSettingsReader>();
            var logger = rootContainer.Resolve<ILoggerFactory>().GetLogger(nameof(Program));

            if (args == null || args.Length == 0)
            {
                var instrumentationKey = settingsReader.GetValue<string>("Principal.AI.InstrumentationKey");
                if (!string.IsNullOrWhiteSpace(instrumentationKey))
                {
                    // Initialize the micro-service telemetry context (Application Insights)
                    MicroServiceTelemetryInitializer.Initialize(new MicroServiceTelemetryConfiguration
                    {
                        DeploymentName = settingsReader.GetValue<string>("Principal.AI.DeploymentName"),
                        InstrumentationKey = instrumentationKey,
                        ServiceName = "PrincipalService",
                        ServiceVersion = typeof(Program).Assembly.GetName().Version.ToString(),
                    });
                }

                try
                {
                    // The ServiceManifest.XML file defines one or more service type names.
                    // Registering a service maps a service type name to a .NET type.
                    // When Service Fabric creates an instance of this service type,
                    // an instance of the class is created in this host process.
                    ServiceRuntime.RegisterServiceAsync("PrincipalType",
                        context => new PrincipalService(context)).GetAwaiter().GetResult();

                    logger.Info($"ServiceType is registered. ProcessId: {Process.GetCurrentProcess().Id}, Name: {typeof(PrincipalService).Name}");

                    // Prevents this host process from terminating so services keeps running.
                    Thread.Sleep(Timeout.Infinite);
                }
                catch (Exception e)
                {
                    logger.Error("Service initialization failed.", e);
                    throw;
                }
            }
            else
            {
                if (args.Length < 3)
                {
                    throw new ArgumentException("Command line parameters must be provided for the service name, service base URI, and MacroViewer registration URI.");
                }

                ServiceFabricOwinHost serviceFabricOwinHost = new ServiceFabricOwinHost();
                serviceFabricOwinHost.Start(typeof(Startup), args[0], args[1], args[2]);
            }
        }
    }
}
