using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Autofac;
using Autofac.Core;
using Autofac.Core.Lifetime;
using AutoMapper;
using FluentValidation;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.Bootstrappers.Autofac;
using Nancy.Responses;
using Newtonsoft.Json;
using StackExchange.Redis;
using Synthesis.Authentication;
using Synthesis.Authentication.Jwt;
using Synthesis.Cache;
using Synthesis.Cache.Redis;
using Synthesis.Configuration;
using Synthesis.Configuration.Infrastructure;
using Synthesis.DocumentStorage;
using Synthesis.DocumentStorage.DocumentDB;
using Synthesis.EventBus;
using Synthesis.EventBus.Kafka;
using Synthesis.Http;
using Synthesis.Http.Configuration;
using Synthesis.Http.Microservice;
using Synthesis.KeyManager;
using Synthesis.License.Manager;
using Synthesis.License.Manager.Interfaces;
using Synthesis.Logging;
using Synthesis.Logging.Log4Net;
using Synthesis.Nancy.MicroService.Authentication;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Serialization;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PolicyEvaluator.Autofac;
using Synthesis.Serialization.Json;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.Mapper;
using Synthesis.PrincipalService.Modules;
using Synthesis.PrincipalService.Owin;
using Synthesis.PrincipalService.Utilities;
using Synthesis.PrincipalService.Workflow.Controllers;
using Synthesis.Tracking;
using Synthesis.Tracking.ApplicationInsights;
using Synthesis.Tracking.Web;

namespace Synthesis.PrincipalService
{
    public class PrincipalServiceBootstrapper : AutofacNancyBootstrapper
    {
        public const string ServiceName = "Synthesis.PrincipalService";
        public const string AuthorizationPassThroughKey = "AuthorizationPassThrough";
        public const string ServiceToServiceKey = "ServiceToService";
        public static readonly LogTopic DefaultLogTopic = new LogTopic(ServiceName);
        public static readonly LogTopic EventServiceLogTopic = new LogTopic($"{ServiceName}.EventHub");

        private const int RedisSyncTimeoutInMilliseconds = 15 * 1000;
        private const int RedisConnectTimeoutInMilliseconds = 10 * 1000;
        private const int RedisConnectRetryTimes = 30;

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

                // Change the default json serializer to use a different contract resolver
                builder.Register(c =>
                {
                    var serializer = new JsonSerializer
                    {
                        ContractResolver = new SynthesisModelContractResolver(),
                        Formatting = Formatting.None
                    };
                    return serializer;
                });
            });

            container
                .Resolve<ILoggerFactory>()
                .GetLogger(this)
                .Info("PrincipalService Service Running....");
        }

        protected override ILifetimeScope CreateRequestContainer(NancyContext context)
        {
            return ApplicationContainer.BeginLifetimeScope(
                MatchingScopeLifetimeTags.RequestLifetimeScopeTag,
                bldr =>
                {
                    bldr.Register(c => new Http.Microservice.RequestHeaders(context.Request.Headers))
                        .As<IRequestHeaders>()
                        .InstancePerLifetimeScope();
                });
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
                });

            // IEventPublisher
            builder.Register(c => c.Resolve<IEventBus>().CreateEventPublisher());

            builder.Register(c => new EventServiceContext { ServiceName = ServiceName, ConsumerGroup = ServiceName });
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
                    Endpoint = settings.GetValue<string>("DocumentDB.Endpoint"),
                    DatabaseName = settings.GetValue<string>("Principal.DocumentDB.DatabaseName")
                };
            });
            builder.RegisterType<DocumentDbRepositoryFactory>().As<IRepositoryFactory>().SingleInstance();

            builder.Register(c =>
            {
                var reader = c.Resolve<IAppSettingsReader>();
                return new ServiceToServiceClientConfiguration
                {
                    AuthenticationRoute = reader.GetValue<string>("ServiceAuthenticationRoute"),
                    ClientId = reader.GetValue<string>("Principal.ClientId"),
                    ClientSecret = reader.GetValue<string>("Principal.ClientSecret")
                };
            });

            // Key Vault
            builder.RegisterType<KeyVault>().As<IKeyVault>().SingleInstance();
            builder.Register(c =>
            {
                var reader = c.Resolve<IAppSettingsReader>();
                var config = KeyVaultConfiguration.FromApplicationKeyFile();
                var keyVaultClientId = reader.GetValue<string>("KeyVault.ClientId");
                if (!string.IsNullOrEmpty(keyVaultClientId))
                {
                    config.AzureConfiguration = new AzureKeyVaultConfiguration
                    {
                        ClientId = keyVaultClientId,
                        ClientSecret = reader.GetValue<string>("KeyVault.ClientSecret"),
                        SecretBaseUri = reader.GetValue<string>("KeyVault.SecretBaseUri")
                    };
                }
                return config;
            });
            builder.RegisterType<KeyVaultCertificateProvider>()
                .As<ICertificateProvider>();

            // Microservice HTTP Clients
            builder.RegisterType<AuthorizationPassThroughClient>()
                .Keyed<IMicroserviceHttpClient>(AuthorizationPassThroughKey);

            builder.RegisterType<ServiceToServiceClient>()
                .Keyed<IMicroserviceHttpClient>(ServiceToServiceKey)
                .AsSelf();

            builder.RegisterType<SynthesisHttpClient>()
                .As<IHttpClient>();

            builder.RegisterType<HttpClientConfiguration>()
                .As<IHttpClientConfiguration>();

            //Mapper
            var mapper = new MapperConfiguration(cfg => {
                cfg.AddProfile<UserProfile>();
                cfg.AddProfile<UserInviteProfile>();
                cfg.AddProfile<MachineProfile>();
                cfg.AddProfile<UserInviteProfile>();
            }).CreateMapper();
            builder.RegisterInstance(mapper).As<IMapper>();

            // Object serialization
            builder.RegisterType<JsonObjectSerializer>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.ParameterType == typeof(JsonSerializer),
                    (p, c) => new JsonSerializer()))
                .As<Serialization.IObjectSerializer>();

            // JWT Token Validator
            builder.RegisterType<JwtTokenValidator>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.ParameterType == typeof(ICertificateProvider),
                    (p, c) => c.Resolve<ICertificateProvider>(new ResolvedParameter(
                        (p2, c2) => p2.Name == "keyName",
                        (p2, c2) => c2.Resolve<IAppSettingsReader>().GetValue<string>("TokenValidator.CertificateKeyName")))))
                .As<ITokenValidator>()
                .SingleInstance();

                builder.RegisterType<MicroserviceHttpClientResolver>()
                    .As<IMicroserviceHttpClientResolver>()
                    .WithParameter(new ResolvedParameter(
                        (p, c) => p.Name == "passThroughKey",
                        (p, c) => AuthorizationPassThroughKey))
                    .WithParameter(new ResolvedParameter(
                        (p, c) => p.Name == "serviceToServiceKey",
                        (p, c) => ServiceToServiceKey));
               builder.RegisterPolicyEvaluatorComponents();

            // Redis cache
            builder.RegisterType<RedisCache>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.ParameterType == typeof(IConnectionMultiplexer),
                    (p, c) =>
                    {
                        var reader = c.Resolve<IAppSettingsReader>();
                        var redisOptions = new ConfigurationOptions
                        {
                            Password = reader.GetValue<string>("Redis.Key"),
                            AbortOnConnectFail = false,
                            SyncTimeout = RedisSyncTimeoutInMilliseconds,
                            ConnectTimeout = RedisConnectTimeoutInMilliseconds,
                            ConnectRetry = RedisConnectRetryTimes
                        };
                        redisOptions.EndPoints.Add(reader.GetValue<string>("Redis.Endpoint"));
                        return ConnectionMultiplexer.Connect(redisOptions);
                    }))
                .As<ICache>()
                .SingleInstance();

            // Validation
            builder.RegisterType<ValidatorLocator>().As<IValidatorLocator>();
            RegisterValidators(builder);

            // Controllers
            builder.RegisterType<UsersController>().As<IUsersController>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "deploymentType",
                    (p, c) => c.Resolve<IAppSettingsReader>().GetValue<string>("DeploymentType")));
            builder.RegisterType<UserInvitesController>().As<IUserInvitesController>();
            builder.RegisterType<MachinesController>().As<IMachineController>();
            builder.RegisterType<GroupsController>().As<IGroupsController>();

            builder.RegisterType<LicenseApi>().As<ILicenseApi>();
            builder.RegisterType<EmailUtility>().As<IEmailUtility>();
            builder.RegisterType<TenantApi>().As<ITenantApi>();
            builder.RegisterType<ProjectApi>().As<IProjectApi>();
            return builder.Build();
        }

        private static ILogLayout CreateLogLayout(IAppSettingsReader settingsReader)
        {
            var version = typeof(PrincipalServiceBootstrapper).Assembly.GetName().Version.ToString();

            var logLayout = new LogLayoutBuilder().Use<LogLayoutMetadata>().BuildGlobalLayout();
            var localIpHostEntry = Dns.GetHostEntry(Dns.GetHostName());

            var messageContent = logLayout.Get<LogLayoutMetadata>();
            messageContent.LocalIP = localIpHostEntry.AddressList.FirstOrDefault(i => i.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? string.Empty;
            messageContent.ApplicationName = ServiceName;
            messageContent.Environment = settingsReader.GetValue<string>("Principal.Environment");
            messageContent.Facility = settingsReader.GetValue<string>("Principal.Facility");
            messageContent.Host = Environment.MachineName;
            messageContent.RemoteIP = string.Empty;
            messageContent.Version = version;

            logLayout.Update(messageContent);

            return logLayout;
        }

        private static void RegisterValidators(ContainerBuilder builder)
        {
            // Use reflection to register all the IValidators in the Synthesis.PrincipalService.Validators namespace
            var assembly = Assembly.GetAssembly(typeof(UsersModule));
            var types = assembly.GetTypes().Where(x => string.Equals(x.Namespace, "Synthesis.PrincipalService.Validators", StringComparison.Ordinal)).ToArray();
            foreach (var type in types)
            {
                if (!type.IsAbstract && typeof(IValidator).IsAssignableFrom(type))
                {
                    builder.RegisterType(type).AsSelf().As<IValidator>();
                }
            }
        }
    }
}
