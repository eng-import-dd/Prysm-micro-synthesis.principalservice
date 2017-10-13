using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class EmailAddressValidatorTest
    {
        private readonly EmailAddressValidator _validator = new EmailAddressValidator();

        [Trait("Email Address Test", "Email Address Test")]
        [Fact]
        public void ShouldFailIfEmailAddressIsEmpty()
        {
            var request = string.Empty;
            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Trait("Email Address Test", "Email Address Test")]
        [Fact]
        public void ShouldFailIfInvalidEmailAddress()
        {
            var request = "user@prysm";
            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Trait("Email Address Test", "Email Address Test")]
        [Fact]
        public void ShouldPassIfValidEmailAddress()
        {
            var request = "user@prysm.com";
            var result = _validator.Validate(request);
            Assert.True(result.IsValid);
        }
    }
}
