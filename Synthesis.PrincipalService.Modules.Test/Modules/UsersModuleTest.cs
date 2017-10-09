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
using Synthesis.Nancy.MicroService;
using ClaimTypes = System.IdentityModel.Claims.ClaimTypes;
using Synthesis.PrincipalService.Entity;

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
        #region Create User Response Test  Cases
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

        #region GetUserByIdBasic Response Test Cases
        [Fact]
        public async Task GetUserByIdBasicReturnsOk()
        {
            _controllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult(new UserResponse()));

            var validUserId = Guid.NewGuid();
            var response = await _browserAuth.Get($"/api/v1/users/{validUserId}/basic", with =>
             {
                 with.HttpRequest();
                 with.Header("Accept", "application/json");
                 with.Header("Content-Type", "application/json");
             });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        [Fact]
        public async Task GetUserByIdBasicReturnsBadRequest()
        {
            _controllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var validUserId = Guid.NewGuid();
            var response = await _browserAuth.Get($"/api/v1/users/{validUserId}/basic", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.Header("Content-Type", "application/json");
            });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        [Fact]
        public async Task GetUserByIdBasicReturnsUnauthorized()
        {
            _controllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult(new UserResponse()));

            var validUserId = Guid.NewGuid();
            var response = await _browserNoAuth.Get($"/api/v1/users/{validUserId}/basic", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.Header("Content-Type", "application/json");
            });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        [Fact]
        public async Task GetUserByIdBasicReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                .Throws(new Exception());

            var validUserId = Guid.NewGuid();
            var response = await _browserAuth.Get($"/api/v1/users/{validUserId}/basic", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.Header("Content-Type", "application/json");
            });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        [Fact]
        public async Task GetUserByIdBasicReturnsNotFoundIfItemDoesNotExist()
        {
            _controllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            var validUserId = Guid.NewGuid();
            var response = await _browserAuth.Get($"v1/users/{validUserId}/basic", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.Header("Content-Type", "application/json");
            });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        #endregion

        #region GetUsersForAccount Response Test Cases
        [Fact]
        public async Task GetUsersForAccountReturnsOk()
        {
            _controllerMock.Setup(m => m.GetUsersForAccountAsync(It.IsAny<GetUsersParams>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(new PagingMetadata<UserResponse>()));

            var response = await _browserAuth.Get($"/api/v1/users/", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.Header("Content-Type", "application/json");
            });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        [Fact]
        public async Task GetUsersForAccountReturnsNotFound()
        {
            _controllerMock.Setup(m => m.GetUsersForAccountAsync(It.IsAny<GetUsersParams>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            var response = await _browserAuth.Get($"/api/v1/users/", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.Header("Content-Type", "application/json");
            });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        [Fact]
        public async Task GetUsersForAccountReturnsBadRequest()
        {
            _controllerMock.Setup(m => m.GetUsersForAccountAsync(It.IsAny<GetUsersParams>(),It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await _browserAuth.Get($"/api/v1/users/", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.Header("Content-Type", "application/json");
            });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        [Fact]
        public async Task GetUsersForAccountReturnsUnauthorized()
        {
            _controllerMock.Setup(m => m.GetUsersForAccountAsync(It.IsAny<GetUsersParams>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(new PagingMetadata<UserResponse>()));

            var response = await _browserNoAuth.Get($"/api/v1/users/", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.Header("Content-Type", "application/json");
            });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        [Fact]
        public async Task GetUsersForAccountReturnsInternalError()
        {
            _controllerMock.Setup(m => m.GetUsersForAccountAsync(It.IsAny<GetUsersParams>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new Exception());

            var response = await _browserAuth.Get($"/api/v1/users/", with =>
            {
                with.HttpRequest();
                with.Header("Accept", "application/json");
                with.Header("Content-Type", "application/json");
            });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
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

        #region Update User Async Response Test Cases

        [Fact]
        public async Task UpdateUserReturnsOk()
        {
            var actual = await _browserAuth.Put(
                                                 "/v1/users/5b5d1d1a-ecab-4074-b06a-adac80e4980b",
                                                 with =>
                                                 {
                                                     with.Header("Accept", "application/json");
                                                     with.Header("Content-Type", "application/json");
                                                     with.HttpRequest();
                                                     with.JsonBody(new User());
                                                 });
            Assert.Equal(HttpStatusCode.OK, actual.StatusCode);

        }


        [Fact]
        public async Task UpdateUserReturnsUnauthorized()
        {
            var actual = await _browserNoAuth.Put(
                                                "/v1/users/5b5d1d1a-ecab-4074-b06a-adac80e4980b",
                                                with =>
                                                {
                                                    with.Header("Accept", "application/json");
                                                    with.Header("Content-Type", "application/json");
                                                    with.HttpRequest();
                                                    with.JsonBody(new User());
                                                });
            Assert.Equal(HttpStatusCode.Unauthorized, actual.StatusCode);

        }

        [Fact]
        public async Task UpdateUserReturnsNotFound()
        {
            var actual = await _browserAuth.Put(
                                                  "/v1/users/somestring",
                                                  with =>
                                                  {
                                                      with.Header("Accept", "application/json");
                                                      with.Header("Content-Type", "application/json");
                                                      with.HttpRequest();
                                                      with.JsonBody(new User());
                                                  });
            Assert.Equal(HttpStatusCode.NotFound, actual.StatusCode);

        }

        [Fact]
        public async Task UpdateUserReturnsBadRequestDueToBindingException()
        {
            
            var actual = await _browserAuth.Put(
                                                "/v1/users/5b5d1d1a-ecab-4074-b06a-adac80e4980b",
                                                with =>
                                                {
                                                    with.Header("Accept", "application/json");
                                                    with.Header("Content-Type", "application/json");
                                                    with.HttpRequest();
                                                    with.JsonBody("[}");
                                                });
            Assert.Equal(HttpStatusCode.BadRequest, actual.StatusCode);

        }

        [Fact]
        public async Task UpdateUserReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.UpdateUserAsync(It.IsAny<Guid>(), It.IsAny<UpdateUserRequest>()))
                           .Throws(new Exception());
            var actual = await _browserAuth.Put(
                                                "/v1/users/5b5d1d1a-ecab-4074-b06a-adac80e4980b",
                                                with =>
                                                {
                                                    with.Header("Accept", "application/json");
                                                    with.Header("Content-Type", "application/json");
                                                    with.HttpRequest();
                                                    with.JsonBody(new UpdateUserRequest());
                                                });
            Assert.Equal(HttpStatusCode.InternalServerError, actual.StatusCode);

        }
        #endregion

        #region Lock User Response Test Cases
        [Fact]
        public async Task LockUserReturnsSuccess()
        {
            _controllerMock
                .Setup(uc => uc.LockOrUnlockUserAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .ReturnsAsync(true);

            var actual = await _browserAuth.Post(
                                                 "/v1/users/f629f87c-366d-4790-ac34-964e3558bdcd/lock",
                                                 with =>
                                                 {
                                                     with.Header("Accept", "application/json");
                                                     with.Header("Content-Type", "application/json");
                                                     with.HttpRequest();
                                                     with.JsonBody(new User() { IsLocked = true });
                                                 });
            Assert.Equal(HttpStatusCode.OK, actual.StatusCode);
            //_controllerMock.Verify(m => m.LockUserAsync(Guid.Parse("f629f87c-366d-4790-ac34-964e3558bdcd"),true));
        }
        [Fact]
        public async Task LockUserReturnsBadRequest()
        {
            _controllerMock
                .Setup(uc => uc.LockOrUnlockUserAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .ReturnsAsync(true);

            var actual = await _browserAuth.Post(
                                                 "/v1/users/f629f87c-366d-4790-ac34-964e3558bdcd/lock",
                                                 with =>
                                                 {
                                                     with.Header("Accept", "application/json");
                                                     with.Header("Content-Type", "application/json");
                                                     with.HttpRequest();
                                                     with.JsonBody("{]");
                                                 });
            Assert.Equal(HttpStatusCode.BadRequest, actual.StatusCode);
            //_controllerMock.Verify(m => m.LockUserAsync(Guid.Parse("f629f87c-366d-4790-ac34-964e3558bdcd"),true));
        }
        [Fact]
        public async Task LockUserReturnsValidationException()
        {
            _controllerMock
                .Setup(uc => uc.LockOrUnlockUserAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var actual = await _browserAuth.Post(
                                                 "/v1/users/f629f87c-366d-4790-ac34-964e3558bdcd/lock",
                                                 with =>
                                                 {
                                                     with.Header("Accept", "application/json");
                                                     with.Header("Content-Type", "application/json");
                                                     with.HttpRequest();
                                                     with.JsonBody(new User());
                                                 });
            Assert.Equal(HttpStatusCode.BadRequest, actual.StatusCode);
        }

        [Fact]
        public async Task LockUserReturnsInternalServerError()
        {
            _controllerMock
                .Setup(uc => uc.LockOrUnlockUserAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .Throws(new Exception());

            var actual = await _browserAuth.Post(
                                                 "/v1/users/f629f87c-366d-4790-ac34-964e3558bdcd/lock",
                                                 with =>
                                                 {
                                                     with.Header("Accept", "application/json");
                                                     with.Header("Content-Type", "application/json");
                                                     with.HttpRequest();
                                                     with.JsonBody(new User());
                                                 });
            Assert.Equal(HttpStatusCode.InternalServerError, actual.StatusCode);
        }
        #endregion

        #region Can Promote user Test cases
        [Fact]
        public async Task CanPromoteuserReturnsSuccess()
        {
            _controllerMock.Setup(m => m.CanPromoteUserAsync(It.IsAny<string>()))
                           .Returns(Task.FromResult(new CanPromoteUserResponse()));
            var response = await _browserAuth.Get($"api/v1/users/canpromoteuser", with =>
                                                                     {
                                                                         with.HttpRequest();
                                                                         with.Header("Accept", "application/json");
                                                                         with.Header("Content-Type", "application/json");
                                                                     });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CanPromoteuserReturnsBadrequest()
        {
            _controllerMock.Setup(m => m.CanPromoteUserAsync(It.IsAny<string>()))
                           .Throws(new ValidationException(new List<ValidationFailure>()));
            var response = await _browserAuth.Get($"api/v1/users/canpromoteuser", with =>
                                                                                  {
                                                                                      with.HttpRequest();
                                                                                      with.Header("Accept", "application/json");
                                                                                      with.Header("Content-Type", "application/json");
                                                                                  });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CanPromoteuserReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.CanPromoteUserAsync(It.IsAny<string>()))
                           .Throws(new Exception());
            var response = await _browserAuth.Get($"api/v1/users/canpromoteuser", with =>
                                                                                  {
                                                                                      with.HttpRequest();
                                                                                      with.Header("Accept", "application/json");
                                                                                      with.Header("Content-Type", "application/json");
                                                                                  });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        } 
        #endregion

        #region User Groups Test Cases

        [Fact]
        public async Task CreateUserGroupReturnsCreated()
        {
            var actual = await _browserAuth.Post("/v1/usergroups",
                                        with =>
                                        {
                                            with.Header("Accept", "application/json");
                                            with.Header("Content-Type", "application/json");
                                            with.HttpRequest();
                                            with.JsonBody(new CreateUserGroupRequest());
                                        });
            Assert.Equal(HttpStatusCode.Created, actual.StatusCode);
        }

        [Fact]
        public async Task CreateUserGroupReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
           _controllerMock.Setup(m => m.CreateUserGroupAsync(It.IsAny<CreateUserGroupRequest>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new Exception());

            var actual = await _browserAuth.Post("/v1/usergroups",
                                        with =>
                                        {
                                            with.Header("Accept", "application/json");
                                            with.Header("Content-Type", "application/json");
                                            with.HttpRequest();
                                            with.JsonBody(new CreateUserGroupRequest());
                                        });
            Assert.Equal(HttpStatusCode.InternalServerError, actual.StatusCode);
        }

        [Fact]
        public async Task CreateUserGroupReturnsItemWithInvalidBodyReturnsBadRequest()
        {
            var invalidBody = "{]";

            var actual = await _browserAuth.Post("/v1/usergroups",
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
        public async Task CreateUserGroupReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.CreateUserGroupAsync(It.IsAny<CreateUserGroupRequest>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new ValidationFailedException(new List<ValidationFailure>()));
            var actual = await _browserAuth.Post("/v1/usergroups",
                                        with =>
                                        {
                                            with.Header("Accept", "application/json");
                                            with.Header("Content-Type", "application/json");
                                            with.HttpRequest();
                                            with.JsonBody(new CreateUserGroupRequest());
                                        });

            Assert.Equal(HttpStatusCode.BadRequest, actual.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, actual.ReasonPhrase);
        }
        
        #endregion

        [Fact]
        public async Task GetGuestUsersForTenantSuccess()
        {
            var actual = await _browserAuth.Get(
                                                "/v1/users/guests",
                                                with =>
                                                {
                                                    with.Header("Accept", "application/json");
                                                    with.Header("Content-Type", "application/json");
                                                    with.HttpRequest();
                                                });
            Assert.Equal(HttpStatusCode.OK, actual.StatusCode);
        }

        [Fact]
        public async Task GetGuestUsersForTenantReturnsUnauthorized()
        {
            var actual = await _browserNoAuth.Get(
                                                "/v1/users/guests",
                                                with =>
                                                {
                                                    with.Header("Accept", "application/json");
                                                    with.Header("Content-Type", "application/json");
                                                    with.HttpRequest();
                                                });
            Assert.Equal(HttpStatusCode.Unauthorized, actual.StatusCode);
        }

        [Fact]
        public async Task GetGuestUsersForTenantReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.GetGuestUsersForTenantAsync(It.IsAny<Guid>(), It.IsAny<GetUsersParams>()))
                           .ThrowsAsync(new Exception());
            var actual = await _browserAuth.Get(
                                                  "/v1/users/guests",
                                                  with =>
                                                  {
                                                      with.Header("Accept", "application/json");
                                                      with.Header("Content-Type", "application/json");
                                                      with.HttpRequest();
                                                  });
            Assert.Equal(HttpStatusCode.InternalServerError, actual.StatusCode);
        }

        [Fact]
        public async Task AutoProvisionRefreshGroupsReturnUser()
        {
            var actual = await _browserAuth.Post("/v1/users/autoprovisionrefreshgroups",
                with =>
                {
                    with.Header("Accept", "application/json");
                    with.Header("Content-Type", "application/json");
                    with.HttpRequest();
                    with.JsonBody(new IdpUserRequest());
                });
            Assert.Equal(HttpStatusCode.OK, actual.StatusCode);
        }

        [Fact]
        public async Task AutoProvisionRefreshGroupsReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(m => m.AutoProvisionRefreshGroups(It.IsAny<IdpUserRequest>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new Exception());

            var response = await _browserAuth.Post("/v1/users/autoprovisionrefreshgroups", 
                with =>
                {
                    with.HttpRequest();
                    with.Header("Accept", "application/json");
                    with.Header("Content-Type", "application/json");
                    with.JsonBody(new IdpUserRequest());
                });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task AutoProvisionRefreshGroupsWithInvalidBodyReturnsBadRequest()
        {
            _controllerMock.Setup(m => m.AutoProvisionRefreshGroups(It.IsAny<IdpUserRequest>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new UserResponse()));

            const string invalidIdpRequest = "{]";

            var response = await _browserAuth.Post("/v1/users/autoprovisionrefreshgroups", 
                with =>
                {
                    with.HttpRequest();
                    with.Header("Accept", "application/json");
                    with.Header("Content-Type", "application/json");
                    with.JsonBody(invalidIdpRequest);
                });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestBindingException, response.ReasonPhrase);
        }
    }
}
