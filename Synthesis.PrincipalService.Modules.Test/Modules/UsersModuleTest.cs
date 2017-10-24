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
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Constants;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Serialization;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Entity;
using Synthesis.PrincipalService.Mapper;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;
using Synthesis.PrincipalService.Utilities;
using Synthesis.PrincipalService.Workflow.Controllers;
using Synthesis.PrincipalService.Workflow.Exceptions;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ClaimTypes = System.IdentityModel.Claims.ClaimTypes;

namespace Synthesis.PrincipalService.Modules.Test.Modules
{
    [SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
    public class UsersModuleTest
    {
        private readonly Browser _browserAuth;
        private readonly Browser _browserNoAuth;

        private readonly Mock<IUsersController> _controllerMock = new Mock<IUsersController>();
        private readonly Mock<IRepository<User>> _userRepositoryMock = new Mock<IRepository<User>>();

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
            _controllerMock.Setup(m => m.UpdateUserAsync(It.IsAny<Guid>(), It.IsAny<UpdateUserRequest>()))
                           .ReturnsAsync(new UserResponse());
            Guid.TryParse("DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3", out var tenantId);
            _controllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new UserResponse() { TenantId = tenantId });
            var actual = await _browserAuth.Put(
                                                 $"/v1/users/{Guid.NewGuid()}",
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
                                                  $"/v1/users/{Guid.NewGuid()}",
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
            _controllerMock.Setup(m => m.UpdateUserAsync(It.IsAny<Guid>(), It.IsAny<UpdateUserRequest>()))
                           .Throws(new DocumentNotFoundException());
            var actual = await _browserAuth.Put(
                                                $"/v1/users/{Guid.NewGuid()}",
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
                                                $"/v1/users/{Guid.NewGuid()}",
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
            Guid.TryParse("DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3", out var tenantId);
            _controllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new UserResponse(){TenantId = tenantId });

            
            var actual = await _browserAuth.Put(
                                                $"/v1/users/{Guid.NewGuid()}",
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
            var email = "asd@hmm.com";
            var response = await _browserAuth.Get($"api/v1/users/canpromoteuser/{email}", with =>
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
            var email = "asd@hmm.com";
            var response = await _browserAuth.Get($"api/v1/users/canpromoteuser/{email}", with =>
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
            var email = "asd@hmm.com";
            var response = await _browserAuth.Get($"api/v1/users/canpromoteuser/{email}", with =>
                                                                                  {
                                                                                      with.HttpRequest();
                                                                                      with.Header("Accept", "application/json");
                                                                                      with.Header("Content-Type", "application/json");
                                                                                  });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CanPromoteuserReturnsUserNotFound()
        {
            _controllerMock.Setup(m => m.CanPromoteUserAsync(It.IsAny<string>()))
                           .Throws(new NotFoundException("User Doesn't Exist"));
            var email = "";
            var response = await _browserAuth.Get($"api/v1/users/canpromoteuser/{email}", with =>
                                                                                  {
                                                                                      with.HttpRequest();
                                                                                      with.Header("Accept", "application/json");
                                                                                      with.Header("Content-Type", "application/json");
                                                                                  });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        #endregion

        #region Resend welcome Email Test Cases
        [Fact]
        public async Task ResendWelcomeEmailReturnsOk()
        {
            _controllerMock.Setup(m => m.ResendUserWelcomeEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                           .Returns(Task.FromResult(true));
            var response = await _browserAuth.Post($"/v1/users/resendwelcomemail", with =>
                                                                                  {
                                                                                      with.HttpRequest();
                                                                                      with.Header("Accept", "application/json");
                                                                                      with.Header("Content-Type", "application/json");
                                                                                      with.JsonBody(new ResendEmailRequest());
                                                                                  });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task ResendWelcomeEmailReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.ResendUserWelcomeEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                           .Throws(new Exception());
            var response = await _browserAuth.Post($"/v1/users/resendwelcomemail", with =>
                                                                                   {
                                                                                       with.HttpRequest();
                                                                                       with.Header("Accept", "application/json");
                                                                                       with.Header("Content-Type", "application/json");
                                                                                       with.JsonBody(new ResendEmailRequest());
                                                                                   });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task ResendWelcomeEmailReturnsBadRequestDuetoBinding()
        {
            _controllerMock.Setup(m => m.ResendUserWelcomeEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                           .Throws(new Exception());
            var response = await _browserAuth.Post($"/v1/users/resendwelcomemail", with =>
                                                                                   {
                                                                                       with.HttpRequest();
                                                                                       with.Header("Accept", "application/json");
                                                                                       with.Header("Content-Type", "application/json");
                                                                                       with.JsonBody("{]");
                                                                                   });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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

        [Fact]
        [Trait("User Group","User Group Tests")]
        public async Task GetUsersForGroupReturnFound()
        {
            var validGroupId = Guid.NewGuid();

            _controllerMock.Setup(m => m.GetGroupUsers(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new List<Guid>()));

            _userRepositoryMock.Setup(m => m.GetItemsAsync(u => u.Groups.Contains(validGroupId)))
                               .Returns(Task.FromResult(Enumerable.Empty<User>()));
            

            var response = await _browserAuth.Get($"/v1/groups/{validGroupId}/users", with =>
                                                {
                                                    with.HttpRequest();
                                                    with.Header("Accept", "application/json");
                                                    with.Header("Content-Type", "application/json");
                                                });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        [Trait("User Group", "User Group Tests")]
        public async Task GetUsersForGroupReturnNotFoundException()
        {
            var validGroupId = Guid.NewGuid();

            _controllerMock.Setup(m => m.GetGroupUsers(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new NotFoundException(string.Empty));

            _userRepositoryMock.Setup(m => m.GetItemsAsync(u => u.Groups.Contains(validGroupId)))
                               .Returns(Task.FromResult(Enumerable.Empty<User>()));


            var response = await _browserAuth.Get($"/v1/usergroups/{validGroupId}", with =>
                                                {
                                                    with.HttpRequest();
                                                    with.Header("Accept", "application/json");
                                                    with.Header("Content-Type", "application/json");
                                                });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        [Trait("User Group", "User Group Tests")]
        public async Task GetUsersForGroupReturnValidationException()
        {
            var validGroupId = Guid.NewGuid();

            _controllerMock.Setup(m => m.GetGroupUsers(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new ValidationFailedException(Enumerable.Empty<ValidationFailure>()));

            _userRepositoryMock.Setup(m => m.GetItemsAsync(u => u.Groups.Contains(validGroupId)))
                               .Returns(Task.FromResult(Enumerable.Empty<User>()));


            var response = await _browserAuth.Get($"/v1/groups/{validGroupId}/users", with =>
                                                                                    {
                                                                                        with.HttpRequest();
                                                                                        with.Header("Accept", "application/json");
                                                                                        with.Header("Content-Type", "application/json");
                                                                                    });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        [Trait("User Group", "User Group Tests")]
        public async Task GetUsersForGroupReturnUnAuthorized()
        {
            var validGroupId = Guid.NewGuid();

            _controllerMock.Setup(m => m.GetGroupUsers(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new List<Guid>()));

            _userRepositoryMock.Setup(m => m.GetItemsAsync(u => u.Groups.Contains(validGroupId)))
                               .Returns(Task.FromResult(Enumerable.Empty<User>()));


            var response = await _browserNoAuth.Get($"/v1/groups/{validGroupId}/users", with =>
                                                                                    {
                                                                                        with.HttpRequest();
                                                                                        with.Header("Accept", "application/json");
                                                                                        with.Header("Content-Type", "application/json");
                                                                                    });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        [Trait("User Group", "User Group Tests")]
        public async Task GetUsersForGroupReturnInternalServerError()
        {
            var validGroupId = Guid.NewGuid();

            _controllerMock.Setup(m => m.GetGroupUsers(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new Exception());

            _userRepositoryMock.Setup(m => m.GetItemsAsync(u => u.Groups.Contains(validGroupId)))
                               .Returns(Task.FromResult(Enumerable.Empty<User>()));


            var response = await _browserAuth.Get($"/v1/groups/{validGroupId}/users", with =>
                                                                                    {
                                                                                        with.HttpRequest();
                                                                                        with.Header("Accept", "application/json");
                                                                                        with.Header("Content-Type", "application/json");
                                                                                    });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        #endregion

        #region Get UserGroups For User
        [Fact]
        public async Task GetGroupsForUserReturnsFound()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var currentUserId);
            _controllerMock.Setup(m => m.GetGroupsForUserAsync(It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new List<Guid>()));

            _userRepositoryMock.Setup(m => m.GetItemsAsync(u => u.Id == currentUserId))
                               .Returns(Task.FromResult(Enumerable.Empty<User>()));
            _controllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new UserResponse(){Id = currentUserId});
            var response = await _browserAuth.Get($"/v1/users/{currentUserId}/groups", with =>
                                                                                   {
                                                                                       with.HttpRequest();
                                                                                       with.Header("Accept", "application/json");
                                                                                       with.Header("Content-Type", "application/json");
                                                                                   });
            Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupsForUserReturnsBadRequestDueToValidationException()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var currentUserId);
            _controllerMock.Setup(m => m.GetGroupsForUserAsync(It.IsAny<Guid>()))
                           .ThrowsAsync(new ValidationFailedException(new List<ValidationFailure>()));

            _userRepositoryMock.Setup(m => m.GetItemsAsync(u => u.Id == currentUserId))
                               .Returns(Task.FromResult(Enumerable.Empty<User>()));
            _controllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new UserResponse() { Id = currentUserId });
            var response = await _browserAuth.Get($"/v1/users/{currentUserId}/groups", with =>
                                                                                          {
                                                                                              with.HttpRequest();
                                                                                              with.Header("Accept", "application/json");
                                                                                              with.Header("Content-Type", "application/json");
                                                                                          });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupsForUserReturnsInternalServerError()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var currentUserId);
            _controllerMock.Setup(m => m.GetGroupsForUserAsync(It.IsAny<Guid>()))
                           .ThrowsAsync(new Exception());

            _userRepositoryMock.Setup(m => m.GetItemsAsync(u => u.Id == currentUserId))
                               .Returns(Task.FromResult(Enumerable.Empty<User>()));
            _controllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new UserResponse() { Id = currentUserId });
            var response = await _browserAuth.Get($"/v1/users/{currentUserId}/groups", with =>
                                                                                          {
                                                                                              with.HttpRequest();
                                                                                              with.Header("Accept", "application/json");
                                                                                              with.Header("Content-Type", "application/json");
                                                                                          });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupsForUserReturnsUnauthorizedValidUserLevelAccess()
        {
            var currentUserId = Guid.NewGuid();
            _controllerMock.Setup(m => m.GetGroupsForUserAsync(It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new List<Guid>()));

            _userRepositoryMock.Setup(m => m.GetItemsAsync(u => u.Id == currentUserId))
                               .Returns(Task.FromResult(Enumerable.Empty<User>()));
            _controllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new UserResponse() { Id = currentUserId });
            var response = await _browserAuth.Get($"/v1/users/{currentUserId}/groups", with =>
                                                                                          {
                                                                                              with.HttpRequest();
                                                                                              with.Header("Accept", "application/json");
                                                                                              with.Header("Content-Type", "application/json");
                                                                                          });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupsForUserReturnsUnauthorized()
        {
            var userId = Guid.NewGuid();
            var response = await _browserNoAuth.Get($"/v1/users/{userId}/groups", with =>
                                                                                          {
                                                                                              with.HttpRequest();
                                                                                              with.Header("Accept", "application/json");
                                                                                              with.Header("Content-Type", "application/json");
                                                                                          });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        [Fact]
        public async Task GetGroupsForUserReturnNotFound()
        {
            var userId = Guid.NewGuid();

            _controllerMock.Setup(m => m.GetGroupsForUserAsync(It.IsAny<Guid>()))
                           .Throws(new NotFoundException("Record not found"));

            _userRepositoryMock.Setup(m => m.GetItemsAsync(u => u.Id == userId))
                               .Throws(new Exception());


            var response = await _browserAuth.Get($"/v1/users/{userId}/groups", with =>
                                                                                    {
                                                                                        with.HttpRequest();
                                                                                        with.Header("Accept", "application/json");
                                                                                        with.Header("Content-Type", "application/json");
                                                                                    });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        #endregion

        #region Get Guest Users For Tenant Test Cases

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

        #endregion

        #region Auto Provision Refresh Groups Test Cases

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

        #endregion

        #region Remove User from Permission Group
        [Fact]
        public async Task RemoveUserfromPermissionGroupReturnsSuccess()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var userId);
            _controllerMock.Setup(m => m.RemoveUserFromPermissionGroupAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<Guid>()))
                           .Returns(Task.FromResult(true));
            var response = await _browserAuth.Delete($"/v1/groups/{Guid.NewGuid()}/users/{userId}", with =>
                                                                                       {
                                                                                           with.HttpRequest();
                                                                                           with.Header("Accept", "application/json");
                                                                                           with.Header("Content-Type", "application/json");
                                                                                       });
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task RemoveUserfromPermissionGroupReturnsNotFound()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var userId);
            _controllerMock.Setup(m => m.RemoveUserFromPermissionGroupAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .ThrowsAsync(new DocumentNotFoundException("Couldn't find the user"));
            var response = await _browserAuth.Delete($"/v1/groups/{Guid.NewGuid()}/users/{userId}", with =>
                                                                                        {
                                                                                            with.HttpRequest();
                                                                                            with.Header("Accept", "application/json");
                                                                                            with.Header("Content-Type", "application/json");
                                                                                        });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task RemoveUserfromPermissionGroupReturnsBadRequestDueToValidationFailure()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var userId);
            _controllerMock.Setup(m => m.RemoveUserFromPermissionGroupAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .ThrowsAsync(new ValidationFailedException(new List<ValidationFailure>()));
            var response = await _browserAuth.Delete($"/v1/groups/{Guid.NewGuid()}/users/{userId}", with =>
                                                                                 {
                                                                                     with.HttpRequest();
                                                                                     with.Header("Accept", "application/json");
                                                                                     with.Header("Content-Type", "application/json");
                                                                                 });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task RemoveUserfromPermissionGroupReturnsBadRequestDueToBindFailure()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var userId);
            var response = await _browserAuth.Delete($"/v1/groups/{Guid.NewGuid()}/users/{userId}", with =>
                                                                                 {
                                                                                     with.HttpRequest();
                                                                                     with.Header("Accept", "application/json");
                                                                                     with.Header("Content-Type", "application/json");
                                                                                     with.JsonBody("{]");
                                                                                 });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task RemoveUserfromPermissionGroupReturnsInternalServerError()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var userId);
            _controllerMock.Setup(m => m.RemoveUserFromPermissionGroupAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .ThrowsAsync(new Exception());
            var response = await _browserAuth.Delete($"/v1/groups/{Guid.NewGuid()}/users/{userId}", with =>
                                                                                 {
                                                                                     with.HttpRequest();
                                                                                     with.Header("Accept", "application/json");
                                                                                     with.Header("Content-Type", "application/json");
                                                                                 });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        #endregion

        #region Get Tenant Id by User Email Test Cases

        [Trait("Get Tenant Id by User Email", "Get Tenant Id by User Email")]
        [Fact]
        public async Task GetTenantIdByUserEmailSuccess()
        {
            var validEmail = "user@prysm.com";
            var actual = await _browserAuth.Get($"/v1/users/tenantid/{validEmail}",
                                                with =>
                                                {
                                                    with.Header("Accept", "application/json");
                                                    with.Header("Content-Type", "application/json");
                                                    with.HttpRequest();
                                                });
            Assert.Equal(HttpStatusCode.OK, actual.StatusCode);
        }

        [Trait("Get Tenant Id by User Email", "Get Tenant Id by User Email")]
        [Fact]
        public async Task GetTenantIdByUserEmailReturnsInternalServerError()
        {
            var validEmail = "user@prysm.com";
            _controllerMock.Setup(m => m.GetTenantIdByUserEmailAsync(It.IsAny<string>()))
                           .ThrowsAsync(new Exception());
            var actual = await _browserAuth.Get($"/v1/users/tenantid/{validEmail}",
                                                with =>
                                                {
                                                    with.Header("Accept", "application/json");
                                                    with.Header("Content-Type", "application/json");
                                                    with.HttpRequest();
                                                });
            Assert.Equal(HttpStatusCode.InternalServerError, actual.StatusCode);
        }

        [Trait("Get Tenant Id by User Email", "Get Tenant Id by User Email")]
        [Fact]
        public async Task GetTenantIdByUserEmailReturnsBadRequestIfValidationFails()
        {
            var validEmail = "user@prysm.com";
            _controllerMock.Setup(m => m.GetTenantIdByUserEmailAsync(It.IsAny<string>()))
                           .ThrowsAsync(new ValidationFailedException(new List<ValidationFailure>()));
            var actual = await _browserAuth.Get($"/v1/users/tenantid/{validEmail}",
                                                with =>
                                                {
                                                    with.Header("Accept", "application/json");
                                                    with.Header("Content-Type", "application/json");
                                                    with.HttpRequest();
                                                });
            Assert.Equal(HttpStatusCode.BadRequest, actual.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, actual.ReasonPhrase);
        }

        #endregion

        #region Get Users by Ids Test Cases

        [Fact]
        public async Task GetUsersByIdsReturnsOkIfSuccessful()
        {
            var result = await _browserAuth.Post(Routing.GetUsersByIds, with =>
                                                           {
                                                               with.Header("Accept", "application/json");
                                                               with.Header("Content-Type", "application/json");
                                                               with.HttpRequest();
                                                               with.JsonBody(new List<Guid>());
                                                           });

            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        }

        [Fact]
        public async Task GetUsersByIdsReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.GetUsersByIds(It.IsAny<IEnumerable<Guid>>()))
                           .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var result = await _browserAuth.Post(Routing.GetUsersByIds, with =>
                                                                        {
                                                                            with.Header("Accept", "application/json");
                                                                            with.Header("Content-Type", "application/json");
                                                                            with.HttpRequest();
                                                                            with.JsonBody(new List<Guid>());
                                                                        });

            Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        }

        [Fact]
        public async Task GetUsersByIdsReturnsNotFoundIfNoUsersAreFound()
        {
            _controllerMock.Setup(m => m.GetUsersByIds(It.IsAny<IEnumerable<Guid>>()))
                           .Throws(new NotFoundException(string.Empty));

            var result = await _browserAuth.Post(Routing.GetUsersByIds, with =>
                                                                        {
                                                                            with.Header("Accept", "application/json");
                                                                            with.Header("Content-Type", "application/json");
                                                                            with.HttpRequest();
                                                                            with.JsonBody(new List<Guid>());
                                                                        });

            Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        }

        [Fact]
        public async Task GetUsersByIdsReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(m => m.GetUsersByIds(It.IsAny<IEnumerable<Guid>>()))
                           .Throws(new Exception());

            var result = await _browserAuth.Post(Routing.GetUsersByIds, with =>
                                                                        {
                                                                            with.Header("Accept", "application/json");
                                                                            with.Header("Content-Type", "application/json");
                                                                            with.HttpRequest();
                                                                            with.JsonBody(new List<Guid>());
                                                                        });

            Assert.Equal(HttpStatusCode.InternalServerError, result.StatusCode);
        }

        #endregion
    }
}
