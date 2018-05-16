using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class GuestVerificationEmailRequestValidatorTests
    {
        private readonly GuestVerificationEmailRequestValidator _validator = new GuestVerificationEmailRequestValidator();

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void ShouldFailIfFirstNameIsInvalid(string name)
        {
            var request = GuestVerificationEmailRequest.Example();
            request.FirstName = name;

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void ShouldFailIfLastNameIsInvalid(string name)
        {
            var request = GuestVerificationEmailRequest.Example();
            request.LastName = name;

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void ShouldFailIfEmailIsInvalid(string name)
        {
            var request = GuestVerificationEmailRequest.Example();
            request.Email = name;

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldPassIfValid()
        {
            var request = GuestVerificationEmailRequest.Example();

            var result = _validator.Validate(request);

            Assert.True(result.IsValid);
        }
    }
}
