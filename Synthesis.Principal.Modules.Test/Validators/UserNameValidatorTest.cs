using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class UserNameValidatorTest
    {
        private readonly UserNameValidator _validator = new UserNameValidator();

        [Trait("User Name Test", "User Name Test")]
        [Fact]
        public void ShouldFailIfUserNameIsEmpty()
        {
            var request = string.Empty;
            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

       
        [Trait("User Name Test", "User Name Test")]
        [Fact]
        public void ShouldPassIfValidEmailAddress()
        {
            var request = "username";
            var result = _validator.Validate(request);
            Assert.True(result.IsValid);
        }
    }
}
