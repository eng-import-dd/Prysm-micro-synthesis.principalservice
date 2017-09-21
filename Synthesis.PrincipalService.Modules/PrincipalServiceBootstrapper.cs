using Autofac;
using Autofac.Core;
using AutoMapper;
using FluentValidation;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.Bootstrappers.Autofac;
using Nancy.Responses;
using Newtonsoft.Json;
using Synthesis.Configuration;
using Synthesis.Configuration.Infrastructure;
using Synthesis.DocumentStorage;
using Synthesis.DocumentStorage.DocumentDB;
using Synthesis.EventBus;
using Synthesis.EventBus.Kafka;
using Synthesis.Http;
using Synthesis.Http.Configuration;
using Synthesis.KeyManager;
using Synthesis.License.Manager;
using Synthesis.License.Manager.Interfaces;
using Synthesis.Logging;
using Synthesis.Logging.Log4Net;
using Synthesis.Nancy.MicroService.Authorization;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Serialization;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Configurations;
using Synthesis.PrincipalService.Mapper;
using Synthesis.PrincipalService.Owin;
using Synthesis.PrincipalService.Validators;
using Synthesis.PrincipalService.Workflow.Controllers;
using Synthesis.Tracking;
using Synthesis.Tracking.ApplicationInsights;
using Synthesis.Tracking.Web;
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using Synthesis.PrincipalService.Utilities;

namespace Synthesis.PrincipalService
{
    public class PrincipalServiceBootstrapper : AutofacNancyBootstrapper
    {
        public static readonly LogTopic DefaultLogTopic = new LogTopic("Synthesis.PrincipalService");
        public static readonly LogTopic EventServiceLogTopic = new LogTopic("Synthesis.PrincipalService.EventHub");

        private static readonly Lazy<ILifetimeScope> LazyRootContainer = new Lazy<ILifetimeScope>(BuildRootContainer);

        public PrincipalServiceBootstrapper()
        {
            ApplicationContainer = RootContainer.BeginLifetimeScope();
        }

        /// <summary>
        /// Gets the root injection container for this service.
        /// </summary>
        /// <value>
        /// The root injection container for this service.
        /// </value>
        public static ILifetimeScope RootContainer => LazyRootContainer.Value;

        /// <summary>
        /// Gets container for this bootstrapper instance.
        /// </summary>
        public new ILifetimeScope ApplicationContainer { get; }

        /// <summary>
        /// Gets a logger using the default log topic for this service.
        /// </summary>
        public ILogger GetDefaultLogger()
        {
            return ApplicationContainer.Resolve<ILogger>();
        }

        /// <inheritdoc />
        protected override Func<ITypeCatalog, NancyInternalConfiguration> InternalConfiguration
        {
            get
            {
                return NancyInternalConfiguration.WithOverrides(config =>
                                                                {
                                                                    config.Serializers = new[] { typeof(DefaultXmlSerializer), typeof(SynthesisJsonSerializer) };
                                                                });
            }
        }

        protected override void ConfigureApplicationContainer(ILifetimeScope container)
        {
            base.ConfigureApplicationContainer(container);

            container.Update(builder =>
            {
                builder.RegisterType<MetadataRegistry>().As<IMetadataRegistry>().SingleInstance();

                // Update this registration if you need to change the authorization implementation.
                builder.Register(c => new SynthesisStatelessAuthorization(c.Resolve<IKeyManager>(), c.Resolve<ILogger>()))
                    .As<IStatelessAuthorization>()
                    .SingleInstance();

                // Change the default json serializer to use a different contract resolver
                builder.Register(c =>
                                 {
                                     var serializer = new JsonSerializer()
                                     {
                                         ContractResolver = new SynthesisModelContractResolver(),
                                         Formatting = Formatting.None
                                     };
                                     return serializer;
                                 });
            });

            container.Resolve<ILogger>().Info("PrincipalService Service Running....");
        }

        protected override void ApplicationStartup(ILifetimeScope container, IPipelines pipelines)
        {
            // Add the micro-service authorization logic to the Nancy pipeline.
            pipelines.BeforeRequest += ctx =>
            {
                // TODO: This is temporary until we get JWT implemented.
                var identity = new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.Name, "Test User"),
                        new Claim(ClaimTypes.Email, "test@user.com"),
                        new Claim("TenantId" , "DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3"),
                        new Claim("UserId" , "16367A84-65E7-423C-B2A5-5C42F8F1D5F2"),
                        new Claim("IsGuest","false"),
                        new Claim("GuestProjectId","45411E97-03D4-4449-9EFE-552EA42C35C7")
                    },
                    AuthenticationTypes.Basic);
                ctx.CurrentUser = new ClaimsPrincipal(identity);
                return null;
            };

            base.ApplicationStartup(container, pipelines);
            //
            //            Metric.Config
            //                .WithAllCounters()
            //                .WithHttpEndpoint("http://localhost:9000/metrics/")
            //                .WithInternalMetrics()
            //                .WithNancy(pipelines);
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ApplicationContainer.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <inheritdoc />
        protected override ILifetimeScope GetApplicationContainer()
        {
            return ApplicationContainer;
        }

        private static ILifetimeScope BuildRootContainer()
        {
            var builder = new ContainerBuilder();

            var settingsReader = new DefaultAppSettingsReader();
            var loggerFactory = new LoggerFactory();
            var defaultLogger = loggerFactory.Get(DefaultLogTopic);

            builder.RegisterInstance(settingsReader).As<IAppSettingsReader>();

            // Logging
            builder.RegisterInstance(CreateLogLayout(settingsReader));
            builder.RegisterInstance(defaultLogger);
            builder.RegisterInstance(loggerFactory).As<ILoggerFactory>();

            // Tracking
            builder.RegisterType<ApplicationInsightsTrackingService>().As<ITrackingService>();

            // Register our custom OWIN Middleware
            builder.RegisterType<GlobalExceptionHandlerMiddleware>().InstancePerRequest();
            builder.RegisterType<CorrelationScopeMiddleware>().InstancePerRequest();

            // Event Service registration.
            builder.Register(
                c =>
                {
                    var connectionString = c.Resolve<IAppSettingsReader>().GetValue<string>("Kafka.Server");
                    return EventBus.Kafka.EventBus.Create(connectionString);
                })
                .As<IEventBus>();
            builder.Register(c => new EventServiceContext { ServiceName = "Synthesis.PrincipalService", ConsumerGroup = "Synthesis.PrincipalService" });
            builder.RegisterType<EventService>().As<IEventService>().SingleInstance()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "logger",
                    (p, c) => c.Resolve<ILoggerFactory>().Get(EventServiceLogTopic)));

            // DocumentDB registration.
            builder.Register(c =>
            {
                var settings = c.Resolve<IAppSettingsReader>();
                return new DocumentDbContext
                {
                    AuthKey = settings.GetValue<string>("DocumentDB.AuthKey"),
                    DatabaseName = settings.GetValue<string>("DocumentDB.DatabaseName"),
                    Endpoint = settings.GetValue<string>("DocumentDB.Endpoint"),
                };
            });
            builder.RegisterType<DocumentDbRepositoryFactory>().As<IRepositoryFactory>().SingleInstance();

            // Key Manager
            builder.RegisterType<SimpleKeyManager>().As<IKeyManager>().SingleInstance();

            //HttpClient
            builder.RegisterType<HttpClientConfiguration>().As<IHttpClientConfiguration>().SingleInstance();
            builder.RegisterType<SynthesisHttpClient>().As<IHttpClient>().SingleInstance();

            //Mapper
            var mapper = new MapperConfiguration(cfg => {
                                                     cfg.AddProfile<UserProfile>();
                cfg.AddProfile<UserInviteProfile>();
                                                 }).CreateMapper();
            builder.RegisterInstance(mapper).As<IMapper>();

            // Validation
            builder.RegisterType<ValidatorLocator>().As<IValidatorLocator>();
            // Individual validators must be registered here (as they are below)
            builder.RegisterType<CreateUserRequestValidator>().AsSelf().As<IValidator>();
            builder.RegisterType<UpdateUserRequestValidator>().AsSelf().As<IValidator>();
            builder.RegisterType<UserIdValidator>().AsSelf().As<IValidator>();

            builder.RegisterType<CreateGroupRequestValidator>().AsSelf().As<IValidator>();
            builder.RegisterType<GroupIdValidator>().AsSelf().As<IValidator>();

            // Controllers
            builder.RegisterType<UsersController>().As<IUsersController>()
                   .WithParameter(new ResolvedParameter(
                                                        (p, c) => p.Name == "deploymentType",
                                                        (p, c) => c.Resolve<IAppSettingsReader>().GetValue<string>("DeploymentType")));
            builder.RegisterType<UserInvitesController>().As<IUserInvitesController>();


            builder.RegisterType<GroupsController>().As<IGroupsController>();
            builder.RegisterType<LicenseApi>().As<ILicenseApi>();
            builder.RegisterType<EmailUtility>().As<IEmailUtility>();

            return builder.Build();
        }

        private static ILogLayout CreateLogLayout(IAppSettingsReader settingsReader)
        {
            var version = typeof(PrincipalServiceBootstrapper).Assembly.GetName().Version.ToString();

            var logLayout = new LogLayoutBuilder().Use<LogLayoutMetadata>().BuildGlobalLayout();
            var localIpHostEntry = Dns.GetHostEntry(Dns.GetHostName());

            var messageContent = logLayout.Get<LogLayoutMetadata>();
            messageContent.LocalIP = localIpHostEntry.AddressList.FirstOrDefault(i => i.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? string.Empty;
            messageContent.ApplicationName = settingsReader.GetValue<string>("ServiceName");
            messageContent.Environment = settingsReader.GetValue<string>("Environment");
            messageContent.Facility = settingsReader.GetValue<string>("Facility");
            messageContent.Host = Environment.MachineName;
            messageContent.RemoteIP = string.Empty;
            messageContent.Version = version;

            logLayout.Update(messageContent);

            return logLayout;
        }
    }
}
