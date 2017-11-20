using System;
using Synthesis.PrincipalService.Models;
using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class UpdateGroupRequestValidatorTest
    {
        private readonly UpdateGroupRequestValidator _validator = new UpdateGroupRequestValidator();

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public void ShouldFailIfGroupIdIsEmpty()
        {
            var request = new Group
            {
                Id = Guid.NewGuid()
            };

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public void ShouldFailIfGroupNameIsEmpty()
        {
            var request = new Group
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                Name = string.Empty
            };

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public void ShouldFailIfTenantIdIsEmpty()
        {
            var request = new Group
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.Empty
            };

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public void ShouldPassIfValidRequestObject()
        {
            var request = new Group
            {
                Id = Guid.NewGuid(),
                TenantId = Guid.NewGuid(),
                Name = "PrysmGroup"
            };

            var result = _validator.Validate(request);
            Assert.True(result.IsValid);
        }
    }
}