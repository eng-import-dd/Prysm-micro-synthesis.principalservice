using Synthesis.PrincipalService.Requests;
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
            var request = UpdateMachineRequest.Example();
            request.MachineKey = "1122334455667788990011";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfMachineKeyContainsSpecialCharacters()
        {
            var request = UpdateMachineRequest.Example();
            request.MachineKey = "11223344556@#$";

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfMachineKeyIsNull()
        {
            var request = UpdateMachineRequest.Example();
            request.MachineKey = null;

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfLocationIsNull()
        {
            var request = UpdateMachineRequest.Example();
            request.Location = null;

            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfLocationLengthIsGreaterThanMaxLength()
        {
            var request = UpdateMachineRequest.Example();
            request.Location = "asjhdjashdjkhasjdhjkashdjkashdjkhaskjdhjkashdjkashjkdhasj";
            var result = _validator.Validate(request);
            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldPassOnValidRequest()
        {
            var request = UpdateMachineRequest.Example();
            var result = _validator.Validate(request);
            Assert.True(result.IsValid);
        }
    }
}
