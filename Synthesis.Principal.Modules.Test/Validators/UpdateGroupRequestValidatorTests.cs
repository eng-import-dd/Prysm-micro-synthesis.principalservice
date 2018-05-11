using System;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class UpdateGroupRequestValidatorTests
    {
        private readonly UpdateGroupRequestValidator _validator = new UpdateGroupRequestValidator();

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public void ShouldFailIfGroupIdIsEmpty()
        {
            var group = Group.Example();
            group.Id = Guid.Empty;

            var result = _validator.Validate(group);
            Assert.False(result.IsValid);
        }

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public void ShouldFailIfGroupIdIsNull()
        {
            var group = Group.Example();
            group.Id = null;

            var result = _validator.Validate(group);
            Assert.False(result.IsValid);
        }

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public void ShouldFailIfGroupNameIsEmpty()
        {
            var group = Group.Example();
            group.Name = "";

            var result = _validator.Validate(group);
            Assert.False(result.IsValid);
        }

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public void ShouldFailIfGroupNameIsNull()
        {
            var group = Group.Example();
            group.Name = null;

            var result = _validator.Validate(group);
            Assert.False(result.IsValid);
        }

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public void ShouldFailIfTenantIdIsEmpty()
        {
            var group = Group.Example();
            group.TenantId = Guid.Empty;

            var result = _validator.Validate(group);
            Assert.False(result.IsValid);
        }

        [Trait("Update Group", "Update Group Test Cases")]
        [Fact]
        public void ShouldPassIfValidRequestObject()
        {
            var group = Group.Example();

            var result = _validator.Validate(group);
            Assert.True(result.IsValid);
        }
    }
}