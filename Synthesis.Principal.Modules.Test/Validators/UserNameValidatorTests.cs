using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class UserNameValidatorTests
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
        public void ShouldFailIfTooLong()
        {
            var request = new string('*', 101);
            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Trait("User Name Test", "User Name Test")]
        [Fact]
        public void ShouldFailIfContainsSpecialCharacters()
        {
            var request = "UsernameForTheWin@!!!#$";
            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Trait("User Name Test", "User Name Test")]
        [Fact]
        public void ShouldPassIfValid()
        {
            var request = "username";
            var result = _validator.Validate(request);
            Assert.True(result.IsValid);
        }
    }
}
