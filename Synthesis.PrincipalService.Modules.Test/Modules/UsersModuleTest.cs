using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.Testing;
using Nancy.TinyIoc;
using Newtonsoft.Json;
using Synthesis.DocumentStorage;
using Synthesis.EventBus;
using Synthesis.License.Manager.Interfaces;
using Synthesis.License.Manager.Models;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Constants;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Serialization;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Mapper;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;
using Synthesis.PrincipalService.Utilities;
using Synthesis.PrincipalService.Workflow.Controllers;
using Synthesis.PrincipalService.Workflow.Exceptions;
using Xunit;
using ClaimTypes = System.Security.Claims.ClaimTypes;

namespace Synthesis.PrincipalService.Modules.Test.Modules
{
    [SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
    public class UsersModuleTest
    {
        private readonly Browser _browserAuth;
        private readonly Browser _browserNoAuth;

        private readonly Mock<IUsersController> _controllerMock = new Mock<IUsersController>();

        public UsersModuleTest()
        {
            _browserAuth = BrowserWithRequestStartup((container, pipelines, context) =>
            {
                context.CurrentUser = new ClaimsPrincipal(
                    new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Name, "TestUser"),
                        new Claim(ClaimTypes.Email, "test@user.com"),
                        new Claim("TenantId" , "DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3"),
                        new Claim("UserId" , "16367A84-65E7-423C-B2A5-5C42F8F1D5F2")
                    },
                    AuthenticationTypes.Basic));
            });
            _browserNoAuth = BrowserWithRequestStartup((container, pipelines, context) =>
            {
            });
        }

        private Browser BrowserWithRequestStartup(Action<TinyIoCContainer, IPipelines, NancyContext> requestStartup)
        {
            return new Browser(with =>
            {
                var mockLogger = new Mock<ILogger>();

                mockLogger.Setup(l => l.LogMessage(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception>(), It.IsAny<IDictionary<string, object>>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>())).Callback(() => Console.Write(""));
                var logger = mockLogger.Object;
                var loggerFactoryMock = new Mock<ILoggerFactory>();
                loggerFactoryMock.Setup(f => f.Get(It.IsAny<LogTopic>())).Returns(logger);

                var loggerFactory = loggerFactoryMock.Object;
                var resource = new User
                {
                    Id = Guid.Parse("2c1156fa-5902-4978-9c3d-ebcb77ae0d55"),
                    CreatedDate = DateTime.UtcNow,
                    LastAccessDate = DateTime.UtcNow
                };
                var repositoryMock = new Mock<IRepository<User>>();
                repositoryMock
                    .Setup(r => r.GetItemAsync(It.IsAny<Guid>()))
                    .ReturnsAsync(resource);

                var repositoryFactoryMock = new Mock<IRepositoryFactory>();
                repositoryFactoryMock
                    .Setup(f => f.CreateRepository<User>())
                    .Returns(repositoryMock.Object);

                var eventServiceMock = new Mock<IEventService>();
                eventServiceMock.Setup(s => s.PublishAsync(It.IsAny<string>()));
                
                var validatorMock = new Mock<IValidator>();
                validatorMock
                    .Setup(v => v.ValidateAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new ValidationResult());
                var validatorLocatorMock = new Mock<IValidatorLocator>();
                validatorLocatorMock
                    .Setup(l => l.GetValidator(It.IsAny<Type>()))
                    .Returns(validatorMock.Object);

                var mapper = new MapperConfiguration(cfg => {
                                                         cfg.AddProfile<UserProfile>();
                                                     }).CreateMapper();

                var mockEmailUtility = new Mock<IEmailUtility>();
                var mockLicenseApi = new Mock<ILicenseApi>();

                with.EnableAutoRegistration();
                with.RequestStartup(requestStartup);
                with.Dependency(new Mock<IMetadataRegistry>().Object);
                with.Dependency(loggerFactory);
                with.Dependency(logger);
                with.Dependency(validatorLocatorMock.Object);
                with.Dependency(repositoryFactoryMock.Object);
                with.Dependency(eventServiceMock.Object);
                with.Dependency(_controllerMock.Object);
                with.Dependency(mockEmailUtility.Object);
                with.Dependency(mockLicenseApi.Object);
                with.Dependency(mapper);
                with.Module<UsersModule>();
                with.Serializer<SynthesisJsonSerializer>();
            });
        }

        [Fact]
        public async Task RespondWithUnauthorizedNoBearerAsync()
        {
            var actual = await _browserNoAuth.Get(
                "/v1/users/2c1156fa-5902-4978-9c3d-ebcb77ae0d55",
                with =>
                {
                    with.Header("Accept", "application/json");
                    with.Header("Content-Type", "application/json");
                    with.HttpRequest();
                });
            Assert.Equal(HttpStatusCode.Unauthorized, actual.StatusCode);
        }

        [Fact]
        public async Task RespondWithOkAsync()
        {
            var actual = await _browserAuth.Get(
                "/v1/users/2c1156fa-5902-4978-9c3d-ebcb77ae0d55",
                with =>
                {
                    with.Header("Accept", "application/json");
                    with.Header("Content-Type", "application/json");
                    with.HttpRequest();
                });
            Assert.Equal(HttpStatusCode.OK, actual.StatusCode);
        }

        #region Create User tests
        
        [Fact]
        public async Task CreateUserReturnsCreatedAsync()
        {
            var actual = await _browserAuth.Post(
                                                 "/v1/users",
                                                 with =>
                                                 {
                                                     with.Header("Accept", "application/json");
                                                     with.Header("Content-Type", "application/json");
                                                     with.HttpRequest();
                                                     with.JsonBody(new CreateUserRequest());
                                                 });
            Assert.Equal(HttpStatusCode.Created, actual.StatusCode);
        }

        [Fact]
        public async Task CreateUserReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(m => m.CreateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new Exception());

            var actual = await _browserAuth.Post(
                                                 "/v1/users",
                                                 with =>
                                                 {
                                                     with.Header("Accept", "application/json");
                                                     with.Header("Content-Type", "application/json");
                                                     with.HttpRequest();
                                                     with.JsonBody(new CreateUserRequest());
                                                 });
            Assert.Equal(HttpStatusCode.InternalServerError, actual.StatusCode);
        }

        [Fact]
        public async Task CreateUserReturnsItemWithInvalidBodyReturnsBadRequest()
        {
            var invalidBody = "{]";

            var actual = await _browserAuth.Post(
                                                 "/v1/users",
                                                 with =>
                                                 {
                                                     with.Header("Accept", "application/json");
                                                     with.Header("Content-Type", "application/json");
                                                     with.HttpRequest();
                                                     with.JsonBody(invalidBody);
                                                 });

            Assert.Equal(HttpStatusCode.BadRequest, actual.StatusCode);
            Assert.Equal(ResponseText.BadRequestBindingException, actual.ReasonPhrase);
        }

        [Fact]
        public async Task CreateUserReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.CreateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new ValidationFailedException(new List<ValidationFailure>()));
            var actual = await _browserAuth.Post(
                                                 "/v1/users",
                                                 with =>
                                                 {
                                                     with.Header("Accept", "application/json");
                                                     with.Header("Content-Type", "application/json");
                                                     with.HttpRequest();
                                                     with.JsonBody(new CreateUserRequest());
                                                 });

            Assert.Equal(HttpStatusCode.BadRequest, actual.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, actual.ReasonPhrase);
        }


        [Fact]
        public async Task CreateUserReadsTenantIdFromUserClaimAsync()
        {
            _controllerMock
                .Setup(uc => uc.CreateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(new UserResponse());

            var actual = await _browserAuth.Post(
                                                "/v1/users",
                                                with =>
                                                {
                                                    with.Header("Accept", "application/json");
                                                    with.Header("Content-Type", "application/json");
                                                    with.HttpRequest();
                                                    with.JsonBody(new CreateUserRequest());
                                                });
            Assert.Equal(HttpStatusCode.Created, actual.StatusCode);
            _controllerMock.Verify(m=>m.CreateUserAsync(It.IsAny<CreateUserRequest>(), Guid.Parse("DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3"), Guid.Parse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2")));
        }
        #endregion

        #region PromoteGuest Tests
        [Fact]
        public async Task PromoteGuestRespondWithUnauthorizedNoBearerAsync()
        {
            var actual = await _browserNoAuth.Post(
                "/v1/users/C3220603-09D9-452B-B204-6CC3946CE1F4/promote",
                with =>
                {
                    with.Header("Accept", "application/json");
                    with.Header("Content-Type", "application/json");
                    with.HttpRequest();
                    with.JsonBody(new PromoteGuestRequest());
                });
            Assert.Equal(HttpStatusCode.Unauthorized, actual.StatusCode);
        }

        [Fact]
        public async Task PromoteGuestRespondWithOkAsync()
        {
            var actual = await _browserAuth.Post(
                "/v1/users/C3220603-09D9-452B-B204-6CC3946CE1F4/promote",
                with =>
                {
                    with.Header("Accept", "application/json");
                    with.Header("Content-Type", "application/json");
                    with.HttpRequest();
                    with.JsonBody(new PromoteGuestRequest());
                });
            Assert.Equal(HttpStatusCode.OK, actual.StatusCode);
        }

        [Fact]
        public async Task PromoteGuestReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock
                .Setup(uc => uc.PromoteGuestUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LicenseType>(), It.IsAny<bool>()))
                           .Throws(new Exception());

            var actual = await _browserAuth.Post(
                                                 "/v1/users/C3220603-09D9-452B-B204-6CC3946CE1F4/promote",
                                                 with =>
                                                 {
                                                     with.Header("Accept", "application/json");
                                                     with.Header("Content-Type", "application/json");
                                                     with.HttpRequest();
                                                     with.JsonBody(new PromoteGuestRequest());
                                                 });
            Assert.Equal(HttpStatusCode.InternalServerError, actual.StatusCode);
        }

        [Fact]
        public async Task PromoteGuestReturnsItemWithInvalidBodyReturnsBadRequest()
        {
            var invalidBody = "{]";

            var actual = await _browserAuth.Post(
                                                 "/v1/users/C3220603-09D9-452B-B204-6CC3946CE1F4/promote",
                                                 with =>
                                                 {
                                                     with.Header("Accept", "application/json");
                                                     with.Header("Content-Type", "application/json");
                                                     with.HttpRequest();
                                                     with.JsonBody(invalidBody);
                                                 });

            Assert.Equal(HttpStatusCode.BadRequest, actual.StatusCode);
            Assert.Equal(ResponseText.BadRequestBindingException, actual.ReasonPhrase);
        }

        [Fact]
        public async Task PromoteGuestReturnsBadRequestIfValidationFails()
        {
            _controllerMock
                .Setup(uc => uc.PromoteGuestUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LicenseType>(), It.IsAny<bool>()))
                           .Throws(new ValidationFailedException(new List<ValidationFailure>()));
            var actual = await _browserAuth.Post(
                                                 "/v1/users/C3220603-09D9-452B-B204-6CC3946CE1F4/promote",
                                                 with =>
                                                 {
                                                     with.Header("Accept", "application/json");
                                                     with.Header("Content-Type", "application/json");
                                                     with.HttpRequest();
                                                     with.JsonBody(new PromoteGuestRequest());
                                                 });

            Assert.Equal(HttpStatusCode.BadRequest, actual.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, actual.ReasonPhrase);
        }

        [Fact]
        public async Task PromoteGuestReturnsForbiddenIfPromotionFails()
        {
            _controllerMock
                .Setup(uc => uc.PromoteGuestUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LicenseType>(), It.IsAny<bool>()))
                .Throws(new PromotionFailedException(""));
            var actual = await _browserAuth.Post(
                                                 "/v1/users/C3220603-09D9-452B-B204-6CC3946CE1F4/promote",
                                                 with =>
                                                 {
                                                     with.Header("Accept", "application/json");
                                                     with.Header("Content-Type", "application/json");
                                                     with.HttpRequest();
                                                     with.JsonBody(new PromoteGuestRequest());
                                                 });

            Assert.Equal(HttpStatusCode.Forbidden, actual.StatusCode);
            Assert.Equal(ResponseReasons.PromotionFailed, actual.ReasonPhrase);
        }

        [Fact]
        public async Task PromoteGuestReturnsForbiddenIfLicenseAssignmentFails()
        {
            _controllerMock
                .Setup(uc => uc.PromoteGuestUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LicenseType>(), It.IsAny<bool>()))
                .Throws(new LicenseAssignmentFailedException("", Guid.NewGuid()));
            var actual = await _browserAuth.Post(
                                                 "/v1/users/C3220603-09D9-452B-B204-6CC3946CE1F4/promote",
                                                 with =>
                                                 {
                                                     with.Header("Accept", "application/json");
                                                     with.Header("Content-Type", "application/json");
                                                     with.HttpRequest();
                                                     with.JsonBody(new PromoteGuestRequest());
                                                 });

            Assert.Equal(HttpStatusCode.Forbidden, actual.StatusCode);
            Assert.Equal(ResponseReasons.LicenseAssignmentFailed, actual.ReasonPhrase);
        }

        [Fact]
        public async Task PromoteGuestReadsTenantIdFromUserClaimAsync()
        {
            _controllerMock
                .Setup(uc => uc.PromoteGuestUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),It.IsAny<LicenseType>(), It.IsAny<bool>()))
                .ReturnsAsync(new PromoteGuestResponse());

            var actual = await _browserAuth.Post(
                                                "/v1/users/C3220603-09D9-452B-B204-6CC3946CE1F4/promote",
                                                with =>
                                                {
                                                    with.Header("Accept", "application/json");
                                                    with.Header("Content-Type", "application/json");
                                                    with.HttpRequest();
                                                    with.JsonBody(new PromoteGuestRequest{LicenseType = LicenseType.UserLicense});
                                                });
            Assert.Equal(HttpStatusCode.OK, actual.StatusCode);
            _controllerMock.Verify(m => m.PromoteGuestUserAsync(Guid.Parse("C3220603-09D9-452B-B204-6CC3946CE1F4"), Guid.Parse("DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3"), LicenseType.UserLicense, false));
        }
        #endregion
    }
}
