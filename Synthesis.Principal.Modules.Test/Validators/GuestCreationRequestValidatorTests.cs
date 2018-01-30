using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.Principal.Modules.Test.Validators
{
    public class GuestCreationRequestValidatorTests
    {
        private readonly GuestCreationRequestValidator _validator = new GuestCreationRequestValidator();

        [Fact]
        public void ShouldPassWithExample()
        {
            var result = _validator.Validate(CreateUserRequest.GuestExample());
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnInvalidEmail()
        {
            var request = CreateUserRequest.GuestExample();
            request.Email = "invalid.email.com";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnInvalidFirstName()
        {
            var request = CreateUserRequest.GuestExample();
            request.FirstName = "";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnInvalidLastName()
        {
            var request = CreateUserRequest.GuestExample();
            request.LastName = "";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnInvalidPassword()
        {
            var request = CreateUserRequest.GuestExample();
            request.Password = "";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnInvalidPasswordConfirmation()
        {
            var request = CreateUserRequest.GuestExample();
            request.PasswordConfirmation = null;

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfPasswordConfirmationDoesNoMatchPassword()
        {
            var request = CreateUserRequest.GuestExample();
            request.PasswordConfirmation = "";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }
    }
}
