using System;
using FluentValidation;
using Synthesis.PrincipalService.InternalApi.Models;

namespace Synthesis.PrincipalService.Validators
{
    public class CreateMachineRequestValidator : AbstractValidator<Machine>
    {
        public CreateMachineRequestValidator()
        {
            RuleFor(request => request.MachineKey)
                .NotEmpty().WithMessage("The MachineKey property must not be empty")
                .MaximumLength(20).WithMessage("The MachineKey must be less than 20 characters long")
                .MinimumLength(10).WithMessage("The MachineKey must be greater than or equal to 10 characters long")
                .Matches("^[0-9a-zA-Z]*$").WithMessage("MachineKey may only contain alpha-numeric characters.");

            RuleFor(request => request.Location)
                .NotEmpty().WithMessage("The Location property must not be empty")
                .MaximumLength(50).WithMessage("The Location must be less than 50 characters long");

            RuleFor(request => request.SettingProfileId)
                .NotEmpty().WithMessage("The Setting Profile Id property must not be empty")
                .Must(BeAValidGuid).WithMessage("The Setting Profile Id must be a valid guid");
        }

        private bool BeAValidGuid(Guid? id)
        {
            return !id.Equals(default(Guid));
        }
    }
}
