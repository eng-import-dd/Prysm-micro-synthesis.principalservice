using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class NameValidatorTests
    {
        [Fact]
        public void ShouldFailOnInvalidName()
        {
            var validator = new NameValidator();

            var result = validator.Validate("");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnInvalidNameAndCustomPropertyName()
        {
            var validator = new NameValidator("FirstName");

            var result = validator.Validate("");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldPassOnValidName()
        {
            var validator = new NameValidator();

            var result = validator.Validate("SomeName");
            Assert.True(result.IsValid);
        }

        [Fact]
        public void ShouldPassOnValidNameAndCustomPropertyName()
        {
            var validator = new NameValidator("FirstName");

            var result = validator.Validate("SomeName");
            Assert.True(result.IsValid);
        }
    }
}
