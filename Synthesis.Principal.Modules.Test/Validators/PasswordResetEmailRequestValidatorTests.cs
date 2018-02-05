using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class PasswordResetEmailRequestValidatorTests
    {
        private readonly PasswordResetEmailRequestValidator _validator = new PasswordResetEmailRequestValidator();

        [Trait("PasswordResetEmailRequest Test", "PasswordResetEmailRequest Test")]
        [Fact]
        public void ShouldFailOnInvalidFirstName()
        {
            var request = PasswordResetEmailRequest.Example();
            request.FirstName = "";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Trait("PasswordResetEmailRequest Test", "PasswordResetEmailRequest Test")]
        [Fact]
        public void ShouldFailIfEmailIsEmpty()
        {
            var request = PasswordResetEmailRequest.Example();
            request.Email = "";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Trait("PasswordResetEmailRequest Test", "PasswordResetEmailRequest Test")]
        [Fact]
        public void ShouldFailIfEmailIsInvalid()
        {
            var request = PasswordResetEmailRequest.Example();
            request.Email = "ab.com";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Trait("PasswordResetEmailRequest Test", "PasswordResetEmailRequest Test")]
        [Fact]
        public void ShouldFailIfEmailIsNull()
        {
            var request = PasswordResetEmailRequest.Example();
            request.Email = null;

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Trait("PasswordResetEmailRequest Test", "PasswordResetEmailRequest Test")]
        [Fact]
        public void ShouldFailIfLinkIsEmpty()
        {
            var request = PasswordResetEmailRequest.Example();
            request.Link = "";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Trait("PasswordResetEmailRequest Test", "PasswordResetEmailRequest Test")]
        [Fact]
        public void ShouldFailIfLinkIsNull()
        {
            var request = PasswordResetEmailRequest.Example();
            request.Link = null;

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Trait("PasswordResetEmailRequest Test", "PasswordResetEmailRequest Test")]
        [Fact]
        public void ShouldFailIfLinkIsInvalid()
        {
            var request = PasswordResetEmailRequest.Example();
            request.Link = "test.com";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Trait("User Name Test", "User Name Test")]
        [Fact]
        public void ShouldPassIfValidRequest()
        {
            var request = PasswordResetEmailRequest.Example();

            var result = _validator.Validate(request);
            Assert.True(result.IsValid);
        }
    }
}
