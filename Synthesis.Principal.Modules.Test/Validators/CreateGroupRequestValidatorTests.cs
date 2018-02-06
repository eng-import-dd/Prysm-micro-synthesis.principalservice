using System;
using Synthesis.PrincipalService.Models;
using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class CreateGroupRequestValidatorTests
    {
        private Group GetValidRequest()
        {
            return new Group
            {
                Id = Guid.NewGuid(),
                IsLocked = false,
                Name = "A_Name!",
                TenantId = Guid.NewGuid()
            };
        }

        [Fact]
        public void ShouldFailOnEmptyName()
        {
            var group = GetValidRequest();
            group.Name = "";

            var validator = new CreateGroupRequestValidator();
            var result = validator.Validate(group);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnNameTooLong()
        {
            var group = GetValidRequest();
            group.Name = new string('*', 101);

            var validator = new CreateGroupRequestValidator();
            var result = validator.Validate(group);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldPassOnValidGroup()
        {
            var group = GetValidRequest();

            var validator = new CreateGroupRequestValidator();
            var result = validator.Validate(group);
            Assert.True(result.IsValid);
        }
    }
}
