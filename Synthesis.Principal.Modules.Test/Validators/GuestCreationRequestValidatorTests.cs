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
            var result = _validator.Validate(GuestCreationRequest.Example());
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnInvalidEmail()
        {
            var request = GuestCreationRequest.Example();
            request.Email = "invalid.email.com";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnInvalidFirstName()
        {
            var request = GuestCreationRequest.Example();
            request.FirstName = "";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnInvalidLastName()
        {
            var request = GuestCreationRequest.Example();
            request.LastName = "";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnInvalidPassword()
        {
            var request = GuestCreationRequest.Example();
            request.Password = "";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnInvalidPasswordConfirmation()
        {
            var request = GuestCreationRequest.Example();
            request.PasswordConfirmation = null;

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfPasswordConfirmationDoesNoMatchPassword()
        {
            var request = GuestCreationRequest.Example();
            request.PasswordConfirmation = "";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }
    }
}
