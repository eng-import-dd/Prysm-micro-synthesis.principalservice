using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using Nancy;
using Nancy.Testing;
using Synthesis.DocumentStorage;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Constants;
using Synthesis.Nancy.MicroService.Entity;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.Controllers.Exceptions;
using Synthesis.PrincipalService.Exceptions;
using Synthesis.PrincipalService.InternalApi.Constants;
using Synthesis.PrincipalService.InternalApi.Enums;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.TenantService.InternalApi.Api;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Modules
{
    public class UsersModuleTests : BaseModuleTests<UsersModule>
    {
        private readonly Mock<IUsersController> _usersControllerMock = new Mock<IUsersController>();
        private readonly Mock<IGroupsController> _groupsControllerMock = new Mock<IGroupsController>();
        private readonly Mock<ITenantApi> _tenantApiMock = new Mock<ITenantApi>();

        protected override List<object> BrowserDependencies => new List<object>
        {
            _usersControllerMock.Object,
            _groupsControllerMock.Object,
            _tenantApiMock.Object
        };

        private const string ValidTestEmail = "asd@hmm.com";

        [Fact]
        public async Task RespondWithUnauthorizedNoBearerAsync()
        {
            var response = await UnauthenticatedBrowser.Get(string.Format(Routing.UsersWithItemBase, "2c1156fa-5902-4978-9c3d-ebcb77ae0d55"), BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task RespondWithOkAsync()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var currentUserId);

            _usersControllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new User { Id = currentUserId });

            var response = await UserTokenBrowser.Get(string.Format(Routing.UsersWithItemBase, currentUserId), BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        #region GetUserNames

        [Fact]
        public async Task GetUserNamesReturnsOk()
        {
            var response = await UserTokenBrowser.Post(Routing.GetUserNames, ctx => BuildRequest(ctx, new List<Guid>()));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetUserNamesReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _usersControllerMock.Setup(m => m.GetNamesForUsersAsync(It.IsAny<List<Guid>>()))
                .Throws(new Exception());

            var response = await UserTokenBrowser.Post(Routing.GetUserNames, ctx => BuildRequest(ctx, new List<Guid>()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetUserNamesWithInvalidBodyReturnsBadRequest()
        {
            var response = await UserTokenBrowser.Post(Routing.GetUserNames, ctx => BuildRequest(ctx, new List<UserNames> { new UserNames() }));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestBindingException, response.ReasonPhrase);
        }

        [Fact]
        public async Task GetUserNamesReturnsBadRequestIfValidationFails()
        {
            _usersControllerMock.Setup(m => m.GetNamesForUsersAsync(It.IsAny<List<Guid>>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await UserTokenBrowser.Post(Routing.GetUserNames, ctx => BuildRequest(ctx, new List<Guid>()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, response.ReasonPhrase);
        }

        [Fact]
        public async Task GetUserNamesReturnsUnauthorizedWithoutBearerToken()
        {
            var response = await UnauthenticatedBrowser.Post(Routing.GetUserNames, ctx => BuildRequest(ctx, new List<Guid>()));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        #endregion GetUserNames

        #region CreateUser

        [Fact]
        public async Task CreateUserReturnsCreatedAsync()
        {
            var response = await UserTokenBrowser.Post(Routing.Users, ctx => BuildRequest(ctx, new CreateUserRequest()));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task CreateUserAsEnterpriseCallsCreateUserAsync()
        {
            await UserTokenBrowser.Post(Routing.Users, ctx => BuildRequest(ctx, CreateUserRequest.Example()));
            _usersControllerMock.Verify(x => x.CreateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<ClaimsPrincipal>()));
        }

        [Fact]
        public async Task CreateUserAsTrialWithServiceTokenReturnsCreatedAsync()
        {
            var user = CreateUserRequest.Example();
            user.UserType = UserType.Trial;

            var response = await ServiceTokenBrowser.Post(Routing.Users, ctx => BuildRequest(ctx, user));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task CreateUserAsTrialWithServiceTokenCallsCreateUserAsync()
        {
            var user = CreateUserRequest.Example();
            user.UserType = UserType.Trial;

            await ServiceTokenBrowser.Post(Routing.Users, ctx => BuildRequest(ctx, user));
            _usersControllerMock.Verify(x => x.CreateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<ClaimsPrincipal>()));
        }

        [Fact]
        public async Task CreateUserAsGuestWithoutAuthenticationReturnsCreatedAsync()
        {
            var user = CreateUserRequest.GuestUserExample();

            var response = await UnauthenticatedBrowser.Post(Routing.Guests, ctx => BuildRequest(ctx, user));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task CreateUserAsGuestCallsCreateGuestAsync()
        {
            var user = CreateUserRequest.GuestUserExample();

            await UnauthenticatedBrowser.Post(Routing.Guests, ctx => BuildRequest(ctx, user));
            _usersControllerMock.Verify(x => x.CreateGuestUserAsync(It.IsAny<CreateUserRequest>()));
        }

        [Fact]
        public async Task CreateUserReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _usersControllerMock.Setup(m => m.CreateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<ClaimsPrincipal>()))
                           .Throws(new Exception());

            var response = await UserTokenBrowser.Post(Routing.Users, ctx => BuildRequest(ctx, CreateUserRequest.Example()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CreateUserWithInvalidRequestReturnsBadRequest()
        {
            var response = await UserTokenBrowser.Post(Routing.Users, ctx => BuildRequest(ctx, "invalid body"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestBindingException, response.ReasonPhrase);
        }

        [Fact]
        public async Task CreateGuestUserReturnsConflictIfUserExistsExceptionIsThrown()
        {
            _usersControllerMock.Setup(m => m.CreateGuestUserAsync(It.IsAny<CreateUserRequest>()))
                .Throws(new UserExistsException());

            var response = await UnauthenticatedBrowser.Post(Routing.Guests, ctx => BuildRequest(ctx, CreateUserRequest.GuestUserExample()));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task CreateUserReturnsBadRequestIfValidationFails()
        {
            _usersControllerMock.Setup(m => m.CreateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<ClaimsPrincipal>()))
                           .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await UserTokenBrowser.Post(Routing.Users, ctx => BuildRequest(ctx, CreateUserRequest.Example()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, response.ReasonPhrase);
        }

        [Fact]
        public async Task CreateUserReturnsInternalServerErrorIfValidationExceptionIsThrown()
        {
            _usersControllerMock.Setup(m => m.CreateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<ClaimsPrincipal>()))
                           .Throws(new ValidationException(new List<ValidationFailure>()));

            var response = await UserTokenBrowser.Post(Routing.Users, ctx => BuildRequest(ctx, CreateUserRequest.Example()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CreateUserReadsTenantIdFromUserClaimAsync()
        {
            var response = await UserTokenBrowser.Post(Routing.Users, ctx => BuildRequest(ctx, CreateUserRequest.Example()));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            _usersControllerMock.Verify(m => m.CreateUserAsync(It.IsAny<CreateUserRequest>(), It.IsAny<ClaimsPrincipal>()));
        }

        #endregion CreateUser

        #region GetUserById

        [Fact]
        public async Task GetUserByIdBasicReturnsOk()
        {
            _usersControllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult(new User()));

            var validUserId = Guid.NewGuid();

            var response = await UserTokenBrowser.Get(string.Format(Routing.UserByIdBasicBase, validUserId), BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetUserByIdBasicReturnsBadRequest()
        {
            _usersControllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var validUserId = Guid.NewGuid();

            var response = await UserTokenBrowser.Get(string.Format(Routing.UserByIdBasicBase, validUserId), BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetUserByIdBasicReturnsUnauthorized()
        {
            _usersControllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                .Returns(Task.FromResult(new User()));

            var validUserId = Guid.NewGuid();

            var response = await UnauthenticatedBrowser.Get(string.Format(Routing.UserByIdBasicBase, validUserId), BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetUserByIdBasicReturnsInternalServerError()
        {
            _usersControllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                .Throws(new Exception());

            var validUserId = Guid.NewGuid();

            var response = await UserTokenBrowser.Get(string.Format(Routing.UserByIdBasicBase, validUserId), BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetUserByIdBasicReturnsNotFoundIfItemDoesNotExist()
        {
            _usersControllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            var validUserId = Guid.NewGuid();

            var response = await UserTokenBrowser.Get(string.Format(Routing.UserByIdBasicBase, validUserId), BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        #endregion GetUserById

        #region GetUsersBasic

        [Fact]
        public async Task GetUsersBasicReturnsForbiddenWhenTenancyHasNotBeenEstablished()
        {
            TenantId = Guid.Empty;

            var response = await UserTokenBrowser.Post(Routing.UsersBasic, ctx => BuildRequest(ctx, new UserFilteringOptions()));

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task GetUsersForBasicReturnsOk()
        {
            _usersControllerMock.Setup(m => m.GetUsersBasicAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<UserFilteringOptions>()))
                .ReturnsAsync(new PagingMetadata<BasicUser>());

            var response = await UserTokenBrowser.Post(Routing.UsersBasic, ctx => BuildRequest(ctx, new UserFilteringOptions()));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        #endregion

        #region GetUsersForTenant

        [Fact]
        public async Task GetUsersForTenantReturnsOk()
        {
            _usersControllerMock.Setup(m => m.GetUsersForTenantAsync(It.IsAny<UserFilteringOptions>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(new PagingMetadata<User>()));

            var response = await UserTokenBrowser.Post(Routing.GetUsers, ctx => BuildRequest(ctx, new UserFilteringOptions()));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetUsersForTenantReturnsNotFound()
        {
            _usersControllerMock.Setup(m => m.GetUsersForTenantAsync(It.IsAny<UserFilteringOptions>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            var response = await UserTokenBrowser.Post(Routing.GetUsers, ctx => BuildRequest(ctx, new UserFilteringOptions()));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetUsersForTenantReturnsBadRequest()
        {
            _usersControllerMock.Setup(m => m.GetUsersForTenantAsync(It.IsAny<UserFilteringOptions>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await UserTokenBrowser.Post(Routing.GetUsers, ctx => BuildRequest(ctx, new UserFilteringOptions()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetUsersForTenantReturnsUnauthorized()
        {
            _usersControllerMock.Setup(m => m.GetUsersForTenantAsync(It.IsAny<UserFilteringOptions>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(new PagingMetadata<User>()));

            var response = await UnauthenticatedBrowser.Post(Routing.GetUsers, ctx => BuildRequest(ctx, new UserFilteringOptions()));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetUsersForTenantReturnsInternalError()
        {
            _usersControllerMock.Setup(m => m.GetUsersForTenantAsync(It.IsAny<UserFilteringOptions>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new Exception());

            var response = await UserTokenBrowser.Post(Routing.GetUsers, ctx => BuildRequest(ctx, new UserFilteringOptions()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        #endregion GetUsersForTenant

        #region PromoteGuest

        [Fact]
        public async Task PromoteGuest_WithNoBearer_ReturnsUnauthorized()
        {
            var response = await UnauthenticatedBrowser.Post(string.Format(Routing.PromoteGuestBase, "C3220603-09D9-452B-B204-6CC3946CE1F4"), ctx => BuildRequest(ctx, LicenseType.UserLicense));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task PromoteGuest_WithValidRequest_ReturnsOk()
        {
            var response = await UserTokenBrowser.Post(string.Format(Routing.PromoteGuestBase, "C3220603-09D9-452B-B204-6CC3946CE1F4"), ctx => BuildRequest(ctx, LicenseType.UserLicense));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task PromoteGuest_IfUnhandledExceptionIsThrown_ReturnsInternalServerError()
        {
            _usersControllerMock
                .Setup(uc => uc.PromoteGuestUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LicenseType>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<bool>()))
                .Throws(new Exception());

            var response = await UserTokenBrowser.Post(string.Format(Routing.PromoteGuestBase, "C3220603-09D9-452B-B204-6CC3946CE1F4"), ctx => BuildRequest(ctx, LicenseType.UserLicense));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task PromoteGuest_WithInvalidBody_ReturnsBadRequest()
        {
            var response = await UserTokenBrowser.Post(string.Format(Routing.PromoteGuestBase, "C3220603-09D9-452B-B204-6CC3946CE1F4"), ctx => BuildRequest(ctx, "invalid body"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestBindingException, response.ReasonPhrase);
        }

        [Fact]
        public async Task PromoteGuest_IfValidationFails_ReturnsBadRequest()
        {
            _usersControllerMock
                .Setup(uc => uc.PromoteGuestUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LicenseType>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<bool>()))
                           .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await UserTokenBrowser.Post(string.Format(Routing.PromoteGuestBase, "C3220603-09D9-452B-B204-6CC3946CE1F4"), ctx => BuildRequest(ctx, LicenseType.UserLicense));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, response.ReasonPhrase);
        }

        [Fact]
        public async Task PromoteGuest_IfUserNotFound_ReturnsBadRequest()
        {
            _usersControllerMock
                .Setup(uc => uc.PromoteGuestUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LicenseType>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<bool>()))
                .Throws(new NotFoundException());

            var response = await UserTokenBrowser.Post(string.Format(Routing.PromoteGuestBase, "C3220603-09D9-452B-B204-6CC3946CE1F4"), ctx => BuildRequest(ctx, LicenseType.UserLicense));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task PromoteGuest_IfLicenseNotAvailableExceptionIsThrown_ReturnsFailedToAssignLicense()
        {
            _usersControllerMock
                .Setup(uc => uc.PromoteGuestUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LicenseType>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<bool>()))
                .Throws(new LicenseNotAvailableException(""));

            var response = await UserTokenBrowser.Post(string.Format(Routing.PromoteGuestBase, "C3220603-09D9-452B-B204-6CC3946CE1F4"), ctx => BuildRequest(ctx, LicenseType.UserLicense));

            var payload = response.Body.DeserializeJson<PromoteGuestResponse>();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(PromoteGuestResultCode.FailedToAssignLicense, payload.ResultCode);
        }

        [Fact]
        public async Task PromoteGuest_IfUserAlreadyMemberOfTenantExceptionIsThrown_ReturnsUserAlreadyPromoted()
        {
            _usersControllerMock
                .Setup(uc => uc.PromoteGuestUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LicenseType>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<bool>()))
                .Throws(new UserAlreadyMemberOfTenantException(""));

            var response = await UserTokenBrowser.Post(string.Format(Routing.PromoteGuestBase, "C3220603-09D9-452B-B204-6CC3946CE1F4"), ctx => BuildRequest(ctx, LicenseType.UserLicense));

            var payload = response.Body.DeserializeJson<PromoteGuestResponse>();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(PromoteGuestResultCode.UserAlreadyPromoted, payload.ResultCode);
        }

        [Fact]
        public async Task PromoteGuest_IfEmailNotInTenantDomainExceptionIsThrown_ReturnsFailed()
        {
            _usersControllerMock
                .Setup(uc => uc.PromoteGuestUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LicenseType>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<bool>()))
                .Throws(new EmailNotInTenantDomainException(""));

            var response = await UserTokenBrowser.Post(string.Format(Routing.PromoteGuestBase, "C3220603-09D9-452B-B204-6CC3946CE1F4"), ctx => BuildRequest(ctx, LicenseType.UserLicense));

            var payload = response.Body.DeserializeJson<PromoteGuestResponse>();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(PromoteGuestResultCode.Failed, payload.ResultCode);
        }

        [Fact]
        public async Task PromoteGuest_IfAssignUserToTenantExceptionIsThrown_ReturnsFailed()
        {
            _usersControllerMock
                .Setup(uc => uc.PromoteGuestUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LicenseType>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<bool>()))
                .Throws(new AssignUserToTenantException(""));

            var response = await UserTokenBrowser.Post(string.Format(Routing.PromoteGuestBase, "C3220603-09D9-452B-B204-6CC3946CE1F4"), ctx => BuildRequest(ctx, LicenseType.UserLicense));

            var payload = response.Body.DeserializeJson<PromoteGuestResponse>();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(PromoteGuestResultCode.Failed, payload.ResultCode);
        }

        [Fact]
        public async Task PromoteGuest_IfLicenseAssignmentFailedExceptionIsThrown_ReturnsFailedToAssignLicense()
        {
            _usersControllerMock
                .Setup(uc => uc.PromoteGuestUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<LicenseType>(), It.IsAny<ClaimsPrincipal>(), It.IsAny<bool>()))
                .Throws(new LicenseAssignmentFailedException(""));

            var response = await UserTokenBrowser.Post(string.Format(Routing.PromoteGuestBase, "C3220603-09D9-452B-B204-6CC3946CE1F4"), ctx => BuildRequest(ctx, LicenseType.UserLicense));

            var payload = response.Body.DeserializeJson<PromoteGuestResponse>();
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(PromoteGuestResultCode.FailedToAssignLicense, payload.ResultCode);
        }

        [Fact]
        public async Task PromoteGuest_ReadsTenantIdFromUserClaim()
        {
            const LicenseType expectedLicense = LicenseType.UserLicense;
            var expectedId = Guid.NewGuid();
            var request = LicenseType.UserLicense;

            var response = await UserTokenBrowser.Post(string.Format(Routing.PromoteGuestBase, expectedId), ctx => BuildRequest(ctx, request));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            _usersControllerMock.Verify(m => m.PromoteGuestUserAsync(expectedId, TenantId, expectedLicense, It.IsAny<ClaimsPrincipal>(), false));
        }

        #endregion PromoteGuest

        #region UpdateUser

        [Fact]
        public async Task UpdateUserReturnsOk()
        {
            _usersControllerMock.Setup(m => m.UpdateUserAsync(It.IsAny<Guid>(), It.IsAny<User>(), It.IsAny<Guid>(), It.IsAny<ClaimsPrincipal>()))
                           .ReturnsAsync(new User());

            Guid.TryParse("DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3", out var createdBy);

            _usersControllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new User { CreatedBy = createdBy });

            var response = await UserTokenBrowser.Put(string.Format(Routing.UsersWithItemBase, Guid.NewGuid()), ctx => BuildRequest(ctx, new User()));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task UpdateUserReturnsUnauthorized()
        {
            var response = await UnauthenticatedBrowser.Put(string.Format(Routing.UsersWithItemBase, Guid.NewGuid()), ctx => BuildRequest(ctx, new User()));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task UpdateUserReturnsNotFound()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var currentUserId);

            _usersControllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new User { Id = currentUserId });

            _usersControllerMock.Setup(m => m.UpdateUserAsync(It.IsAny<Guid>(), It.IsAny<User>(), It.IsAny<Guid>(), It.IsAny<ClaimsPrincipal>()))
                           .Throws(new NotFoundException("user not found"));

            var response = await UserTokenBrowser.Put(string.Format(Routing.UsersWithItemBase, currentUserId), ctx => BuildRequest(ctx, new User()));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task UpdateUserReturnsBadRequestDueToBindingException()
        {
            var response = await UserTokenBrowser.Put(string.Format(Routing.UsersWithItemBase, Guid.NewGuid()), ctx => BuildRequest(ctx, "invlaid body"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task UpdateUserReturnsInternalServerError()
        {
            _usersControllerMock.Setup(m => m.UpdateUserAsync(It.IsAny<Guid>(), It.IsAny<User>(), It.IsAny<Guid>(), It.IsAny<ClaimsPrincipal>()))
                           .Throws(new Exception());

            Guid.TryParse("DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3", out var createdBy);

            _usersControllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new User { CreatedBy = createdBy });

            var response = await UserTokenBrowser.Put(string.Format(Routing.UsersWithItemBase, Guid.NewGuid()), ctx => BuildRequest(ctx, new User()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        #endregion UpdateUser

        #region LockUser

        [Fact]
        public async Task LockUserReturnsSuccess()
        {
            _usersControllerMock
                .Setup(uc => uc.LockOrUnlockUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>()))
                .ReturnsAsync(true);

            var response = await UserTokenBrowser.Post(string.Format(Routing.LockUserBase, "f629f87c-366d-4790-ac34-964e3558bdcd"), ctx => BuildRequest(ctx, new User { IsLocked = true }));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task LockUserReturnsBadRequest()
        {
            _usersControllerMock
                .Setup(uc => uc.LockOrUnlockUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>()))
                .ReturnsAsync(true);

            var response = await UserTokenBrowser.Post(string.Format(Routing.LockUserBase, "f629f87c-366d-4790-ac34-964e3558bdcd"), ctx => BuildRequest(ctx, "invalid body"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task LockUserReturnsValidationException()
        {
            _usersControllerMock
                .Setup(uc => uc.LockOrUnlockUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await UserTokenBrowser.Post(string.Format(Routing.LockUserBase, "f629f87c-366d-4790-ac34-964e3558bdcd"), ctx => BuildRequest(ctx, new User()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task LockUserReturnsInternalServerError()
        {
            _usersControllerMock
                .Setup(uc => uc.LockOrUnlockUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<bool>()))
                .Throws(new Exception());

            var response = await UserTokenBrowser.Post(string.Format(Routing.LockUserBase, "f629f87c-366d-4790-ac34-964e3558bdcd"), ctx => BuildRequest(ctx, new User()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        #endregion LockUser

        #region CanPromoteUser

        [Fact]
        public async Task CanPromoteuserReturnsSuccess()
        {
            _usersControllerMock.Setup(m => m.CanPromoteUserAsync(It.IsAny<string>(), It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new CanPromoteUser()));

            var response = await UserTokenBrowser.Get($"{Routing.PromoteUser}", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task CanPromoteUser_ReturnsBadrequest()
        {
            _usersControllerMock.Setup(m => m.CanPromoteUserAsync(It.IsAny<string>(), It.IsAny<Guid>()))
                           .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await UserTokenBrowser.Get($"{Routing.PromoteUser}", BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task CanPromoteuserReturnsInternalServerError()
        {
            _usersControllerMock.Setup(m => m.CanPromoteUserAsync(It.IsAny<string>(), It.IsAny<Guid>()))
                           .Throws(new Exception());

            var response = await UserTokenBrowser.Get(string.Format($"{Routing.PromoteUser}", ValidTestEmail), BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CanPromoteuserReturnsUserNotFound()
        {
            _usersControllerMock.Setup(m => m.CanPromoteUserAsync(It.IsAny<string>(), It.IsAny<Guid>()))
                           .Throws(new NotFoundException("User Doesn't Exist"));

            var response = await UserTokenBrowser.Get(string.Format($"{Routing.PromoteUser}?email={ValidTestEmail}", ValidTestEmail), BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        #endregion CanPromoteUser

        #region CreateUserGroup

        [Fact]
        public async Task CreateUserGroupReturnsCreated()
        {
            var response = await UserTokenBrowser.Post(Routing.UserGroups, ctx => BuildRequest(ctx, new UserGroup()));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task CreateUserGroupReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _usersControllerMock.Setup(m => m.CreateUserGroupAsync(It.IsAny<UserGroup>(), It.IsAny<Guid>()))
                            .Throws(new Exception());

            var response = await UserTokenBrowser.Post(Routing.UserGroups, ctx => BuildRequest(ctx, new UserGroup()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CreateUserGroupReturnsItemWithInvalidBodyReturnsBadRequest()
        {
            var response = await UserTokenBrowser.Post(Routing.UserGroups, ctx => BuildRequest(ctx, "invalid body"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestBindingException, response.ReasonPhrase);
        }

        [Fact]
        public async Task CreateUserGroupReturnsBadRequestIfValidationFails()
        {
            _usersControllerMock.Setup(m => m.CreateUserGroupAsync(It.IsAny<UserGroup>(), It.IsAny<Guid>()))
                           .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await UserTokenBrowser.Post(Routing.UserGroups, ctx => BuildRequest(ctx, new UserGroup()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, response.ReasonPhrase);
        }

        #endregion CreateUserGroup

        #region GetUsersForGroup

        [Fact]
        [Trait("User Group", "User Group Tests")]
        public async Task GetUsersForGroupReturnFound()
        {
            var validGroupId = Guid.NewGuid();

            _usersControllerMock.Setup(m => m.GetUserIdsByGroupIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new List<Guid>()));

            var response = await UserTokenBrowser.Get(string.Format(Routing.UserIdsByGroupIdBase, validGroupId), BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        [Trait("User Group", "User Group Tests")]
        public async Task GetUsersForGroupReturnNotFoundException()
        {
            var validGroupId = Guid.NewGuid();

            _usersControllerMock.Setup(m => m.GetUserIdsByGroupIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new NotFoundException(string.Empty));

            var response = await UserTokenBrowser.Get($"{Routing.UserGroups}/{validGroupId}", BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        [Trait("User Group", "User Group Tests")]
        public async Task GetUsersForGroupReturnValidationException()
        {
            var validGroupId = Guid.NewGuid();

            _usersControllerMock.Setup(m => m.GetUserIdsByGroupIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new ValidationFailedException(Enumerable.Empty<ValidationFailure>()));

            var response = await UserTokenBrowser.Get(string.Format(Routing.UserIdsByGroupIdBase, validGroupId), BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        [Trait("User Group", "User Group Tests")]
        public async Task GetUsersForGroupReturnUnAuthorized()
        {
            var validGroupId = Guid.NewGuid();

            _usersControllerMock.Setup(m => m.GetUserIdsByGroupIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new List<Guid>()));

            var response = await UnauthenticatedBrowser.Get(string.Format(Routing.UserIdsByGroupIdBase, validGroupId), BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        [Trait("User Group", "User Group Tests")]
        public async Task GetUsersForGroupReturnInternalServerError()
        {
            var validGroupId = Guid.NewGuid();

            _usersControllerMock.Setup(m => m.GetUserIdsByGroupIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new Exception());

            var response = await UserTokenBrowser.Get(string.Format(Routing.UserIdsByGroupIdBase, validGroupId), BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        #endregion GetUsersForGroup

        #region GetGroupsForUser

        [Fact]
        public async Task GetGroupsForUserReturnsOk()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var currentUserId);

            _usersControllerMock.Setup(m => m.GetGroupIdsByUserIdAsync(It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new List<Guid>()));

            _usersControllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new User { Id = currentUserId });

            var response = await UserTokenBrowser.Get(string.Format(Routing.GroupIdsByUserIdBase, currentUserId), BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupsForUserReturnsBadRequestDueToValidationException()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var currentUserId);

            _usersControllerMock.Setup(m => m.GetGroupIdsByUserIdAsync(It.IsAny<Guid>()))
                           .ThrowsAsync(new ValidationFailedException(new List<ValidationFailure>()));

            _usersControllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new User { Id = currentUserId });

            var response = await UserTokenBrowser.Get(string.Format(Routing.GroupIdsByUserIdBase, currentUserId), BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupsForUserReturnsInternalServerError()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var currentUserId);

            _usersControllerMock.Setup(m => m.GetGroupIdsByUserIdAsync(It.IsAny<Guid>()))
                           .ThrowsAsync(new Exception());

            _usersControllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new User { Id = currentUserId });

            var response = await UserTokenBrowser.Get(string.Format(Routing.GroupIdsByUserIdBase, currentUserId), BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupsForUserReturnsUnauthorizedValidUserLevelAccess()
        {
            var currentUserId = Guid.NewGuid();

            _usersControllerMock.Setup(m => m.GetGroupIdsByUserIdAsync(It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new List<Guid>()));

            _usersControllerMock.Setup(m => m.GetUserAsync(It.IsAny<Guid>()))
                           .ReturnsAsync(new User { Id = currentUserId });

            var response = await UnauthenticatedBrowser.Get(string.Format(Routing.GroupIdsByUserIdBase, currentUserId), BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupsForUserReturnsUnauthorized()
        {
            var userId = Guid.NewGuid();

            var response = await UnauthenticatedBrowser.Get(string.Format(Routing.GroupIdsByUserIdBase, userId), BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupsForUserReturnNotFound()
        {
            var userId = Guid.NewGuid();

            _usersControllerMock.Setup(m => m.GetGroupIdsByUserIdAsync(It.IsAny<Guid>()))
                           .Throws(new NotFoundException("Record not found"));

            //_userRepositoryMock.Setup(m => m.GetItemsAsync(u => u.Id == userId))
            //                   .Throws(new Exception());

            var response = await UserTokenBrowser.Get(string.Format(Routing.GroupIdsByUserIdBase, userId), BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        #endregion GetGroupsForUser

        #region GetGuestUsersForTenant

        [Fact]
        public async Task GetGuestUsersForTenantSuccess()
        {
            var response = await UserTokenBrowser.Post(Routing.GuestUsersForTenant, ctx => BuildRequest(ctx, new UserFilteringOptions()));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestUsersForTenantReturnsUnauthorized()
        {
            var response = await UnauthenticatedBrowser.Post(Routing.GuestUsersForTenant, ctx => BuildRequest(ctx, new UserFilteringOptions()));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetGuestUsersForTenantReturnsInternalServerError()
        {
            _usersControllerMock.Setup(m => m.GetGuestUsersForTenantAsync(It.IsAny<Guid>(), It.IsAny<UserFilteringOptions>()))
                           .ThrowsAsync(new Exception());

            var response = await UserTokenBrowser.Post(Routing.GuestUsersForTenant, ctx => BuildRequest(ctx, new UserFilteringOptions()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        #endregion GetGuestUsersForTenant

        #region AutoProvisionRefreshGroups

        [Fact]
        public async Task AutoProvisionRefreshGroupsReturnUser()
        {
            var response = await UserTokenBrowser.Post(Routing.AutoProvisionRefreshGroups, ctx => BuildRequest(ctx, new IdpUserRequest()));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task AutoProvisionRefreshGroupsReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _usersControllerMock.Setup(m => m.AutoProvisionRefreshGroupsAsync(It.IsAny<IdpUserRequest>(), It.IsAny<Guid>(), It.IsAny<ClaimsPrincipal>()))
                           .Throws(new Exception());

            var response = await UserTokenBrowser.Post(Routing.AutoProvisionRefreshGroups, ctx => BuildRequest(ctx, new IdpUserRequest()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task AutoProvisionRefreshGroupsWithInvalidBodyReturnsBadRequest()
        {
            _usersControllerMock.Setup(m => m.AutoProvisionRefreshGroupsAsync(It.IsAny<IdpUserRequest>(), It.IsAny<Guid>(), It.IsAny<ClaimsPrincipal>()))
                           .Returns(Task.FromResult(new User()));

            var response = await UserTokenBrowser.Post(Routing.AutoProvisionRefreshGroups, ctx => BuildRequest(ctx, "invalid idp request"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestBindingException, response.ReasonPhrase);
        }

        #endregion AutoProvisionRefreshGroups

        #region RemoveUserfromPermissionGroup

        [Fact]
        public async Task RemoveUserfromPermissionGroupReturnsSuccess()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var userId);

            _usersControllerMock.Setup(m => m.RemoveUserFromPermissionGroupAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Returns(Task.FromResult(true));

            var response = await UserTokenBrowser.Delete(string.Format(Routing.RemoveUserFromPermissionGroupBase, Guid.NewGuid(), userId), BuildRequest);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task RemoveUserfromPermissionGroupReturnsNotFound()
        {
            _usersControllerMock.Setup(m => m.RemoveUserFromPermissionGroupAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .ThrowsAsync(new DocumentNotFoundException("Couldn't find the user"));

            var response = await UserTokenBrowser.Delete(string.Format(Routing.RemoveUserFromPermissionGroupBase, Guid.NewGuid(), Guid.NewGuid()), BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task RemoveUserfromPermissionGroupReturnsBadRequestDueToValidationFailure()
        {
            _usersControllerMock.Setup(m => m.RemoveUserFromPermissionGroupAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .ThrowsAsync(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await UserTokenBrowser.Delete(string.Format(Routing.RemoveUserFromPermissionGroupBase, Guid.NewGuid(), Guid.NewGuid()), BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task RemoveUserfromPermissionGroupReturnsBadRequestDueToBindFailure()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var userId);

            var response = await UserTokenBrowser.Delete(string.Format(Routing.RemoveUserFromPermissionGroupBase, Guid.NewGuid(), userId), ctx => BuildRequest(ctx, "invalid data"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task RemoveUserfromPermissionGroupReturnsInternalServerError()
        {
            Guid.TryParse("16367A84-65E7-423C-B2A5-5C42F8F1D5F2", out var userId);

            _usersControllerMock.Setup(m => m.RemoveUserFromPermissionGroupAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .ThrowsAsync(new Exception());

            var response = await UserTokenBrowser.Delete(string.Format(Routing.RemoveUserFromPermissionGroupBase, Guid.NewGuid(), userId), BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        #endregion RemoveUserfromPermissionGroup

        #region GetUsersByIds

        [Fact]
        public async Task GetUsersByIdsReturnsOkIfSuccessful()
        {
            _usersControllerMock.Setup(m => m.GetUsersByIdsAsync(It.IsAny<List<Guid>>()))
                .ReturnsAsync(new List<User>());

            var response = await UserTokenBrowser.Post(Routing.GetUsersByIds, ctx => BuildRequest(ctx, new List<Guid>()));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetUsersByIdsReturnsBadRequestIfValidationFails()
        {
            _usersControllerMock.Setup(m => m.GetUsersByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                           .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await UserTokenBrowser.Post(Routing.GetUsersByIds, ctx => BuildRequest(ctx, new List<Guid>()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetUsersByIdsReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _usersControllerMock.Setup(m => m.GetUsersByIdsAsync(It.IsAny<IEnumerable<Guid>>()))
                           .Throws(new Exception());

            var response = await UserTokenBrowser.Post(Routing.GetUsersByIds, ctx => BuildRequest(ctx, new List<Guid>()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        #endregion GetUsersByIds

        #region GetLicenseTypeForUser

        [Trait("GetLicenseTypeForUser", "Get License Type For User")]
        [Fact]
        public async Task GetLicenseTypeForUserReturnsOk()
        {
            _usersControllerMock.Setup(m => m.GetLicenseTypeForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .ReturnsAsync(LicenseType.Default);

            var userId = Guid.NewGuid();

            var response = await UserTokenBrowser.Get(string.Format(Routing.LicenseTypeForUserBase, userId), BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Trait("GetLicenseTypeForUser", "Get License Type For User")]
        [Fact]
        public async Task GetLicenseTypeForUserReturnsNotFoundIfUserDoesNotExist()
        {
            _usersControllerMock.Setup(m => m.GetLicenseTypeForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new NotFoundException(string.Empty));

            var userId = Guid.NewGuid();

            var response = await UserTokenBrowser.Get(string.Format(Routing.LicenseTypeForUserBase, userId), BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Trait("GetLicenseTypeForUser", "Get License Type For User")]
        [Fact]
        public async Task GetLicenseTypeForUserReturnsUnAuthorized()
        {
            _usersControllerMock.Setup(m => m.GetLicenseTypeForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new InvalidOperationException());

            var userId = Guid.NewGuid();

            var response = await UserTokenBrowser.Get(string.Format(Routing.LicenseTypeForUserBase, userId), BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Trait("GetLicenseTypeForUser", "Get License Type For User")]
        [Fact]
        public async Task GetLicenseTypeForUserReturnsInernalServerError()
        {
            _usersControllerMock.Setup(m => m.GetLicenseTypeForUserAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new Exception());

            var userId = Guid.NewGuid();

            var response = await UserTokenBrowser.Get(string.Format(Routing.LicenseTypeForUserBase, userId), BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        #endregion GetLicenseTypeForUser

        #region SendGuestVerificationEmailAsync

        [Fact]
        public async Task SendGuestVerificationEmailAsyncReturnsOk()
        {
            _usersControllerMock.Setup(m => m.SendGuestVerificationEmailAsync(It.IsAny<GuestVerificationEmailRequest>()))
                .Returns(Task.FromResult(GuestVerificationEmailRequest.Example()));

            var response = await ServiceTokenBrowser.Post(Routing.SendVerificationEmail, ctx => BuildRequest(ctx, new UserFilteringOptions()));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task SendGuestVerificationEmailAsyncReturnsBadRequest()
        {
            _usersControllerMock.Setup(m => m.SendGuestVerificationEmailAsync(It.IsAny<GuestVerificationEmailRequest>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await ServiceTokenBrowser.Post(Routing.SendVerificationEmail, ctx => BuildRequest(ctx, new UserFilteringOptions()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task SendGuestVerificationEmailAsyncReturnsUnauthorized()
        {
            _usersControllerMock.Setup(m => m.SendGuestVerificationEmailAsync(It.IsAny<GuestVerificationEmailRequest>()))
                .Returns(Task.FromResult(GuestVerificationEmailRequest.Example()));

            var response = await UnauthenticatedBrowser.Post(Routing.SendVerificationEmail, ctx => BuildRequest(ctx, new UserFilteringOptions()));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task SendGuestVerificationEmailAsyncReturnsAlreadyVerified()
        {
            _usersControllerMock.Setup(m => m.SendGuestVerificationEmailAsync(It.IsAny<GuestVerificationEmailRequest>()))
                .Throws(new EmailAlreadyVerifiedException());

            var response = await ServiceTokenBrowser.Post(Routing.SendVerificationEmail, ctx => BuildRequest(ctx, new UserFilteringOptions()));

            Assert.Equal(HttpStatusCode.FailedDependency, response.StatusCode);
        }

        [Fact]
        public async Task SendGuestVerificationEmailAsyncReturnsRecentlySent()
        {
            _usersControllerMock.Setup(m => m.SendGuestVerificationEmailAsync(It.IsAny<GuestVerificationEmailRequest>()))
                .Throws(new EmailRecentlySentException());

            var response = await ServiceTokenBrowser.Post(Routing.SendVerificationEmail, ctx => BuildRequest(ctx, new UserFilteringOptions()));

            Assert.Equal(HttpStatusCode.FailedDependency, response.StatusCode);
        }

        [Fact]
        public async Task SendGuestVerificationEmailAsyncReturnsInternalServerError()
        {
            _usersControllerMock.Setup(m => m.SendGuestVerificationEmailAsync(It.IsAny<GuestVerificationEmailRequest>()))
                .Throws(new Exception());

            var response = await ServiceTokenBrowser.Post(Routing.SendVerificationEmail, ctx => BuildRequest(ctx, new UserFilteringOptions()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        #endregion SendGuestVerificationEmailAsync
    }
}