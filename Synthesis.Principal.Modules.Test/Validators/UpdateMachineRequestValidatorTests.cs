using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Validators;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class UpdateMachineRequestValidatorTests
    {
        private readonly UpdateMachineRequestValidator _validator = new UpdateMachineRequestValidator();

        [Fact]
        public void ShouldFailIfMachineKeyIsGreaterThanMaxLength()
        {
            var request = Machine.Example();
            request.MachineKey = "1122334455667788990011";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfMachineKeyContainsSpecialCharacters()
        {
            var request = Machine.Example();
            request.MachineKey = "11223344556@#$";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfMachineKeyIsNull()
        {
            var request = Machine.Example();
            request.MachineKey = null;

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfLocationIsNull()
        {
            var request = Machine.Example();
            request.Location = null;

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfLocationLengthIsGreaterThanMaxLength()
        {
            var request = Machine.Example();
            request.Location = "asjhdjashdjkhasjdhjkashdjkashdjkhaskjdhjkashdjkashjkdhasj";
            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldPassOnValidRequest()
        {
            var request = Machine.Example();
            var result = _validator.Validate(request);
            Assert.True(result.IsValid);
        }
    }
}
