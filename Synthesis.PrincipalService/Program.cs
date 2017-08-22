using System;
using System.Configuration;
using System.Diagnostics;
using System.Threading;
using Microsoft.ServiceFabric.Services.Runtime;
using Synthesis.Owin.Host;
using Synthesis.ApplicationInsights;
using Synthesis.Logging;
using Synthesis.Logging.Log4Net;
using System.Net;
using System.Linq;
using System.Net.Sockets;

namespace Synthesis.PrincipalService
{
    internal static class Program
    {
        /// <summary>
        /// This is the entry point of the service host process.
        /// </summary>
        private static void Main(string[] args)
        {
            var version = typeof(Program).Assembly.GetName().Version.ToString();
            var deploymentName = ConfigurationManager.AppSettings["AI.DeploymentName"];

            ILogLayout logLayout = new LogLayoutBuilder().Use<LogLayoutMetadata>().BuildGlobalLayout();
            LogLayoutMetadata messageContent = logLayout.Get<LogLayoutMetadata>();
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            messageContent.LocalIP = host.AddressList.FirstOrDefault((i) => i.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? string.Empty;
            messageContent.ApplicationName = ConfigurationManager.AppSettings["ApplicationName"];
            messageContent.Environment = ConfigurationManager.AppSettings["Environment"];
            messageContent.Facility = ConfigurationManager.AppSettings["Facility"];
            messageContent.Host = Environment.MachineName;
            messageContent.RemoteIP = string.Empty;
            messageContent.Version = version;
            logLayout.Update(messageContent);

            PrincipalServiceBootstrapper.GlobalLogContent = logLayout;
            PrincipalServiceBootstrapper.LoggerFactory = new LoggerFactory();

            ILogger logger = PrincipalServiceBootstrapper.LoggerFactory.Get(new LogTopic("Synthesis.PrincipalService"));

            if (args == null || args.Length == 0)
            {
                var instrumentationKey = ConfigurationManager.AppSettings["AI.InstrumentationKey"];
                if (!string.IsNullOrWhiteSpace(instrumentationKey))
                {
                    // Creating this initializes the Telemetry Context
                    MicroServiceTelemetryInitializer.Initialize(version, deploymentName, instrumentationKey);
                }

                try
                {
                  // The ServiceManifest.XML file defines one or more service type names.
                  // Registering a service maps a service type name to a .NET type.
                  // When Service Fabric creates an instance of this service type,
                  // an instance of the class is created in this host process.
                  ServiceRuntime.RegisterServiceAsync("PrincipalServiceType",
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
