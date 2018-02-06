using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class ProjectAccessCodeValidatorTests
    {
        private readonly ProjectAccessCodeValidator _validator = new ProjectAccessCodeValidator();

        [Fact]
        public void ShouldFailOnEmptyCode()
        {
            var result = _validator.Validate("");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnShortCode()
        {
            var result = _validator.Validate("123456789");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnLongCode()
        {
            var result = _validator.Validate("12345678901");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfContainsNonNumber()
        {
            var result = _validator.Validate("1a23");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldPassOnValidCode()
        {
            var result = _validator.Validate("1234567890");
            Assert.True(result.IsValid);
        }
    }
}
