using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Validators;
using System;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class UpdateMachineRequestValidatorTest
    {
        private UpdateMachineRequestValidator _validator = new UpdateMachineRequestValidator();

        [Fact]
        public void ShouldFailIfMachineKeyIsGreaterThanMaxLength()
        {
            var request = new UpdateMachineRequest
            {
                MachineKey = "1122334455667788990011",
                DateModified = DateTime.UtcNow,
                ModifiedBy = Guid.Parse("698cdb4e-74ce-44df-88c2-2c012b3c3c59"),
                Location = "SomeTestLocation",
                Id = Guid.Parse("075fbb56-5417-45d8-8e22-91235348586e"),
                TenantId = Guid.Parse("ecbb7165-7fa7-48ef-a7e0-945053202f3e")
            };
            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfMachineKeyContainsSpecialCharacters()
        {
            var request = new UpdateMachineRequest
            {
                MachineKey = "11223344556@#$",
                DateModified = DateTime.UtcNow,
                ModifiedBy = Guid.Parse("698cdb4e-74ce-44df-88c2-2c012b3c3c59"),
                Location = "SomeTestLocation",
                Id = Guid.Parse("075fbb56-5417-45d8-8e22-91235348586e"),
                TenantId = Guid.Parse("ecbb7165-7fa7-48ef-a7e0-945053202f3e")
            };
            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfMachineKeyIsNull()
        {
            var request = new UpdateMachineRequest
            {
                MachineKey = null,
                DateModified = DateTime.UtcNow,
                ModifiedBy = Guid.Parse("698cdb4e-74ce-44df-88c2-2c012b3c3c59"),
                Location = "SomeTestLocation",
                Id = Guid.Parse("075fbb56-5417-45d8-8e22-91235348586e"),
                TenantId = Guid.Parse("ecbb7165-7fa7-48ef-a7e0-945053202f3e")
            };
            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfLocationIsNull()
        {
            var request = new UpdateMachineRequest
            {
                MachineKey = "215361527",
                DateModified = DateTime.UtcNow,
                ModifiedBy = Guid.Parse("698cdb4e-74ce-44df-88c2-2c012b3c3c59"),
                Location = null,
                Id = Guid.Parse("075fbb56-5417-45d8-8e22-91235348586e"),
                TenantId = Guid.Parse("ecbb7165-7fa7-48ef-a7e0-945053202f3e")
            };
            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

        [Fact]
        public void ShouldFailIfLocationLengthIsGreaterThanMaxLength()
        {
            var request = new UpdateMachineRequest
            {
                MachineKey = "215361527",
                DateModified = DateTime.UtcNow,
                ModifiedBy = Guid.Parse("698cdb4e-74ce-44df-88c2-2c012b3c3c59"),
                Location = "asjhdjashdjkhasjdhjkashdjkashdjkhaskjdhjkashdjkashjkdhasj",
                Id = Guid.Parse("075fbb56-5417-45d8-8e22-91235348586e"),
                TenantId = Guid.Parse("ecbb7165-7fa7-48ef-a7e0-945053202f3e")
            };
            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }

       
    }
}
