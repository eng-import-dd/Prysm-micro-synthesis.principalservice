﻿using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.Principal.Modules.Test.Validators
{
    public class CreateUserRequestValidatorTests
    {
        private readonly CreateUserRequestValidator _validator = new CreateUserRequestValidator();

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void ShouldFailIfFirstNameIsInvalid(string name)
        {
            var request = UserRequest.Example();
            request.FirstName = name;

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void ShouldFailIfLastNameIsInvalid(string name)
        {
            var request = UserRequest.Example();
            request.LastName = name;

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void ShouldFailIfEmailIsInvalid(string name)
        {
            var request = UserRequest.Example();
            request.Email = name;

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }


        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void ShouldFailIfUserNameIsInvalid(string name)
        {
            var request = UserRequest.Example();
            request.UserName = name;

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldPassIfValid()
        {
            var request = UserRequest.Example();

            var result = _validator.Validate(request);

            Assert.True(result.IsValid);
        }

    }
}
