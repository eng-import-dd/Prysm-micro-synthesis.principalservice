using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Nancy;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.Entity;
using Synthesis.PrincipalService.Modules;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Responses;
using Xunit;

namespace Synthesis.Principal.Modules.Test.Modules
{
    public class UserInviteModuleTests : BaseModuleTests<UserInviteModule>
    {
        /// <inheritdoc />
        protected override List<object> BrowserDependencies { get; }
        private readonly Mock<IUserInvitesController> _controllerMock = new Mock<IUserInvitesController>();

        public UserInviteModuleTests()
        {
            BrowserDependencies = new List<object> { _controllerMock.Object };
        }

        [Fact]
        public async void CreateUserInviteReturnCreated()
        {
            _controllerMock.Setup(m => m.CreateUserInviteListAsync(It.IsAny<List<UserInviteRequest>>(), It.IsAny<Guid>()))
                .ReturnsAsync(new List<UserInviteResponse>());

            var response = await UserTokenBrowser.Post("/v1/userinvites", ctx => BuildRequest(ctx, new List<UserInviteRequest>()));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async void CreateUserInviteReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.CreateUserInviteListAsync(It.IsAny<List<UserInviteRequest>>(), It.IsAny<Guid>()))
                .ThrowsAsync(new Exception());

            var response = await UserTokenBrowser.Post("/v1/userinvites", ctx => BuildRequest(ctx, new List<UserInviteRequest>()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async void GetInvitedUsersForTenantReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.GetUsersInvitedForTenantAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .ThrowsAsync(new Exception());

            var response = await UserTokenBrowser.Get("/v1/userinvites", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetInvitedUsersForTenantReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(m => m.GetUsersInvitedForTenantAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .Throws(new Exception());

            var response = await UserTokenBrowser.Get("/v1/userinvites", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async void GetInvitedUsersForTenantReturnsOk()
        {
            _controllerMock.Setup(m => m.GetUsersInvitedForTenantAsync(It.IsAny<Guid>(), It.IsAny<bool>()))
                .Returns(Task.FromResult(new PagingMetadata<UserInviteResponse>()));

            var response = await UserTokenBrowser.Get("/v1/userinvites", BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async void ResendUserInviteReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.ResendEmailInviteAsync(It.IsAny<List<UserInviteRequest>>(), It.IsAny<Guid>()))
                .ThrowsAsync(new Exception());

            var response = await UserTokenBrowser.Post("/v1/userinvites/resend", ctx => BuildRequest(ctx, new List<UserInviteRequest>()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async void ResendUserInviteReturnsRespondWithUnauthorizedNoBearer()
        {
            var response = await UnauthenticatedBrowser.Post("/v1/userinvites/resend", ctx => BuildRequest(ctx, new List<UserInviteRequest>()));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async void ResendUserInviteReturnsSuccess()
        {
            var response = await UserTokenBrowser.Post("/v1/userinvites/resend", ctx => BuildRequest(ctx, new List<UserInviteRequest>()));

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async void RespondWithUnauthorizedNoBearer()
        {
            var response = await UnauthenticatedBrowser.Post("/v1/userinvites", ctx => BuildRequest(ctx, new UserInviteRequest()));

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}