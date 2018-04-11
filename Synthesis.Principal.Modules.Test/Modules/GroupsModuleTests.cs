using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentValidation.Results;
using Moq;
using Nancy;
using Synthesis.Nancy.MicroService;
using Synthesis.Nancy.MicroService.Constants;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Controllers;
using Synthesis.PrincipalService.InternalApi.Constants;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Models;
using Synthesis.PrincipalService.Modules;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Modules
{
    public class GroupsModuleTests : BaseModuleTests<GroupsModule>
    {
        /// <inheritdoc />
        protected override List<object> BrowserDependencies { get; }
        private readonly Mock<IGroupsController> _controllerMock = new Mock<IGroupsController>();


        public GroupsModuleTests()
        {
            BrowserDependencies = new List<object> { _controllerMock.Object };
        }

        [Fact]
        public async Task CreateGroupReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.CreateGroupAsync(It.IsAny<Group>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await UserTokenBrowser.Post(Routing.Groups, ctx => BuildRequest(ctx, new Group()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, response.ReasonPhrase);
        }

        [Fact]
        public async Task CreateGroupReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(m => m.CreateGroupAsync(It.IsAny<Group>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new Exception());

            var response = await UserTokenBrowser.Post(Routing.Groups, ctx => BuildRequest(ctx, new Group()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task CreateGroupReturnsItemWithInvalidBodyReturnsBadRequest()
        {
            var response = await UserTokenBrowser.Post(Routing.Groups, ctx => BuildRequest(ctx, "invalid body"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestBindingException, response.ReasonPhrase);
        }

        [Fact]
        public async Task CreateGroupReturnsOk()
        {
            var response = await UserTokenBrowser.Post(Routing.Groups, ctx => BuildRequest(ctx, new Group()));
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }

        [Fact]
        public async Task DeleteGroupReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.DeleteGroupAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var groupId = Guid.NewGuid();

            var response = await UserTokenBrowser.Delete(string.Format(Routing.GroupsWithIdBase, groupId), BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, response.ReasonPhrase);
        }

        [Fact]
        public async Task DeleteGroupReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(m => m.DeleteGroupAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new Exception());

            var groupId = Guid.NewGuid();

            var response = await UserTokenBrowser.Delete(string.Format(Routing.GroupsWithIdBase, groupId), BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task DeleteGroupReturnsNoContent()
        {
            var groupId = Guid.NewGuid();

            var response = await UserTokenBrowser.Delete(string.Format(Routing.GroupsWithIdBase, groupId), BuildRequest);

            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupByIdReturnsBadRequest()
        {
            _controllerMock.Setup(m => m.GetGroupByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var validGroupId = Guid.NewGuid();

            var response = await UserTokenBrowser.Get(string.Format(Routing.GroupsWithIdBase, validGroupId), BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupByIdReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.GetGroupByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new Exception());

            var validGroupId = Guid.NewGuid();

            var response = await UserTokenBrowser.Get(string.Format(Routing.GroupsWithIdBase, validGroupId), BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupByIdReturnsNotFoundIfItemDoesNotExist()
        {
            _controllerMock.Setup(m => m.GetGroupByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            var validGroupId = Guid.NewGuid();

            var response = await UserTokenBrowser.Get(string.Format(Routing.GroupsWithIdBase, validGroupId), BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupByIdReturnsOk()
        {
            _controllerMock.Setup(m => m.GetGroupByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(new Group()));

            var validGroupId = Guid.NewGuid();

            var response = await UserTokenBrowser.Get(string.Format(Routing.GroupsWithIdBase, validGroupId), BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupByIdReturnsUnauthorized()
        {
            _controllerMock.Setup(m => m.GetGroupByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(new Group()));

            var validGroupId = Guid.NewGuid();

            var response = await UnauthenticatedBrowser.Get(string.Format(Routing.GroupsWithIdBase, validGroupId), BuildRequest);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupByIdReturnsValidationFailedException()
        {
            var errors = Enumerable.Empty<ValidationFailure>();
            _controllerMock.Setup(m => m.GetGroupByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(errors));

            var validGroupId = Guid.NewGuid();

            var response = await UserTokenBrowser.Get(string.Format(Routing.GroupsWithIdBase, validGroupId), BuildRequest);

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Trait("GetGroupsForTenant", "Get Groups For Tenant Test Cases")]
        [Fact]
        public async Task GetGroupsForTenantReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.GetGroupsForTenantAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new Exception());

            var response = await UserTokenBrowser.Get("/v1/groups", BuildRequest);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Trait("GetGroupsForTenant", "Get Groups For Tenant Test Cases")]
        [Fact]
        public async Task GetGroupsForTenantReturnsNotFoundIfItemDoesNotExist()
        {
            _controllerMock.Setup(m => m.GetGroupsForTenantAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new NotFoundException(string.Empty));

            var response = await UserTokenBrowser.Get(Routing.Groups, BuildRequest);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Trait("GetGroupsForTenant", "Get Groups For Tenant Test Cases")]
        [Fact]
        public async Task GetGroupsForTenantReturnsOk()
        {
            _controllerMock.Setup(m => m.GetGroupsForTenantAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Returns(Task.FromResult(Enumerable.Empty<Group>()));

            var response = await UserTokenBrowser.Get(Routing.Groups, BuildRequest);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public async Task UpdateGroupReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.UpdateGroupAsync(It.IsAny<Group>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var response = await UserTokenBrowser.Put(Routing.Groups, ctx => BuildRequest(ctx, new Group()));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, response.ReasonPhrase);
        }

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public async Task UpdateGroupReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(m => m.UpdateGroupAsync(It.IsAny<Group>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                .Throws(new Exception());

            var response = await UserTokenBrowser.Put(Routing.Groups, ctx => BuildRequest(ctx, new Group()));

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public async Task UpdateGroupReturnsItemWithInvalidBodyReturnsBadRequest()
        {
            var response = await UserTokenBrowser.Put(Routing.Groups, ctx => BuildRequest(ctx, "invalid body"));

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(ResponseText.BadRequestBindingException, response.ReasonPhrase);
        }

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public async Task UpdateGroupReturnsOk()
        {
            var response = await UserTokenBrowser.Put(Routing.Groups, ctx => BuildRequest(ctx, new Group()));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}