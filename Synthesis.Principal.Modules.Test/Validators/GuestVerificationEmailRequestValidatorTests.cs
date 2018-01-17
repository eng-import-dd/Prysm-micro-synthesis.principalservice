using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.Principal.Modules.Test.Validators
{
    public class GuestVerificationEmailRequestValidatorTests
    {
        private readonly GuestVerificationEmailRequestValidator _validator = new GuestVerificationEmailRequestValidator();

        [Fact]
        public void ShouldPassWithExample()
        {
            var result = _validator.Validate(GuestVerificationEmailRequest.Example());
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnInvalidEmail()
        {
            var request = GuestVerificationEmailRequest.Example();
            request.Email = "";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnInvalidFirstName()
        {
            var request = GuestVerificationEmailRequest.Example();
            request.FirstName = "";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnInvalidLastName()
        {
            var request = GuestVerificationEmailRequest.Example();
            request.LastName = "";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnInvalidProjectAccessCode()
        {
            var request = GuestVerificationEmailRequest.Example();
            request.ProjectAccessCode = "";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

    }
}
