using System.Collections.Generic;
using System.Reflection;
using Autofac;
using Autofac.Core;
using AutoMapper;
using Nancy;
using Synthesis.Configuration;
using Synthesis.DocumentStorage;
using Synthesis.EmailService.InternalApi.Api;
using Synthesis.FeatureFlags.Feature.TryNBuy;
using Synthesis.FeatureFlags.Interfaces;
using Synthesis.FeatureFlags.Rollout;
using Synthesis.IdentityService.InternalApi.Api;
using Synthesis.License.Manager;
using Synthesis.License.Manager.Interfaces;
using Synthesis.Microservice.Health;
using Synthesis.Nancy.Autofac.Module.Configuration;
using Synthesis.Nancy.Autofac.Module.DocumentDb;
using Synthesis.Nancy.Autofac.Module.Microservice;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.Email;
using Synthesis.PrincipalService.Events;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Mapper;
using Synthesis.PrincipalService.Modules;
using Synthesis.PrincipalService.Services;
using Synthesis.ProjectService.InternalApi.Api;
using Synthesis.SubscriptionService.InternalApi.Api;
using Synthesis.TenantService.InternalApi.Api;
using Module = Autofac.Module;

namespace Synthesis.PrincipalService
{
    public class PrincipalAutofacModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            var dbConfigDictionary = new Dictionary<string, string>
            {
                {DocumentDbAutofacModule.AuthKey, "Principal.DocumentDb.AuthKey"},
                {DocumentDbAutofacModule.Endpoint, "Principal.DocumentDb.Endpoint"},
                {DocumentDbAutofacModule.DatabaseName, "Principal.DocumentDb.DatabaseName"},
                {DocumentDbAutofacModule.RuThroughput, "Principal.DocumentDb.RuThroughput"}
            };
            builder.RegisterModule<ConfigurationAutofacModule>();
            builder.RegisterModule(new MicroserviceAutofacModule(dbConfigDictionary, 
                ServiceInformation.ServiceName, 
                ServiceInformation.ServiceNameShort,
                Assembly.GetAssembly(GetType())));

            // event subscriber
            builder
                .RegisterType<EventSubscriber>()
                .AsSelf()
                .AutoActivate(); 
            
            // The indexing policy also needs to be included in the documentdb section
            var mapper = new MapperConfiguration(cfg =>
            {
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
            builder.RegisterType<MachinesController>().As<IMachinesController>();
            builder.RegisterType<GroupsController>().As<IGroupsController>();
            builder.RegisterType<SuperAdminService>().As<ISuperAdminService>();

            // Api
            builder.RegisterType<LicenseApi>().As<ILicenseApi>();
            builder.RegisterType<TenantApi>().As<ITenantApi>();
            builder.RegisterType<ProjectAccessApi>().As<IProjectAccessApi>();
            builder.RegisterType<IdentityUserApi>().As<IIdentityUserApi>();
            builder.RegisterType<CloudShim>().As<ICloudShim>();
            builder.RegisterType<EmailApi>()
                .WithParameter("serviceUrlSettingName", "Email.Url")
                .As<IEmailApi>();
            builder.RegisterType<EmailSendingService>().As<IEmailSendingService>().InstancePerRequest();
            builder.RegisterType<SubscriptionApi>()
                .As<ISubscriptionApi>();

            // Health Reporters
            builder.RegisterType<RepositoryHealthReporter<User>>().As<IHealthReporter>()
                .SingleInstance();

            builder.RegisterType<RepositoryHealthReporter<Machine>>().As<IHealthReporter>()
                .SingleInstance();

            builder.RegisterType<RepositoryHealthReporter<UserInvite>>().As<IHealthReporter>()
                .SingleInstance();

            builder.RegisterType<RepositoryHealthReporter<Group>>().As<IHealthReporter>()
                .SingleInstance();

            // other
            builder.RegisterType<TenantUserSearchBuilder>().As<ITenantUserSearchBuilder>();
            builder.RegisterType<UserQueryRunner>().As<IQueryRunner<User>>();

            // nancy modules
            builder.RegisterType<GroupsModule>().As<INancyModule>();
            builder.RegisterType<MachinesModule>().As<INancyModule>();
            builder.RegisterType<PrincipalServiceHealthModule>().As<INancyModule>();
            builder.RegisterType<UserInviteModule>().As<INancyModule>();
            builder.RegisterType<UsersModule>().As<INancyModule>();

            //Feature Flags
            builder.Register(c =>
            {
                var reader = c.Resolve<IAppSettingsReader>();
                return new RolloutConfiguration()
                {
                    ServiceUrl = reader.SafeGetValue<string>("Rollout.Url"),
                    ApiKey = reader.SafeGetValue<string>("Rollout.ApiKey"),
                    Enabled = reader.SafeGetValue("FeatureFlagsEnabled", false)
                };
            });

            builder.Register(c =>
                {
                    var configuration = c.Resolve<RolloutConfiguration>();
                    var rolloutFeatureFlagProvider = new RolloutFeatureFlagProvider(configuration);
                    rolloutFeatureFlagProvider
                        .RegisterFeatureFlag(TryNBuyFeature.GetFlagDefinition())
                        .InitializeAsync().Wait();
                    return rolloutFeatureFlagProvider;
                })
                .As<IFeatureFlagProvider>()
                .SingleInstance();
        }
    }
}