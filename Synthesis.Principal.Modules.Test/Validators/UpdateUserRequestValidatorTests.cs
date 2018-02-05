using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class UpdateUserRequestValidatorTests
    {
        private readonly UpdateUserRequestValidator _validator = new UpdateUserRequestValidator();

        [Fact]
        public void ShouldFailIfFirstNameIsEmpty()
        {
            var request = UpdateUserRequest.Example();
            request.FirstName = "";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfLastNameIsEmpty()
        {
            var request = UpdateUserRequest.Example();
            request.LastName = "";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfEmailIsEmpty()
        {
            var request = UpdateUserRequest.Example();
            request.Email = "";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }


        [Fact]
        public void ShouldFailIfUserNameIsEmpty()
        {
            var request = UpdateUserRequest.Example();
            request.UserName = "";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldPassIfValid()
        {
            var request = UpdateUserRequest.Example();
            var result = _validator.Validate(request);
            Assert.True(result.IsValid);
        }
    }
}
