using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
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
using Synthesis.Logging;
using Synthesis.Nancy.MicroService.Constants;
using Synthesis.Nancy.MicroService.Metadata;
using Synthesis.Nancy.MicroService.Serialization;
using Synthesis.Nancy.MicroService.Validation;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Utilities;
using Synthesis.PrincipalService.Workflow.Controllers;

using Xunit;
using ClaimTypes = System.Security.Claims.ClaimTypes;
using Synthesis.Nancy.MicroService;

namespace Synthesis.PrincipalService.Modules.Test.Modules
{
    /// <summary>
    /// Groups Module Unit Test Cases class.
    /// </summary>
    [SuppressMessage("ReSharper", "ExplicitCallerInfoArgument")]
    public class GroupsModuleTest
    {
        private readonly Browser _browserAuth;
        private readonly Browser _browserNoAuth;

        private readonly Mock<IGroupsController> _controllerMock = new Mock<IGroupsController>();

        /// <summary>
        /// Initializes a new instance of the <see cref="GroupsModuleTest"/> class.
        /// </summary>
        public GroupsModuleTest()
        {
            _browserAuth = BrowserWithRequestStartup((container, pipelines, context) =>
                                                     {
                                                         context.CurrentUser = new ClaimsPrincipal(new ClaimsIdentity(new[]
                                                                                                                      {
                                                                                                                          new Claim(ClaimTypes.Name, "TestUser"),
                                                                                                                          new Claim(ClaimTypes.Email, "test@user.com"),
                                                                                                                          new Claim("TenantId", "DBAE315B-6ABF-4A8B-886E-C9CC0E1D16B3"),
                                                                                                                          new Claim("UserId", "16367A84-65E7-423C-B2A5-5C42F8F1D5F2")
                                                                                                                      },
                                                                                                                      AuthenticationTypes.Basic));
                                                     });
            _browserNoAuth = BrowserWithRequestStartup((container, pipelines, context) => { });
        }

        /// <summary>
        /// Browsers the with request startup.
        /// </summary>
        /// <param name="requestStartup">The request startup.</param>
        /// <returns>Browser object.</returns>
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
                                   var resource = new Group()
                                   {
                                       Id = Guid.Parse("2c1156fa-5902-4978-9c3d-ebcb77ae0d55"),
                                       Name = string.Empty,
                                       IsLocked = false,
                                       TenantId = Guid.Parse("b8e6c8df-15b0-44dc-b52a-7f9805be796a")
                                   };
                                   var repositoryMock = new Mock<IRepository<Group>>();
                                   repositoryMock
                                       .Setup(r => r.GetItemAsync(It.IsAny<Guid>()))
                                       .ReturnsAsync(resource);

                                   var repositoryFactoryMock = new Mock<IRepositoryFactory>();
                                   repositoryFactoryMock
                                       .Setup(f => f.CreateRepository<Group>())
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
                                   with.Module<GroupsModule>();
                                   with.Serializer<SynthesisJsonSerializer>();
                               });
        }

        #region Create Group Test cases
        [Fact]
        public async Task CreateGroupReturnsOk()
        {
            var actual = await _browserAuth.Post(
                                                 "/v1/groups", with =>
                                                               {
                                                                   with.Header("Accept", "application/json");
                                                                   with.Header("Content-Type", "application/json");
                                                                   with.HttpRequest();
                                                                   with.JsonBody(new Group());

                                                               });
            Assert.Equal(HttpStatusCode.Created, actual.StatusCode);
        }

        [Fact]
        public async Task CreateGroupReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(m => m.CreateGroupAsync(It.IsAny<Group>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new Exception());

            var actual = await _browserAuth.Post(
                                                 "/v1/groups",
                                                 with =>
                                                 {
                                                     with.Header("Accept", "application/json");
                                                     with.Header("Content-Type", "application/json");
                                                     with.HttpRequest();
                                                     with.JsonBody(new Group());
                                                 });
            Assert.Equal(HttpStatusCode.InternalServerError, actual.StatusCode);
        }

        [Fact]
        public async Task CreateGroupReturnsItemWithInvalidBodyReturnsBadRequest()
        {
            var invalidBody = "{]";

            var actual = await _browserAuth.Post(
                                                 "/v1/groups",
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
        public async Task CreateGroupReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.CreateGroupAsync(It.IsAny<Group>(), It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new ValidationFailedException(new List<ValidationFailure>()));
            var actual = await _browserAuth.Post(
                                                 "/v1/groups",
                                                 with =>
                                                 {
                                                     with.Header("Accept", "application/json");
                                                     with.Header("Content-Type", "application/json");
                                                     with.HttpRequest();
                                                     with.JsonBody(new Group());
                                                 });

            Assert.Equal(HttpStatusCode.BadRequest, actual.StatusCode);
            Assert.Equal(ResponseText.BadRequestValidationFailed, actual.ReasonPhrase);
        }

        #endregion

        #region GetGroupById Response Test Cases

        [Fact]
        public async Task GetGroupByIdReturnsOk()
        {
            _controllerMock.Setup(m => m.GetGroupByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new Group()));

            var validGroupId = Guid.NewGuid();
            var response = await _browserAuth.Get($"/v1/groups/{validGroupId}", 
                with =>
                {
                    with.HttpRequest();
                    with.Header("Accept", "application/json");
                    with.Header("Content-Type", "application/json");
                });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupByIdReturnsBadRequest()
        {
            _controllerMock.Setup(m => m.GetGroupByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new ValidationFailedException(new List<ValidationFailure>()));

            var validGroupId = Guid.NewGuid();
            var response = await _browserAuth.Get($"/v1/groups/{validGroupId}", 
                with =>
                {
                    with.HttpRequest();
                    with.Header("Accept", "application/json");
                    with.Header("Content-Type", "application/json");
                });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupByIdReturnsUnauthorized()
        {
            _controllerMock.Setup(m => m.GetGroupByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Returns(Task.FromResult(new Group()));

            var validGroupId = Guid.NewGuid();
            var response = await _browserNoAuth.Get($"/v1/groups/{validGroupId}", 
                with =>
                {
                    with.HttpRequest();
                    with.Header("Accept", "application/json");
                    with.Header("Content-Type", "application/json");
                });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupByIdReturnsInternalServerError()
        {
            _controllerMock.Setup(m => m.GetGroupByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new Exception());

            var validGroupId = Guid.NewGuid();
            var response = await _browserAuth.Get($"/v1/groups/{validGroupId}",
                with =>
                {
                    with.HttpRequest();
                    with.Header("Accept", "application/json");
                    with.Header("Content-Type", "application/json");
                });
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupByIdReturnsNotFoundIfItemDoesNotExist()
        {
            _controllerMock.Setup(m => m.GetGroupByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new NotFoundException(string.Empty));

            var validGroupId = Guid.NewGuid();
            var response = await _browserAuth.Get($"/v1/groups/{validGroupId}", 
                with =>
                {
                    with.HttpRequest();
                    with.Header("Accept", "application/json");
                    with.Header("Content-Type", "application/json");
                });
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task GetGroupByIdReturnsValidationFailedException()
        {
            var errors = Enumerable.Empty<ValidationFailure>();
            _controllerMock.Setup(m => m.GetGroupByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>()))
                           .Throws(new ValidationFailedException(errors));

            var validGroupId = Guid.NewGuid();
            var response = await _browserAuth.Get($"/v1/groups/{validGroupId}",
                with =>
                {
                    with.HttpRequest();
                    with.Header("Accept", "application/json");
                    with.Header("Content-Type", "application/json");
                });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        #endregion

        #region Delete Group Test cases
        [Fact]
        public async Task DeleteGroupReturnsNoContent()
        {
            var actual = await _browserAuth.Delete(
                                                 "/v1/groups/7b629edf-ebce-49c3-9760-8a1856da2830", with =>
                                                               {
                                                                   with.Header("Accept", "application/json");
                                                                   with.Header("Content-Type", "application/json");
                                                                   with.HttpRequest();
                                                                   
                                                               });
            Assert.Equal(HttpStatusCode.NoContent, actual.StatusCode);
        }

        [Fact]
        public async Task DeleteGroupReturnsInternalServerErrorIfUnhandledExceptionIsThrown()
        {
            _controllerMock.Setup(m => m.DeleteGroupAsync(It.IsAny<Guid>()))
                           .Throws(new Exception());

            var actual = await _browserAuth.Delete(
                                                 "/v1/groups/7b629edf-ebce-49c3-9760-8a1856da2830",
                                                 with =>
                                                 {
                                                     with.Header("Accept", "application/json");
                                                     with.Header("Content-Type", "application/json");
                                                     with.HttpRequest();
                                                     
                                                 });
            Assert.Equal(HttpStatusCode.InternalServerError, actual.StatusCode);
        }

        [Fact]
        public async Task DeleteGroupReturnsBadRequestIfValidationFails()
        {
            _controllerMock.Setup(m => m.DeleteGroupAsync(It.IsAny<Guid>()))
                           .Throws(new ValidationFailedException(new List<ValidationFailure>()));
            var actual = await _browserAuth.Delete(
                                                 "/v1/groups/7b629edf-ebce-49c3-9760-8a1856da2830",
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
    }
}
