using Synthesis.PrincipalService.Validators;
using System;
using Synthesis.PrincipalService.InternalApi.Models;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class CreateMachineRequestValidatorTests
    {
        private readonly CreateMachineRequestValidator _validator = new CreateMachineRequestValidator();

        [Fact]
        public void ShouldFailIfMachineKeyIsNotSet()
        {
            var machine = Machine.Example();
            machine.MachineKey = "";

            var result = _validator.Validate(machine);

            Assert.False(result.IsValid);
        }           

        [Fact]
        public void ShouldPassIfModifiedByAndMachineKeyIsSet()
        {
            var machine = Machine.Example();

            var result = _validator.Validate(machine);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfMachineKeyIsLessThan10Char()
        {
            var machine = Machine.Example();
            machine.MachineKey = "123456789";

            var result = _validator.Validate(machine);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfMachineKeyIsLargerThan20Char()
        {
            var machine = Machine.Example();
            machine.MachineKey = "123456789012345678901";

            var result = _validator.Validate(machine);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfLocationIsEmptyOrNull()
        {
            var machine = Machine.Example();
            machine.MachineKey = "     ";

            var result = _validator.Validate(machine);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfSettingProfileIdIsNull()
        {
            var machine = Machine.Example();
            machine.SettingProfileId = Guid.Empty;

            var result = _validator.Validate(machine);

            Assert.False(result.IsValid);
        }
    }
}
