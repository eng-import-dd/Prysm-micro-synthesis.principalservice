using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Validators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class CreateUserRequestValidatorTest
    {
        private readonly CreateUserRequestValidator _validator = new CreateUserRequestValidator();

        [Fact]
        public void ShouldFailIfFirstNameIsEmpty()
        {
            var request = new CreateUserRequest
            {
                LastName = "Test",
                 PasswordHash = "hash",
                 PasswordSalt ="salt",
                 Email ="a@b.com",
                 UserName ="User"
            };

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfLastNameIsEmpty()
        {
            var request = new CreateUserRequest
            {

                FirstName = "Test",
                PasswordHash = "hash",
                PasswordSalt = "salt",
                Email = "a@b.com",
                UserName = "User"
            };

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfEmailIsEmpty()
        {
            var request = new CreateUserRequest
            {
                FirstName = "Test",
                LastName ="User",
                PasswordHash = "hash",
                PasswordSalt = "salt",
                UserName = "User"
            };

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }


        [Fact]
        public void ShouldFailIfUserNameIsEmpty()
        {
            var request = new CreateUserRequest
            {
                FirstName = "Test",
                LastName = "User",
                PasswordHash ="hash",
                PasswordSalt = "salt",
                Email = "a@b.com"
            };

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }


        [Fact]
        public void ShouldFailIfPasswordHashIsEmpty()
        {
            var request = new CreateUserRequest
            {
                FirstName ="Test",
                LastName = "User",
                PasswordSalt = "salt",
                Email = "a@b.com",
                UserName = "User"
            };

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfPasswordSaltIsEmpty()
        {
            var request = new CreateUserRequest
            {
                FirstName = "Test",
                LastName = "User",
                PasswordHash = "hash",
                Email = "a@b.com",
                UserName = "User"
            };

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }


        [Fact]
        public void ShouldPassIfValid()
        {
            var request = new CreateUserRequest
            {
                FirstName = "Test",
                LastName = "User",
                PasswordHash = "hash",
                PasswordSalt ="salt",
                Email = "a@b.com",
                UserName = "User"
            };

            var result = _validator.Validate(request);

            Assert.True(result.IsValid);
        }

    }
}
