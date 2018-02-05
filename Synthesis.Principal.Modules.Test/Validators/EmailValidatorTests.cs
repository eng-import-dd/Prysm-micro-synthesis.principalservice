using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class EmailValidatorTests
    {
        [Fact]
        public void ShouldFailOnInvalidEmailAddress()
        {
            var validator = new EmailValidator();

            var result = validator.Validate("name.email.com");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnInvalidEmailAddressAndCustomPropertyName()
        {
            var validator = new EmailValidator("GuestEmail");

            var result = validator.Validate("name.email.com");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldPassOnValidEmailAddress()
        {
            var validator = new EmailValidator();

            var result = validator.Validate("name@email.com");
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ShouldPassOnValidEmailAddressAndCustomPropertyName()
        {
            var validator = new EmailValidator("GuestEmail");

            var result = validator.Validate("name@email.com");
            Assert.True(result.IsValid);
        }
    }
}
