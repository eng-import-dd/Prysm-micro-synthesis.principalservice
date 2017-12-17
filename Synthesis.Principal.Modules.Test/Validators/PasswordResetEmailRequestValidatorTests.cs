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
        public void ShouldFailIfFirstNameIsEmpty()
        {
            var request = new PasswordResetEmailRequest
            {
                Email = "a@b.com",
                Link = "http://test.com"
            };
            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Trait("PasswordResetEmailRequest Test", "PasswordResetEmailRequest Test")]
        [Fact]
        public void ShouldFailIfEmailIsEmpty()
        {
            var request = new PasswordResetEmailRequest
            {
                FirstName = "Test",
                Link = "http://test.com"
            };
            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Trait("PasswordResetEmailRequest Test", "PasswordResetEmailRequest Test")]
        [Fact]
        public void ShouldFailIfEmailIsInvalid()
        {
            var request = new PasswordResetEmailRequest
            {
                Email = "ab.com",
                FirstName = "Test",
                Link = "http://test.com"
            };
            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Trait("PasswordResetEmailRequest Test", "PasswordResetEmailRequest Test")]
        [Fact]
        public void ShouldFailIfLinkIsEmpty()
        {
            var request = new PasswordResetEmailRequest
            {
                Email = "a@b.com",
                FirstName = "Test"
            };
            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Trait("PasswordResetEmailRequest Test", "PasswordResetEmailRequest Test")]
        [Fact]
        public void ShouldFailIfLinkIsInvalid()
        {
            var request = new PasswordResetEmailRequest
            {
                Email = "a@b.com",
                FirstName = "Test",
                Link = "test.com"
            };
            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }



        [Trait("User Name Test", "User Name Test")]
        [Fact]
        public void ShouldPassIfValidRequest()
        {
            var request = new PasswordResetEmailRequest
            {
                Email = "a@b.com",
                FirstName = "Test",
                Link = "http://test.com"
            };
            var result = _validator.Validate(request);
            Assert.True(result.IsValid);
        }
    }
}
