using AutoMapper;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Nancy;
using Nancy.Testing;
using Synthesis.DocumentStorage;
using Synthesis.License.Manager.Models;
using Synthesis.Logging;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Constants;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Constants;
using Synthesis.PrincipalService.Models;
using Synthesis.PrincipalService.Entity;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Synthesis.Authentication;
using Synthesis.PolicyEvaluator;
using Synthesis.PolicyEvaluator.Models;
using Synthesis.PrincipalService.Controllers.Exceptions;
using Synthesis.PrincipalService.Controllers.Interfaces;
using Xunit;
using ClaimTypes = Synthesis.Nancy.MicroService.Constants.ClaimTypes;

namespace Synthesis.PrincipalService.Modules.Test.Modules
{
    [SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
    public class UsersModuleTest
    {
        private Browser AuthenticatedBrowser => GetBrowser();
        private Browser UnauthenticatedBrowser => GetBrowser(false);

        // TODO: Uncomment the browsers below when ForbiddenBrowser tests are added
        //private Browser ForbiddenBrowser => GetBrowser(true, false);

        private readonly Mock<IUsersController> _controllerMock = new Mock<IUsersController>();
        private readonly Mock<IRepository<User>> _userRepositoryMock = new Mock<IRepository<User>>();
        private readonly Mock<IPolicyEvaluator> _policyEvaluatorMock = new Mock<IPolicyEvaluator>();
        private readonly Mock<IPolicyEvaluator> _policyEvaluatorForbiddenMock = new Mock<IPolicyEvaluator>();
        private readonly Mock<ITokenValidator> _tokenValidatorMock = new Mock<ITokenValidator>();
        private readonly Mock<IMetadataRegistry> _metadataRegistryMock = new Mock<IMetadataRegistry>();
        private readonly Mock<ILoggerFactory> _loggerFactoryMock = new Mock<ILoggerFactory>();

        private const string ValidTestEmail = "asd@hmm.com";
        private const string EmptyTestEmail = "";

        public UsersModuleTest()
        {
            _loggerFactoryMock.Setup(m => m.Get(It.IsAny<LogTopic>()))
                .Returns(new Mock<ILogger>().Object);

            _policyEvaluatorForbiddenMock
                .Setup(x => x.EvaluateAsync(It.IsAny<PolicyEvaluationContext>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(PermissionScope.Deny);

            _metadataRegistryMock
                .Setup(x => x.GetRouteMetadata(It.IsAny<string>()))
                .Returns<string>(name => new SynthesisRouteMetadata(null, null, name));
        }

        private Browser GetBrowser(bool isAuthenticated = true, bool hasAccess = true)
        {
            return new Browser(with =>
            {
                if (isAuthenticated)
                {
                    with.RequestStartup((container, pipelines, context) =>
                    {
                        var identity = new ClaimsIdentity(new[]
                            {
                                new Claim(ClaimTypes.Account, "Test User"),
                                new Claim(ClaimTypes.Email, "test@user.com")
                            },
                            AuthenticationTypes.Basic);
                        context.CurrentUser = new ClaimsPrincipal(identity);
                    });
                }

                with.Dependency(_controllerMock.Object);
                with.Dependency(_userRepositoryMock.Object);
                with.Dependency(_tokenValidatorMock.Object);
                with.Dependency(_loggerFactoryMock.Object);
                with.Dependency(_metadataRegistryMock.Object);
                with.Dependency(hasAccess ? _policyEvaluatorMock.Object : _policyEvaluatorForbiddenMock.Object);
                with.Module<UsersModule>();
                with.EnableAutoRegistration();
            });
        }

        private static void BuildRequest(BrowserContext context)
        {
            context.HttpRequest();
            context.Header("Accept", "application/json");
            context.Header("Content-Type", "application/json");
        }

        private static void BuildRequest<T>(BrowserContext context, T body)
        {
            context.HttpRequest();
            context.Header("Accept", "application/json");
            context.Header("Content-Type", "application/json");
            context.JsonBody(body);
        }

        [Fact]
        public async Task RespondWithUnauthorizedNoBearerAsync()
        {
            var response = await UnauthenticatedBrowser.Get("/v1/users/2c1156fa-5902-4978-9c3d-ebcb77ae0d55", BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task RespondWithOkAsync()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var currentUserId);

            _controllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new UserResponse() { Id = currentUserId });

            var response = await AuthenticatedBrowser.Get($"/v1/users/{currentUserId}", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CreateUserReturnsCreatedAsync()
        {
            var response = await AuthenticatedBrowser.Post("/v1/users", ctx => BuildRequest(ctx, new CreateUserRequest()));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task CreateUserReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(m => m.CreateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new Exception());

            var response = await AuthenticatedBrowser.Post("/v1/users", ctx => BuildRequest(ctx, new CreateUserRequest()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CreateUserReturnsItemWithInvalidBodyReturnsBadRequest()
        {
            var response = await AuthenticatedBrowser.Post("/v1/users", ctx => BuildRequest(ctx, "invalid body"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestBindingException, response.ReasonPhrase);
        }

        [Fact]
        public async Task CreateUserReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.CreateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await AuthenticatedBrowser.Post("/v1/users", ctx => BuildRequest(ctx, new CreateUserRequest()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, response.ReasonPhrase);
        }

        [Fact]
        public async Task CreateUserReadsTenantIdFromUserClaimAsync()
        {
            _controllerMock
                .Setup(uc => uc.CreateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .ReturnsAsync(new UserResponse());

            var response = await AuthenticatedBrowser.Post("/v1/users", ctx => BuildRequest(ctx, new CreateUserRequest()));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            _controllerMock.Verify(m=>m.CreateUserAsync(It.IsAny<CreateUserRequest>(), Guid.Parse("DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3"), Guid.Parse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2")));
        }

        [Fact]
        public async Task GetUserByIdBasicReturnsOk()
        {
            _controllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult(new UserResponse()));

            var validUserId = Guid.NewGuid();

            var response = await AuthenticatedBrowser.Get($"/api/v1/users/{validUserId}/basic", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        [Fact]
        public async Task GetUserByIdBasicReturnsBadRequest()
        {
            _controllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var validUserId = Guid.NewGuid();

            var response = await AuthenticatedBrowser.Get($"/api/v1/users/{validUserId}/basic", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        [Fact]
        public async Task GetUserByIdBasicReturnsUnauthorized()
        {
            _controllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult(new UserResponse()));

            var validUserId = Guid.NewGuid();

            var response = await UnauthenticatedBrowser.Get($"/api/v1/users/{validUserId}/basic", BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        [Fact]
        public async Task GetUserByIdBasicReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                .Throws(new Exception());

            var validUserId = Guid.NewGuid();

            var response = await AuthenticatedBrowser.Get($"/api/v1/users/{validUserId}/basic", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
        [Fact]
        public async Task GetUserByIdBasicReturnsNotFoundIfItemDoesNotExist()
        {
            _controllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            var validUserId = Guid.NewGuid();

            var response = await AuthenticatedBrowser.Get($"/api/v1/users/{validUserId}/basic", BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetUsersForAccountReturnsOk()
        {
            _controllerMock.Setup(m => m.GetUsersForAccountAsync(It.IsAny<GetUsersParams>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(new PagingMetadata<UserResponse>()));

            var response = await AuthenticatedBrowser.Get("/api/v1/users/", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        [Fact]
        public async Task GetUsersForAccountReturnsNotFound()
        {
            _controllerMock.Setup(m => m.GetUsersForAccountAsync(It.IsAny<GetUsersParams>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            var response = await AuthenticatedBrowser.Get("/api/v1/users/", BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        [Fact]
        public async Task GetUsersForAccountReturnsBadRequest()
        {
            _controllerMock.Setup(m => m.GetUsersForAccountAsync(It.IsAny<GetUsersParams>(),It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await AuthenticatedBrowser.Get("/api/v1/users/", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        [Fact]
        public async Task GetUsersForAccountReturnsUnauthorized()
        {
            _controllerMock.Setup(m => m.GetUsersForAccountAsync(It.IsAny<GetUsersParams>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(new PagingMetadata<UserResponse>()));

            var response = await UnauthenticatedBrowser.Get("/api/v1/users/", BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
        [Fact]
        public async Task GetUsersForAccountReturnsInternalError()
        {
            _controllerMock.Setup(m => m.GetUsersForAccountAsync(It.IsAny<GetUsersParams>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new Exception());

            var response = await AuthenticatedBrowser.Get("/api/v1/users/", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task PromoteGuestRespondWithUnauthorizedNoBearerAsync()
        {
            var response = await UnauthenticatedBrowser.Post("/v1/users/C3220603-09D9-452B-B204-6CC3946CE1F4/promote", ctx => BuildRequest(ctx, new PromoteGuestRequest()));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task PromoteGuestRespondWithOkAsync()
        {
            var response = await AuthenticatedBrowser.Post("/v1/users/C3220603-09D9-452B-B204-6CC3946CE1F4/promote", ctx => BuildRequest(ctx, new PromoteGuestRequest()));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task PromoteGuestReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock
                .Setup(uc => uc.PromoteGuestUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LicenseType>(), It.IsAny<bool>()))
                           .Throws(new Exception());

            var response = await AuthenticatedBrowser.Post("/v1/users/C3220603-09D9-452B-B204-6CC3946CE1F4/promote", ctx => BuildRequest(ctx, new PromoteGuestRequest()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task PromoteGuestReturnsItemWithInvalidBodyReturnsBadRequest()
        {
            var response = await AuthenticatedBrowser.Post("/v1/users/C3220603-09D9-452B-B204-6CC3946CE1F4/promote", ctx => BuildRequest(ctx, "invalid body"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestBindingException, response.ReasonPhrase);
        }

        [Fact]
        public async Task PromoteGuestReturnsBadRequestIfValidationFails()
        {
            _controllerMock
                .Setup(uc => uc.PromoteGuestUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LicenseType>(), It.IsAny<bool>()))
                           .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await AuthenticatedBrowser.Post("/v1/users/C3220603-09D9-452B-B204-6CC3946CE1F4/promote", ctx => BuildRequest(ctx, new PromoteGuestRequest()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, response.ReasonPhrase);
        }

        [Fact]
        public async Task PromoteGuestReturnsForbiddenIfPromotionFails()
        {
            _controllerMock
                .Setup(uc => uc.PromoteGuestUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LicenseType>(), It.IsAny<bool>()))
                .Throws(new PromotionFailedException(""));

            var response = await AuthenticatedBrowser.Post("/v1/users/C3220603-09D9-452B-B204-6CC3946CE1F4/promote", ctx => BuildRequest(ctx, new PromoteGuestRequest()));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(ResponseReasons.PromotionFailed, response.ReasonPhrase);
        }

        [Fact]
        public async Task PromoteGuestReturnsForbiddenIfLicenseAssignmentFails()
        {
            _controllerMock
                .Setup(uc => uc.PromoteGuestUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LicenseType>(), It.IsAny<bool>()))
                .Throws(new LicenseAssignmentFailedException("", Guid.NewGuid()));

            var response = await AuthenticatedBrowser.Post("/v1/users/C3220603-09D9-452B-B204-6CC3946CE1F4/promote", ctx => BuildRequest(ctx, new PromoteGuestRequest()));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(ResponseReasons.LicenseAssignmentFailed, response.ReasonPhrase);
        }

        [Fact]
        public async Task PromoteGuestReadsTenantIdFromUserClaimAsync()
        {
            _controllerMock
                .Setup(uc => uc.PromoteGuestUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),It.IsAny<LicenseType>(), It.IsAny<bool>()))
                .ReturnsAsync(new PromoteGuestResponse());

            var response = await AuthenticatedBrowser.Post("/v1/users/C3220603-09D9-452B-B204-6CC3946CE1F4/promote", ctx => BuildRequest(ctx, new PromoteGuestRequest { LicenseType = LicenseType.UserLicense }));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            _controllerMock.Verify(m => m.PromoteGuestUserAsync(Guid.Parse("C3220603-09D9-452B-B204-6CC3946CE1F4"), Guid.Parse("DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3"), LicenseType.UserLicense, false));
        }

        [Fact]
        public async Task UpdateUserReturnsOk()
        {
            _controllerMock.Setup(m => m.UpdateUserAsync(It.IsAny<Guid>(), It.IsAny<UpdateUserRequest>()))
                           .ReturnsAsync(new UserResponse());

            Guid.TryParse("DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3", out var tenantId);

            _controllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new UserResponse() { TenantId = tenantId });

            var response = await AuthenticatedBrowser.Put($"/v1/users/{Guid.NewGuid()}", ctx => BuildRequest(ctx, new User()));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        }

        [Fact]
        public async Task UpdateUserReturnsUnauthorized()
        {
            var response = await UnauthenticatedBrowser.Put($"/v1/users/{Guid.NewGuid()}", ctx => BuildRequest(ctx, new User()));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task UpdateUserReturnsNotFound()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var currentUserId);

            _controllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new UserResponse() { Id = currentUserId });

            _controllerMock.Setup(m => m.UpdateUserAsync(It.IsAny<Guid>(), It.IsAny<UpdateUserRequest>()))
                           .Throws(new NotFoundException("user not found"));

            var response = await AuthenticatedBrowser.Put($"/v1/users/{currentUserId}", ctx => BuildRequest(ctx, new User()));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task UpdateUserReturnsBadRequestDueToBindingException()
        {
            var response = await AuthenticatedBrowser.Put($"/v1/users/{Guid.NewGuid()}", ctx => BuildRequest(ctx, "invlaid body"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UpdateUserReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.UpdateUserAsync(It.IsAny<Guid>(), It.IsAny<UpdateUserRequest>()))
                           .Throws(new Exception());

            Guid.TryParse("DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3", out var tenantId);

            _controllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new UserResponse(){TenantId = tenantId });

            var response = await AuthenticatedBrowser.Put($"/v1/users/{Guid.NewGuid()}", ctx => BuildRequest(ctx, new UpdateUserRequest()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task LockUserReturnsSuccess()
        {
            _controllerMock
                .Setup(uc => uc.LockOrUnlockUserAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .ReturnsAsync(true);

            var response = await AuthenticatedBrowser.Post("/v1/users/f629f87c-366d-4790-ac34-964e3558bdcd/lock", ctx => BuildRequest(ctx, new User() { IsLocked = true }));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task LockUserReturnsBadRequest()
        {
            _controllerMock
                .Setup(uc => uc.LockOrUnlockUserAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .ReturnsAsync(true);

            var response = await AuthenticatedBrowser.Post("/v1/users/f629f87c-366d-4790-ac34-964e3558bdcd/lock", ctx => BuildRequest(ctx, "invalid body"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task LockUserReturnsValidationException()
        {
            _controllerMock
                .Setup(uc => uc.LockOrUnlockUserAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await AuthenticatedBrowser.Post("/v1/users/f629f87c-366d-4790-ac34-964e3558bdcd/lock", ctx => BuildRequest(ctx, new User()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task LockUserReturnsInternalServerError()
        {
            _controllerMock
                .Setup(uc => uc.LockOrUnlockUserAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .Throws(new Exception());

            var response = await AuthenticatedBrowser.Post("/v1/users/f629f87c-366d-4790-ac34-964e3558bdcd/lock", ctx => BuildRequest(ctx, new User()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CanPromoteuserReturnsSuccess()
        {
            _controllerMock.Setup(m => m.CanPromoteUserAsync(It.IsAny<string>()))
                           .Returns(Task.FromResult(new CanPromoteUserResponse()));

            var response = await AuthenticatedBrowser.Get($"api/v1/users/canpromoteuser/{ValidTestEmail}", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CanPromoteuserReturnsBadrequest()
        {
            _controllerMock.Setup(m => m.CanPromoteUserAsync(It.IsAny<string>()))
                           .Throws(new ValidationException(new List<ValidationFailure>()));

            var response = await AuthenticatedBrowser.Get($"api/v1/users/canpromoteuser/{ValidTestEmail}", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CanPromoteuserReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.CanPromoteUserAsync(It.IsAny<string>()))
                           .Throws(new Exception());

            var response = await AuthenticatedBrowser.Get($"api/v1/users/canpromoteuser/{ValidTestEmail}", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CanPromoteuserReturnsUserNotFound()
        {
            _controllerMock.Setup(m => m.CanPromoteUserAsync(It.IsAny<string>()))
                           .Throws(new NotFoundException("User Doesn't Exist"));

            var response = await AuthenticatedBrowser.Get($"api/v1/users/canpromoteuser/{EmptyTestEmail}", BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task ResendWelcomeEmailReturnsOk()
        {
            _controllerMock.Setup(m => m.ResendUserWelcomeEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                           .Returns(Task.FromResult(true));

            var response = await AuthenticatedBrowser.Post("/v1/users/resendwelcomemail", ctx => BuildRequest(ctx, new ResendEmailRequest()));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task ResendWelcomeEmailReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.ResendUserWelcomeEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                           .Throws(new Exception());

            var response = await AuthenticatedBrowser.Post("/v1/users/resendwelcomemail", ctx => BuildRequest(ctx, new ResendEmailRequest()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task ResendWelcomeEmailReturnsBadRequestDuetoBinding()
        {
            _controllerMock.Setup(m => m.ResendUserWelcomeEmailAsync(It.IsAny<string>(), It.IsAny<string>()))
                           .Throws(new Exception());

            var response = await AuthenticatedBrowser.Post("/v1/users/resendwelcomemail", ctx => BuildRequest(ctx, "invlaid body"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CreateUserGroupReturnsCreated()
        {
            var response = await AuthenticatedBrowser.Post("/v1/usergroups", ctx => BuildRequest(ctx, new CreateUserGroupRequest()));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task CreateUserGroupReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
           _controllerMock.Setup(m => m.CreateUserGroupAsync(It.IsAny<CreateUserGroupRequest>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new Exception());

            var response = await AuthenticatedBrowser.Post("/v1/usergroups", ctx => BuildRequest(ctx, new CreateUserGroupRequest()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CreateUserGroupReturnsItemWithInvalidBodyReturnsBadRequest()
        {
            var response = await AuthenticatedBrowser.Post("/v1/usergroups", ctx => BuildRequest(ctx, "invalid body"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestBindingException, response.ReasonPhrase);
        }

        [Fact]
        public async Task CreateUserGroupReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.CreateUserGroupAsync(It.IsAny<CreateUserGroupRequest>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await AuthenticatedBrowser.Post("/v1/usergroups", ctx => BuildRequest(ctx, new CreateUserGroupRequest()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, response.ReasonPhrase);
        }

        [Fact]
        [Trait("User Group","User Group Tests")]
        public async Task GetUsersForGroupReturnFound()
        {
            var validGroupId = Guid.NewGuid();

            _controllerMock.Setup(m => m.GetGroupUsersAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new List<Guid>()));

            _userRepositoryMock.Setup(m => m.GetItemsAsync(u => u.Groups.Contains(validGroupId)))
                               .Returns(Task.FromResult(Enumerable.Empty<User>()));

            var response = await AuthenticatedBrowser.Get($"/v1/groups/{validGroupId}/users", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        [Trait("User Group", "User Group Tests")]
        public async Task GetUsersForGroupReturnNotFoundException()
        {
            var validGroupId = Guid.NewGuid();

            _controllerMock.Setup(m => m.GetGroupUsersAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new NotFoundException(string.Empty));

            _userRepositoryMock.Setup(m => m.GetItemsAsync(u => u.Groups.Contains(validGroupId)))
                               .Returns(Task.FromResult(Enumerable.Empty<User>()));

            var response = await AuthenticatedBrowser.Get($"/v1/usergroups/{validGroupId}", BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        [Trait("User Group", "User Group Tests")]
        public async Task GetUsersForGroupReturnValidationException()
        {
            var validGroupId = Guid.NewGuid();

            _controllerMock.Setup(m => m.GetGroupUsersAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new ValidationFailedException(Enumerable.Empty<ValidationFailure>()));

            _userRepositoryMock.Setup(m => m.GetItemsAsync(u => u.Groups.Contains(validGroupId)))
                               .Returns(Task.FromResult(Enumerable.Empty<User>()));

            var response = await AuthenticatedBrowser.Get($"/v1/groups/{validGroupId}/users", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        [Trait("User Group", "User Group Tests")]
        public async Task GetUsersForGroupReturnUnAuthorized()
        {
            var validGroupId = Guid.NewGuid();

            _controllerMock.Setup(m => m.GetGroupUsersAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new List<Guid>()));

            _userRepositoryMock.Setup(m => m.GetItemsAsync(u => u.Groups.Contains(validGroupId)))
                               .Returns(Task.FromResult(Enumerable.Empty<User>()));

            var response = await UnauthenticatedBrowser.Get($"/v1/groups/{validGroupId}/users", BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        [Trait("User Group", "User Group Tests")]
        public async Task GetUsersForGroupReturnInternalServerError()
        {
            var validGroupId = Guid.NewGuid();

            _controllerMock.Setup(m => m.GetGroupUsersAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new Exception());

            _userRepositoryMock.Setup(m => m.GetItemsAsync(u => u.Groups.Contains(validGroupId)))
                               .Returns(Task.FromResult(Enumerable.Empty<User>()));

            var response = await AuthenticatedBrowser.Get($"/v1/groups/{validGroupId}/users", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

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

            var response = await AuthenticatedBrowser.Get($"/v1/users/{currentUserId}/groups", BuildRequest);

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

            var response = await AuthenticatedBrowser.Get($"/v1/users/{currentUserId}/groups", BuildRequest);

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

            var response = await AuthenticatedBrowser.Get($"/v1/users/{currentUserId}/groups", BuildRequest);

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

            var response = await AuthenticatedBrowser.Get($"/v1/users/{currentUserId}/groups", BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupsForUserReturnsUnauthorized()
        {
            var userId = Guid.NewGuid();

            var response = await UnauthenticatedBrowser.Get($"/v1/users/{userId}/groups", BuildRequest);

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

            var response = await AuthenticatedBrowser.Get($"/v1/users/{userId}/groups", BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestUsersForTenantSuccess()
        {
            var response = await AuthenticatedBrowser.Get("/v1/users/guests", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestUsersForTenantReturnsUnauthorized()
        {
            var response = await UnauthenticatedBrowser.Get("/v1/users/guests", BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestUsersForTenantReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.GetGuestUsersForTenantAsync(It.IsAny<Guid>(), It.IsAny<GetUsersParams>()))
                           .ThrowsAsync(new Exception());

            var response = await AuthenticatedBrowser.Get("/v1/users/guests", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task AutoProvisionRefreshGroupsReturnUser()
        {
            var response = await AuthenticatedBrowser.Post("/v1/users/autoprovisionrefreshgroups", ctx => BuildRequest(ctx, new IdpUserRequest()));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task AutoProvisionRefreshGroupsReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(m => m.AutoProvisionRefreshGroupsAsync(It.IsAny<IdpUserRequest>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new Exception());

            var response = await AuthenticatedBrowser.Post("/v1/users/autoprovisionrefreshgroups", ctx => BuildRequest(ctx, new IdpUserRequest()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task AutoProvisionRefreshGroupsWithInvalidBodyReturnsBadRequest()
        {
            _controllerMock.Setup(m => m.AutoProvisionRefreshGroupsAsync(It.IsAny<IdpUserRequest>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new UserResponse()));

            var response = await AuthenticatedBrowser.Post("/v1/users/autoprovisionrefreshgroups", ctx => BuildRequest(ctx, "invalid idp request"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestBindingException, response.ReasonPhrase);
        }

        [Fact]
        public async Task RemoveUserfromPermissionGroupReturnsSuccess()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var userId);

            _controllerMock.Setup(m => m.RemoveUserFromPermissionGroupAsync(It.IsAny<Guid>(),It.IsAny<Guid>(),It.IsAny<Guid>()))
                           .Returns(Task.FromResult(true));

            var response = await AuthenticatedBrowser.Delete($"/v1/groups/{Guid.NewGuid()}/users/{userId}", BuildRequest);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task RemoveUserfromPermissionGroupReturnsNotFound()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var userId);

            _controllerMock.Setup(m => m.RemoveUserFromPermissionGroupAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .ThrowsAsync(new DocumentNotFoundException("Couldn't find the user"));

            var response = await AuthenticatedBrowser.Delete($"/v1/groups/{Guid.NewGuid()}/users/{userId}", BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task RemoveUserfromPermissionGroupReturnsBadRequestDueToValidationFailure()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var userId);

            _controllerMock.Setup(m => m.RemoveUserFromPermissionGroupAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .ThrowsAsync(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await AuthenticatedBrowser.Delete($"/v1/groups/{Guid.NewGuid()}/users/{userId}", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task RemoveUserfromPermissionGroupReturnsBadRequestDueToBindFailure()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var userId);

            var response = await AuthenticatedBrowser.Delete($"/v1/groups/{Guid.NewGuid()}/users/{userId}", ctx => BuildRequest(ctx, "invalid data"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task RemoveUserfromPermissionGroupReturnsInternalServerError()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var userId);

            _controllerMock.Setup(m => m.RemoveUserFromPermissionGroupAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .ThrowsAsync(new Exception());

            var response = await AuthenticatedBrowser.Delete($"/v1/groups/{Guid.NewGuid()}/users/{userId}", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Trait("Get Tenant Id by User Email", "Get Tenant Id by User Email")]
        [Fact]
        public async Task GetTenantIdByUserEmailSuccess()
        {
            var response = await AuthenticatedBrowser.Get($"/v1/users/tenantid/{ValidTestEmail}", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Trait("Get Tenant Id by User Email", "Get Tenant Id by User Email")]
        [Fact]
        public async Task GetTenantIdByUserEmailReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.GetTenantIdByUserEmailAsync(It.IsAny<string>()))
                           .ThrowsAsync(new Exception());

            var response = await AuthenticatedBrowser.Get($"/v1/users/tenantid/{ValidTestEmail}", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Trait("Get Tenant Id by User Email", "Get Tenant Id by User Email")]
        [Fact]
        public async Task GetTenantIdByUserEmailReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.GetTenantIdByUserEmailAsync(It.IsAny<string>()))
                           .ThrowsAsync(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await AuthenticatedBrowser.Get($"/v1/users/tenantid/{ValidTestEmail}", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, response.ReasonPhrase);
        }

        [Fact]
        public async Task GetUsersByIdsReturnsOkIfSuccessful()
        {
            var response = await AuthenticatedBrowser.Post(Routing.GetUsersByIds, ctx => BuildRequest(ctx, new List<Guid>()));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetUsersByIdsReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.GetUsersByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                           .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await AuthenticatedBrowser.Post(Routing.GetUsersByIds, ctx => BuildRequest(ctx, new List<Guid>()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetUsersByIdsReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(m => m.GetUsersByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                           .Throws(new Exception());

            var response = await AuthenticatedBrowser.Post(Routing.GetUsersByIds, ctx => BuildRequest(ctx, new List<Guid>()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Trait("GetLicenseTypeForUser", "Get License Type For User")]
        [Fact]
        public async Task GetLicenseTypeForUserReturnsOk()
        {
            _controllerMock.Setup(m => m.GetLicenseTypeForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .ReturnsAsync(LicenseType.Default);

            var userId = Guid.NewGuid();

            var response = await AuthenticatedBrowser.Get($"/v1/users/{userId}/license-types", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Trait("GetLicenseTypeForUser", "Get License Type For User")]
        [Fact]
        public async Task GetLicenseTypeForUserReturnsNotFoundIfUserDoesNotExist()
        {
            _controllerMock.Setup(m => m.GetLicenseTypeForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new NotFoundException(string.Empty));

            var userId = Guid.NewGuid();

            var response = await AuthenticatedBrowser.Get($"/v1/users/{userId}/license-types", BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Trait("GetLicenseTypeForUser", "Get License Type For User")]
        [Fact]
        public async Task GetLicenseTypeForUserReturnsUnAuthorized()
        {
            _controllerMock.Setup(m => m.GetLicenseTypeForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new InvalidOperationException());

            var userId = Guid.NewGuid();

            var response = await AuthenticatedBrowser.Get($"/v1/users/{userId}/license-types", BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }


        [Trait("GetLicenseTypeForUser", "Get License Type For User")]
        [Fact]
        public async Task GetLicenseTypeForUserReturnsInernalServerError()
        {
            _controllerMock.Setup(m => m.GetLicenseTypeForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new Exception());

            var userId = Guid.NewGuid();

            var response = await AuthenticatedBrowser.Get($"/v1/users/{userId}/license-types", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
    }
}
