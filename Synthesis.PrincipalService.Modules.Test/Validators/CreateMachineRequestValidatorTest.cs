using Synthesis.PrincipalService.Requests;
using Synthesis.PrincipalService.Validators;
using System;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Validators
{
    public class CreateMachineRequestValidatorTest
    {
        private readonly CreateMachineRequestValidator _validator = new CreateMachineRequestValidator();

        [Fact]
        public void ShouldFailIfMachineKeyIsNotSet()
        {
            var request = new CreateMachineRequest
            {
                AccountId = Guid.Parse("c7eb01e4-6435-4c30-b17d-743f70043d9d"),
                AveragePingTime = new TimeSpan(0, 0, 1),
                CurrentlyOnline = false,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                IsSelected = false,
                LastOnline = DateTime.UtcNow,
                Location = "location",
                MachineId = Guid.Parse("002a2f31-0de4-495c-823e-4d5fa4da071b"),
                ModifiedBy = Guid.Parse("8767752e-a867-4ec3-bec8-cea01ea2eabd"),
                SettingProfileId = Guid.Parse("5c0cff7b-bbf6-41a4-a3f1-dba3b01f16d5"),
                SettingProfileName = "name",
                SynthesisVersion = "2.10"
            };

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }           

        [Fact]
        public void ShouldFailIfModifiedByIsNotSet()
        {
            var request = new CreateMachineRequest
            {
                AccountId = Guid.Parse("c7eb01e4-6435-4c30-b17d-743f70043d9d"),
                AveragePingTime = new TimeSpan(0, 0, 1),
                CurrentlyOnline = false,
                DateCreated = DateTime.UtcNow,
                DateModified = DateTime.UtcNow,
                IsSelected = false,
                LastOnline = DateTime.UtcNow,
                Location = "location",
                MachineId = Guid.Parse("002a2f31-0de4-495c-823e-4d5fa4da071b"),
                MachineKey = "mac_key",
                SettingProfileId = Guid.Parse("5c0cff7b-bbf6-41a4-a3f1-dba3b01f16d5"),
                SettingProfileName = "name",
                SynthesisVersion = "2.10"
            };

            var result = _validator.Validate(request);

            Assert.False(result.IsValid);
        }
    }
}
