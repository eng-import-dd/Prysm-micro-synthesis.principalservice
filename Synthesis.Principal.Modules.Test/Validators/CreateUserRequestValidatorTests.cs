﻿using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.Principal.Modules.Test.Validators
{
    public class CreateUserRequestValidatorTests
    {
        private readonly CreateUserRequestValidator _validator = new CreateUserRequestValidator();

        [Fact]
        public void ShouldFailIfFirstNameIsEmpty()
        {
            var request = new UserRequest
            {
                LastName = "Test",
                 Email ="a@b.com",
                 UserName ="User"
            };

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfLastNameIsEmpty()
        {
            var request = new UserRequest
            {

                FirstName = "Test",
                Email = "a@b.com",
                UserName = "User"
            };

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfEmailIsEmpty()
        {
            var request = new UserRequest
            {
                FirstName = "Test",
                LastName ="User",
                UserName = "User"
            };

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }


        [Fact]
        public void ShouldFailIfUserNameIsEmpty()
        {
            var request = new UserRequest
            {
                FirstName = "Test",
                LastName = "User",
                Email = "a@b.com"
            };

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldPassIfValid()
        {
            var request = new UserRequest
            {
                FirstName = "Test",
                LastName = "User",
                Email = "a@b.com",
                UserName = "User"
            };

            var result = _validator.Validate(request);

            Assert.True(result.IsValid);
        }

    }
}