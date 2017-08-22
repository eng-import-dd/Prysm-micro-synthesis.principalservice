using System.Configuration;
using Nancy.Bootstrapper;
using Synthesis.KeyManager;
using Synthesis.Logging;
using Synthesis.Logging.Autofac;
using Synthesis.Nancy.MicroService.Authorization;
using Synthesis.Nancy.MicroService.Dao;
using Synthesis.Nancy.MicroService.Metadata;
using Nancy.Bootstrappers.Autofac;
using Autofac;
using Autofac.Core;
using Synthesis.Configuration;
using Synthesis.Configuration.Infrastructure;
using Synthesis.EventBus;
using Synthesis.EventBus.Kafka;
using Synthesis.PrincipalService.Dao;
using Synthesis.PrincipalService.Validators;
using Synthesis.PrincipalService.Workflow.Controllers;

namespace Synthesis.PrincipalService
{
    public class PrincipalServiceBootstrapper : AutofacNancyBootstrapper
    {
         /// <summary>
        /// Represents object which manages content of the each log message
        /// It is a static field due content is built before creating Nancy app
        /// as result this property is used to trasmit instance of ILogContent to
        /// the Nancy app
        /// </summary>

        public static ILogLayout GlobalLogContent { get; set; }


        /// <summary>
        /// LoggerFactory is initialized at the main method point so 
        /// this property is used to transmit instance of Factory object
        /// </summary>
        public static ILoggerFactory LoggerFactory { get; set; }

        protected override void ConfigureApplicationContainer(ILifetimeScope container)
        {
            var kafkaServer = ConfigurationManager.AppSettings["Kafka.Server"];
            ILogger logger = LoggerFactory.Get(new LogTopic("Synthesis.PrincipalService"));

            base.ConfigureApplicationContainer(container);
            container.Update((builder) =>
            {
                builder.RegisterInstance(GlobalLogContent).As<ILogLayout>().SingleInstance();
                builder.RegisterInstance(LoggerFactory).As<ILoggerFactory>().SingleInstance();
                builder.Register(c => new EnterExitInterceptor(logger));
                builder.Register(c => new SimpleKeyManager(logger)).As<IKeyManager>().SingleInstance().EnableLoggingInterceptors();
                builder.RegisterType<CorrelationInterceptor>();
                builder.RegisterType<SynthesisMonolithicCloudDao>().As<ISynthesisMonolithicCloudDao>().SingleInstance();
                builder.RegisterType<MetadataRegistry>().As<IMetadataRegistry>().SingleInstance();
                builder.RegisterType<ValidatorLocator>().As<IValidatorLocator>();
                builder.RegisterType<PrincipalserviceController>().As<IPrincipalserviceController>();
                builder.RegisterType<RepositoryFactory>().As<IRepositoryFactory>();
                builder.RegisterType<DefaultAppSettingsReader>().As<IAppSettingsReader>();
                
                // Event Bus
                builder.Register(c => EventBus.Kafka.EventBus.Create(kafkaServer).CreateEventPublisher()).As<IEventPublisher>();
                builder.Register(c => new EventServiceContext { ServiceName = "Synthesis.PrincipalService" });
                builder.RegisterType<EventService>().As<IEventService>().SingleInstance()
                       .WithParameter(new ResolvedParameter((p, c) => p.Name == "logger", (p, c) => LoggerFactory.Get(new LogTopic("Synthesis.PrincipalService.EventBus"))));

                // Update this registration if you need to change the authorization implementation.
                builder.Register(c=> new SynthesisStatelessAuthorization(c.Resolve<IKeyManager>(), logger)).As<IStatelessAuthorization>().SingleInstance();
            });

            logger.Info("PrincipalService Service Running....");
        }

        protected override void ApplicationStartup(ILifetimeScope container, IPipelines pipelines)
        {
            // Add the micro-service authorization logic to the Nancy pipeline.
            StatelessAuthorization.Enable(pipelines, container.Resolve<IStatelessAuthorization>());

            base.ApplicationStartup(container, pipelines);
            //
            //            Metric.Config
            //                .WithAllCounters()
            //                .WithHttpEndpoint("http://localhost:9000/metrics/")
            //                .WithInternalMetrics()
            //                .WithNancy(pipelines);
        }
    }
}