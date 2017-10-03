using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class UpdateUserRequestValidatorTest
    {
        private readonly UpdateUserRequestValidator _validator = new UpdateUserRequestValidator();

        [Fact]
        public void ShouldFailIfFirstNameIsEmpty()
        {
            var request = new UpdateUserRequest
            {
                LastName = "Test",
                PasswordHash = "hash",
                PasswordSalt = "salt",
                Email = "a@b.com",
                UserName = "User"
            };

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfLastNameIsEmpty()
        {
            var request = new UpdateUserRequest
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
            var request = new UpdateUserRequest
            {
                FirstName = "Test",
                LastName = "User",
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
            var request = new UpdateUserRequest
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
            var request = new UpdateUserRequest
            {
                FirstName = "Test",
                LastName = "User",
                Email = "a@b.com",
                UserName = "User",
                IsLocked = false
            };

            var result = _validator.Validate(request);

            Assert.True(result.IsValid);
        }

    }
}
