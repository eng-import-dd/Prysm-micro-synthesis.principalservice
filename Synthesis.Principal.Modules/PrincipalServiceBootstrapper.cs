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
using Synthesis.Configuration.Shared;
using Synthesis.DocumentStorage;
using Synthesis.DocumentStorage.DocumentDB;
using Synthesis.EmailService.InternalApi.Api;
using Synthesis.EventBus;
using Synthesis.EventBus.Kafka.Autofac;
using Synthesis.Http;
using Synthesis.Http.Configuration;
using Synthesis.Http.Microservice;
using Synthesis.IdentityService.InternalApi.Api;
using Synthesis.License.Manager;
using Synthesis.License.Manager.Interfaces;
using Synthesis.Logging;
using Synthesis.Logging.Log4Net;
using Synthesis.Microservice.Health;
using Synthesis.Nancy.MicroService.Authentication;
using Synthesis.Nancy.MicroService.EventBus;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Serialization;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.Owin.Security;
using Synthesis.PolicyEvaluator.Autofac;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.Email;
using Synthesis.PrincipalService.Events;
using Synthesis.PrincipalService.InternalApi.Constants;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Mapper;
using Synthesis.PrincipalService.Modules;
using Synthesis.PrincipalService.Owin;
using Synthesis.ProjectService.InternalApi.Api;
using Synthesis.Serialization.Json;
using Synthesis.TenantService.InternalApi.Api;
using Synthesis.Tracking;
using Synthesis.Tracking.ApplicationInsights;
using Synthesis.Tracking.Web;
using IObjectSerializer = Synthesis.Serialization.IObjectSerializer;
using RequestHeaders = Synthesis.Http.Microservice.RequestHeaders;

namespace Synthesis.PrincipalService
{
    public class PrincipalServiceBootstrapper : AutofacNancyBootstrapper
    {
        private const int RedisConnectRetryTimes = 30;
        private const int RedisConnectTimeoutInMilliseconds = 10 * 1000;
        private const int RedisSyncTimeoutInMilliseconds = 15 * 1000;
        public static readonly LogTopic DefaultLogTopic = new LogTopic(ServiceInformation.ServiceName);
        public static readonly LogTopic EventServiceLogTopic = new LogTopic($"{ServiceInformation.ServiceName}.EventHub");
        private static readonly Lazy<ILifetimeScope> LazyRootContainer = new Lazy<ILifetimeScope>(BuildRootContainer);

        public PrincipalServiceBootstrapper()
        {
            ApplicationContainer = RootContainer.BeginLifetimeScope();
        }

        /// <summary>
        ///     Gets container for this bootstrapper instance.
        /// </summary>
        public new ILifetimeScope ApplicationContainer { get; }

        /// <summary>
        ///     Gets the root injection container for this service.
        /// </summary>
        /// <value>
        ///     The root injection container for this service.
        /// </value>
        public static ILifetimeScope RootContainer => LazyRootContainer.Value;

        /// <inheritdoc />
        protected override Func<ITypeCatalog, NancyInternalConfiguration> InternalConfiguration
        {
            get { return NancyInternalConfiguration.WithOverrides(config => { config.Serializers = new[] { typeof(DefaultXmlSerializer), typeof(SynthesisJsonSerializer) }; }); }
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
                        ContractResolver = new ApiModelContractResolver(),
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
                    bldr.RegisterType<EventServicePublishExtender>()
                        .WithParameter(new ResolvedParameter(
                            (p, c) => p.ParameterType == typeof(IEventService),
                            (p, c) => ApplicationContainer.Resolve<IEventService>()))
                        .As<IEventService>()
                        .InstancePerLifetimeScope();

                    bldr.Register(c => new RequestHeaders(context.Request.Headers))
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

            builder.RegisterType<DefaultAppSettingsReader>()
                .Keyed<IAppSettingsReader>(nameof(DefaultAppSettingsReader));

            builder.RegisterType<SharedAppSettingsReader>()
                .As<IAppSettingsReader>()
                .As<ISharedAppSettingsReader>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "configurationServiceUrl",
                    (p, c) => c.ResolveKeyed<IAppSettingsReader>(nameof(DefaultAppSettingsReader)).GetValue<string>("Configuration.Url")))
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "httpClient",
                    (p, c) => c.ResolveKeyed<IMicroserviceHttpClient>(nameof(ServiceToServiceClient))))
                .SingleInstance();

            RegisterLogging(builder);

            // Tracking
            builder.RegisterType<ApplicationInsightsTrackingService>().As<ITrackingService>();

            // Register our custom OWIN Middleware
            builder.RegisterType<GlobalExceptionHandlerMiddleware>().InstancePerRequest();
            builder.RegisterType<CorrelationScopeMiddleware>().InstancePerRequest();
            builder.RegisterType<SynthesisAuthenticationMiddleware>().InstancePerRequest();
            builder
                .RegisterType<ImpersonateTenantMiddleware>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "tenantUrl",
                    (p, c) => c.Resolve<IAppSettingsReader>().GetValue<string>("Tenant.Url")))
                .InstancePerRequest();

            // DocumentDB registration.
            builder.Register(c =>
            {
                var settings = c.Resolve<IAppSettingsReader>();
                return new DocumentDbContext
                {
                    AuthKey = settings.GetValue<string>("Principal.DocumentDB.AuthKey"),
                    Endpoint = settings.GetValue<string>("Principal.DocumentDB.Endpoint"),
                    DatabaseName = settings.GetValue<string>("Principal.DocumentDB.DatabaseName")
                };
            });
            builder.RegisterType<DocumentDbRepositoryFactory>().As<IRepositoryFactory>().SingleInstance();
            builder.RegisterType<DocumentFileReader>().As<IDocumentFileReader>();
            builder.RegisterType<DefaultDocumentDbConfigurationProvider>().As<IDocumentDbConfigurationProvider>();
            builder.RegisterInstance(Assembly.GetExecutingAssembly());

            builder.Register(c =>
            {
                var reader = c.ResolveKeyed<IAppSettingsReader>(nameof(DefaultAppSettingsReader));
                return new ServiceToServiceClientConfiguration
                {
                    AuthenticationRoute = $"{reader.GetValue<string>("Identity.Url").TrimEnd('/')}/{reader.GetValue<string>("Identity.AccessTokenRoute").TrimStart('/')}",
                    ClientId = reader.GetValue<string>("Principal.Synthesis.ClientId"),
                    ClientSecret = reader.GetValue<string>("Principal.Synthesis.ClientSecret")
                };
            });

            // Certificate provider that provides the JWT validation key to the token validator.
            builder.RegisterType<IdentityServiceCertificateProvider>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "identityUrl",
                    (p, c) => c.ResolveKeyed<IAppSettingsReader>(nameof(DefaultAppSettingsReader)).GetValue<string>("Identity.Url")))
                .As<ICertificateProvider>();

            // Microservice HTTP Clients
            builder.RegisterType<AuthorizationPassThroughClient>()
                .Keyed<IMicroserviceHttpClient>(nameof(AuthorizationPassThroughClient));

            builder.RegisterType<ServiceToServiceClient>()
                .Keyed<IMicroserviceHttpClient>(nameof(ServiceToServiceClient))
                .AsSelf();

            builder.RegisterType<SynthesisHttpClient>()
                .As<IHttpClient>();

            builder.RegisterType<HttpClientConfiguration>()
                .As<IHttpClientConfiguration>();

            // Object serialization
            builder.RegisterType<JsonObjectSerializer>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.ParameterType == typeof(JsonSerializer),
                    (p, c) => new JsonSerializer()))
                .As<IObjectSerializer>();

            // JWT Token Validator
            builder.RegisterType<JwtTokenValidator>()
                .As<ITokenValidator>()
                .SingleInstance();

            // Microservice HTTP client resolver that will select the proper implementation of
            // IMicroserviceHttpClient for calling other microservices.
            builder.RegisterType<MicroserviceHttpClientResolver>()
                .As<IMicroserviceHttpClientResolver>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "passThroughKey",
                    (p, c) => nameof(AuthorizationPassThroughClient)))
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "serviceToServiceKey",
                    (p, c) => nameof(ServiceToServiceClient)));

            // Policy Evaluator components
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
                            Password = reader.GetValue<string>("Redis.General.Key"),
                            AbortOnConnectFail = false,
                            SyncTimeout = RedisSyncTimeoutInMilliseconds,
                            ConnectTimeout = RedisConnectTimeoutInMilliseconds,
                            ConnectRetry = RedisConnectRetryTimes
                        };
                        redisOptions.EndPoints.Add(reader.GetValue<string>("Redis.General.Endpoint"));
                        return ConnectionMultiplexer.Connect(redisOptions);
                    }))
                .As<ICache>()
                .SingleInstance();

            // Validation
            RegisterValidation(builder);

            RegisterEvents(builder);

            RegisterServiceSpecificRegistrations(builder);

            return builder.Build();
        }

        /// <summary>
        ///     The point of this method is to ease updating services.  Any registrations that a service needs can go into this
        ///     method and then when updating to the latest template, this can just be copied forward.
        /// </summary>
        /// <param name="builder"></param>
        private static void RegisterServiceSpecificRegistrations(ContainerBuilder builder)
        {
            // The indexing policy also needs to be included in the documentdb section

            var mapper = new MapperConfiguration(cfg => {
                cfg.AddProfile<UserProfile>();
                cfg.AddProfile<UserInviteProfile>();
            }).CreateMapper();
            builder.RegisterInstance(mapper).As<IMapper>();

            // Controllers
            builder.RegisterType<UsersController>().As<IUsersController>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.Name == "deploymentType",
                    (p, c) => c.Resolve<IAppSettingsReader>().GetValue<string>("Principal.DeploymentType")));
            builder.RegisterType<UserInvitesController>().As<IUserInvitesController>();
            builder.RegisterType<MachinesController>().As<IMachineController>();
            builder.RegisterType<GroupsController>().As<IGroupsController>();

            builder.RegisterType<LicenseApi>().As<ILicenseApi>();
            builder.RegisterType<TenantApi>().As<ITenantApi>();
            builder.RegisterType<TenantDomainApi>().As<ITenantDomainApi>();
            builder.RegisterType<ProjectApi>().As<IProjectApi>();
            builder.RegisterType<IdentityUserApi>().As<IIdentityUserApi>();
            builder.RegisterType<CloudShim>().As<ICloudShim>();
            builder.RegisterType<UserSearchBuilder>().As<IUserSearchBuilder>();
            builder.RegisterType<DocumentDbRepositoryHealthReport>().As<IRepositoryHealthReport>();

            builder.RegisterType<UserQueryRunner>().As<IQueryRunner<User>>();

            builder.RegisterType<RepositoryHealthReporter<User>>().As<IHealthReporter>()
                .SingleInstance()
                .WithParameter("serviceName", ServiceInformation.ServiceNameShort);

            builder.RegisterType<RepositoryHealthReporter<Machine>>().As<IHealthReporter>()
                .SingleInstance()
                .WithParameter("serviceName", ServiceInformation.ServiceNameShort);

            builder.RegisterType<RepositoryHealthReporter<UserInvite>>().As<IHealthReporter>()
                .SingleInstance()
                .WithParameter("serviceName", ServiceInformation.ServiceNameShort);

            builder.RegisterType<RepositoryHealthReporter<Group>>().As<IHealthReporter>()
                .SingleInstance()
                .WithParameter("serviceName", ServiceInformation.ServiceNameShort);

            builder.RegisterType<EmailApi>()
                .WithParameter("serviceUrlSettingName", "Email.Url")
                .As<IEmailApi>();
            builder.RegisterType<EmailSendingService>().As<IEmailSendingService>().InstancePerRequest();
        }

        private static void RegisterLogging(ContainerBuilder builder)
        {
            builder.Register(c =>
            {
                var reader = c.Resolve<IAppSettingsReader>();
                return CreateLogLayout(reader);
            }).AutoActivate();
            var loggerFactory = new LoggerFactory();
            var defaultLogger = loggerFactory.Get(DefaultLogTopic);
            builder.RegisterInstance(defaultLogger);
            builder.RegisterInstance(loggerFactory).As<ILoggerFactory>();
        }

        private static ILogLayout CreateLogLayout(IAppSettingsReader settingsReader)
        {
            var version = typeof(PrincipalServiceBootstrapper).Assembly.GetName().Version.ToString();

            var logLayout = new LogLayoutBuilder().Use<LogLayoutMetadata>().BuildGlobalLayout();
            var localIpHostEntry = Dns.GetHostEntry(Dns.GetHostName());

            var messageContent = logLayout.Get<LogLayoutMetadata>();
            messageContent.LocalIP = localIpHostEntry.AddressList.FirstOrDefault(i => i.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? string.Empty;
            messageContent.ApplicationName = ServiceInformation.ServiceName;
            messageContent.Environment = settingsReader.GetValue<string>("Environment");
            messageContent.Facility = settingsReader.GetValue<string>("Principal.Facility");
            messageContent.Host = Environment.MachineName;
            messageContent.RemoteIP = string.Empty;
            messageContent.Version = version;

            logLayout.Update(messageContent);

            return logLayout;
        }

        private static void RegisterValidation(ContainerBuilder builder)
        {
            builder.RegisterType<ValidatorLocator>().As<IValidatorLocator>();

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

        private static void RegisterEvents(ContainerBuilder builder)
        {
            // Event Service registration.
            builder.RegisterKafkaEventBusComponents(
                ServiceInformation.ServiceName,
                (metadata, bldr) =>
                {
                    bldr.RegisterType<EventServicePublishExtender>()
                        .WithParameter(new ResolvedParameter(
                            (p, c) => p.ParameterType == typeof(IEventService),
                            (p, c) => RootContainer.Resolve<IEventService>()))
                        .As<IEventService>()
                        .InstancePerLifetimeScope();

                    bldr.Register(c => metadata.ToRequestHeaders())
                        .InstancePerRequest();
                });

            builder
                .RegisterType<EventSubscriber>()
                .AsSelf()
                .AutoActivate();

            builder
                .RegisterType<EventHandlerLocator>()
                .As<IEventHandlerLocator>()
                .SingleInstance()
                .AutoActivate();

            // Use reflection to register all the IEventHandlers in the Synthesis.PrincipalService.EventHandlers namespace
            var assembly = Assembly.GetAssembly(typeof(UsersModule));
            var types = assembly.GetTypes().Where(x => string.Equals(x.Namespace, "Synthesis.PrincipalService.EventHandlers", StringComparison.Ordinal)).ToArray();
            foreach (var type in types)
            {
                if (!type.IsAbstract && typeof(IEventHandlerBase).IsAssignableFrom(type))
                {
                    builder.RegisterType(type).AsSelf().As<IEventHandlerBase>();
                }
            }

            // register event service for events to be handled for every instance of this service
            builder.RegisterType<SettingsInvalidateCacheEventHandler>().AsSelf();

            builder.RegisterType<EventHandlerLocator>()
                .WithParameter(new ResolvedParameter(
                    (p, c) => p.ParameterType == typeof(IEventServiceConsumer),
                    (p, c) => c.ResolveKeyed<IEventServiceConsumer>(Registration.PerInstanceEventServiceKey)))
                .OnActivated(args => args.Instance.SubscribeEventHandler<SettingsInvalidateCacheEventHandler>("*", Configuration.Shared.EventNames.SettingsInvalidateCache))
                .Keyed<IEventHandlerLocator>(Registration.PerInstanceEventServiceKey)
                .SingleInstance()
                .AutoActivate();
        }
    }
}
