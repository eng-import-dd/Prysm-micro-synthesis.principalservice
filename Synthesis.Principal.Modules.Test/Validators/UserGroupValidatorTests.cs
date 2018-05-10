using System;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class UserGroupValidatorTests
    {
        private readonly UserGroupValidator _validator = new UserGroupValidator();

        [Fact]
        public void ShouldFailIfUserIdIsEmpty()
        {
            var request = UserGroup.Example();
            request.UserId = Guid.Empty;

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfGroupIdIsEmpty()
        {
            var request = UserGroup.Example();
            request.GroupId = Guid.Empty;

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfRequestObjectEmpty()
        {
            var request = new UserGroup();
            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldPassIfValidRequestObject()
        {
            var request = UserGroup.Example();

            var result = _validator.Validate(request);
            Assert.True(result.IsValid);
        }
    }
}
