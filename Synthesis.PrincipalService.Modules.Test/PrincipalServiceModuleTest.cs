using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Jose;
using log4net.Appender;
using Moq;
using Nancy;
using Nancy.Testing;
using Org.BouncyCastle.Security;
using Synthesis.KeyManager;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Authorization;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Security;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test
{
    [SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
    public class PrincipalServiceModuleTest
    {
        private readonly Browser _browser;
        private IKeyManager _keyManager;

        public PrincipalServiceModuleTest()
        {
            _browser = new Browser(with =>
            {
                var mockKeyManager = new Mock<IKeyManager>();
                var key = new Mock<IKey>();
                key.Setup(k => k.GetContent()).Returns(Encoding.ASCII.GetBytes("This is a test of the emergency broadcast system...."));
                mockKeyManager.Setup(km => km.GetKey("JWT_KEY"))
                    .Returns(key.Object);

                var keyManager = mockKeyManager.Object;
                var mockLogger = new Mock<ILogger>();

                mockLogger.Setup(l => l.LogMessage(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>())).Callback(() => Console.Write(""));
                var logger = mockLogger.Object;
                var loggerFactoryMock = new Mock<ILoggerFactory>();
                loggerFactoryMock.Setup((f) => f.Get(It.IsAny<LogTopic>())).Returns(logger);

                var loggerFactory = loggerFactoryMock.Object;
                var config = new SynthesisStatelessAuthorization(keyManager, logger);

                with.EnableAutoRegistration();
                with.ApplicationStartup((container, pipelines) =>
                {
                    StatelessAuthorization.Enable(pipelines, config);
                });
                with.Dependency(keyManager);
                with.Dependency(new Mock<IMetadataRegistry>().Object); 
                with.Dependency(keyManager);
                with.Dependency(loggerFactory);
                with.Dependency(logger);
                with.Module<PrincipalServiceModule>();
                _keyManager = keyManager;
            });
        }

        [Fact]
        public async void RespondWithUnauthorizedNoBearer()
        {
            var actual = await _browser.Get($"/api/v1/principalservice/hello", with =>
            {
                with.Header("Accept", "application/json");
                with.Header("Content-Type", "application/json");
                with.HttpRequest();
            });
            Assert.Equal(HttpStatusCode.Unauthorized, actual.StatusCode);
        }

        [Fact]
        public async void RespondWithOk()
        {
            var actual = await _browser.Get($"/api/v1/principalservice/hello", with =>
            {
                with.Header("Accept", "application/json");
                with.Header("Content-Type", "application/json");
                with.Header("Authorization", $"Bearer {Token()}");
                with.HttpRequest();
            });
            Assert.Equal(HttpStatusCode.OK, actual.StatusCode);
        }

        private string Token()
        {
            var now = DateTime.Now.Ticks;
            var exp = now + (TimeSpan.TicksPerHour * 8);  // FIXME this should be configurable but is set for 8 hours
            var jti = Convert.ToBase64String(GeneratorUtilities.GetKeyGenerator("AES256").GenerateKey());
            var payload = new Dictionary<string, object>()
                {
                    {"sub", Guid.NewGuid()},
                    {"iat", now },
                    {"jti", jti },
                    {"exp" , exp},
                    {"roles", new string[] {} },
                    {"username", "noone@nowhere.com" },
                    {"account", Guid.NewGuid() },
                    {"permissions", new[] {(int)PermissionEnum.CanLoginToAdminPortal}},
                    {"superadmin", false }
                };
            var secretKey = _keyManager.GetKey("JWT_KEY");
            var token = JWT.Encode(payload, secretKey.GetContent(), JwsAlgorithm.HS256);
            return token;
        }
    }
}