using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class CreateUserRequestValidatorTests
    {
        private readonly CreateUserRequestValidator _validator = new CreateUserRequestValidator();

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void ShouldFailIfFirstNameIsInvalid(string name)
        {
            var request = CreateUserRequest.Example();
            request.FirstName = name;

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void ShouldFailIfLastNameIsInvalid(string name)
        {
            var request = CreateUserRequest.Example();
            request.LastName = name;

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void ShouldFailIfEmailIsInvalid(string name)
        {
            var request = CreateUserRequest.Example();
            request.Email = name;

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }


        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void ShouldFailIfUserNameIsInvalid(string name)
        {
            var request = CreateUserRequest.Example();
            request.Username = name;

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldPassIfValid()
        {
            var request = CreateUserRequest.Example();

            var result = _validator.Validate(request);

            Assert.True(result.IsValid);
        }

    }
}
