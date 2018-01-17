using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.Principal.Modules.Test.Validators
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

        [Theory]
        [InlineData("123")]
        [InlineData("123456789")]
        public void ShouldFailOnShortCode(string accessCode)
        {
            var result = _validator.Validate("123456789");
            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("1234567890")]
        [InlineData("1234567890123")]
        public void ShouldFailOnLongCode(string accessCode)
        {
            var result = _validator.Validate("123456789");
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
