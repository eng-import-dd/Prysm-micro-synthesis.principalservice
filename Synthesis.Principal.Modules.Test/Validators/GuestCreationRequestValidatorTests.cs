﻿using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class GuestCreationRequestValidatorTests
    {
        private readonly GuestCreationRequestValidator _validator = new GuestCreationRequestValidator();

        [Fact]
        public void ShouldPassWithExample()
        {
            var result = _validator.Validate(CreateUserRequest.GuestUserExample());
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnInvalidEmail()
        {
            var request = CreateUserRequest.GuestUserExample();
            request.Email = "invalid.email.com";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnInvalidFirstName()
        {
            var request = CreateUserRequest.GuestUserExample();
            request.FirstName = "";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnInvalidLastName()
        {
            var request = CreateUserRequest.GuestUserExample();
            request.LastName = "";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }
    }
}
