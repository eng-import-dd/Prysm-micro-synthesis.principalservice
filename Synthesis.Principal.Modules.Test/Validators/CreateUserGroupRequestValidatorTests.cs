using System;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class CreateUserGroupRequestValidatorTests
    {
        private readonly CreateUserGroupRequestValidator _validator = new CreateUserGroupRequestValidator();

        [Fact]
        public void ShouldFailIfUserIdIsEmpty()
        {
           var request = new CreateUserGroupRequest
            {
                GroupId = Guid.NewGuid()
            };

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfGroupIdIsEmpty()
        {
            var request = new CreateUserGroupRequest
            {
                UserId = Guid.NewGuid()
            };

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfRequestObjectEmpty()
        {
            var request = new CreateUserGroupRequest();
            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldPassIfValidRequestObject()
        {
            var request = new CreateUserGroupRequest
            {
                UserId = Guid.NewGuid(),
                GroupId = Guid.NewGuid()
            };

            var result = _validator.Validate(request);
            Assert.True(result.IsValid);
        }
    }
}
