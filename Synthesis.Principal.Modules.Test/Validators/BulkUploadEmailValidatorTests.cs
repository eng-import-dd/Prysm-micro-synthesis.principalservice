using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.Principal.Modules.Test.Validators
{
    public class BulkUploadEmailValidatorTests
    {
        private readonly BulkUploadEmailValidator _validator = new BulkUploadEmailValidator();

        [Fact]
        public void ShouldFailOnEmptyEmailAddress()
        {
            var result = _validator.Validate("");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailOnInvalidEmailAddress()
        {
            var result = _validator.Validate("name.email.com");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldPassOnValidEmailAddress()
        {
            var result = _validator.Validate("name@email.com");
            Assert.True(result.IsValid);
        }
    }
}
