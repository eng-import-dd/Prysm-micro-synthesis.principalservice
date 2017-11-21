using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Moq;
using Nancy.Testing;
using Synthesis.Authentication;
using Synthesis.Authentication.Jwt;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Modules;
using Synthesis.PolicyEvaluator;
using ClaimTypes = Synthesis.Authentication.Jwt.ClaimTypes;

namespace Synthesis.PrincipalService.Modules.Test.Modules
{
    public abstract class BaseModuleTests<TModule> where TModule : SynthesisModule
    {
        protected Guid TenantId = Guid.NewGuid();
        protected Guid PrincipalId = Guid.NewGuid();

        private readonly Mock<IMetadataRegistry> _metadataRegistryMock = new Mock<IMetadataRegistry>();
        private readonly Mock<ITokenValidator> _tokenValidatorMock = new Mock<ITokenValidator>();
        private readonly Mock<ILoggerFactory> _loggerFactoryMock = new Mock<ILoggerFactory>();

        protected BaseModuleTests()
        {
            _loggerFactoryMock
                .Setup(x => x.Get(It.IsAny<LogTopic>()))
                .Returns(new Mock<ILogger>().Object);

            _metadataRegistryMock
                .Setup(x => x.GetRouteMetadata(It.IsAny<string>()))
                .Returns<string>(name => new SynthesisRouteMetadata(null, null, name));
        }

        protected abstract List<object> BrowserDependencies { get; }

        protected Browser UserTokenBrowser => GetBrowser();
        protected Browser ServiceTokenBrowser => GetBrowser(AuthenticatedAs.Service);
        protected Browser UnauthenticatedBrowser => GetBrowser(AuthenticatedAs.None);

        protected Mock<IPolicyEvaluator> PolicyEvaluatorMock { get; } = new Mock<IPolicyEvaluator>();

        protected Browser GetBrowser(AuthenticatedAs authenticatedAs = AuthenticatedAs.User)
        {
            return GetBrowser(BrowserDependencies, authenticatedAs);
        }

        protected Browser GetBrowser(List<object> dependencies, AuthenticatedAs authenticatedAs = AuthenticatedAs.User)
        {
            return new Browser(with =>
            {
                if (authenticatedAs != AuthenticatedAs.None)
                {
                    with.RequestStartup((container, pipelines, context) =>
                    {
                        var claims = new[]
                        {
                            new Claim(JwtRegisteredClaimNames.Aud, authenticatedAs == AuthenticatedAs.Service ? Audiences.SynthesisMicroservice : Audiences.Synthesis),
                            new Claim(JwtRegisteredClaimNames.Sub, PrincipalId.ToString()),
                            new Claim(ClaimTypes.Tenant, TenantId.ToString()),
                            new Claim(ClaimTypes.Groups, string.Join(",", Guid.NewGuid().ToString(), Guid.NewGuid().ToString()))
                        };
                        var identity = new ClaimsIdentity(
                            claims,
                            AuthenticationTypes.Basic);
                        context.CurrentUser = new ClaimsPrincipal(identity);
                    });
                }

                foreach (var dependency in dependencies)
                {
                    with.Dependency(dependency);
                }

                with.Dependency(_metadataRegistryMock.Object);
                with.Dependency(_tokenValidatorMock.Object);
                with.Dependency(PolicyEvaluatorMock.Object);
                with.Dependency(_loggerFactoryMock.Object);

                with.Module<TModule>();
            });
        }
        protected enum AuthenticatedAs
        {
            User,
            Service,
            None
        }

        protected void BuildRequest(BrowserContext context)
        {
            context.HttpRequest();
            context.Header("Accept", "application/json");
            context.Header("Content-Type", "application/json");
        }

        protected void BuildRequest<T>(BrowserContext context, T body)
        {
            context.HttpRequest();
            context.Header("Accept", "application/json");
            context.Header("Content-Type", "application/json");
            context.JsonBody(body);
        }
    }
}