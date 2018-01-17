using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.Principal.Modules.Test.Validators
{
    public class PasswordValidatorTests
    {
        private readonly PasswordValidator _validator = new PasswordValidator();

        [Fact]
        public void ShouldFailOnShortPassword()
        {
            var result = _validator.Validate("123");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldPassOnValidPassword()
        {
            var result = _validator.Validate("hello@email.com");
            Assert.True(result.IsValid);
        }
    }
}
