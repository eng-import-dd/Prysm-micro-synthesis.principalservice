using FluentValidation;
using Synthesis.PrincipalService.Requests;
using System;

namespace Synthesis.PrincipalService.Validators
{
    public class UpdateMachineRequestValidator : AbstractValidator<UpdateMachineRequest>
    {
        public UpdateMachineRequestValidator()
        {
            RuleFor(request => request.MachineKey)
                .NotEmpty().WithMessage("The MachineKey property must not be empty")
                .MaximumLength(20).WithMessage("The MachineKey must be less than 20 characters long")
                .Matches(@"^[0-9a-zA-Z]$").WithMessage("MachineKey may only contain alpha-numeric characters.");

            RuleFor(request => request.Location)
                .NotEmpty().WithMessage("The Location property must not be empty")
                .MaximumLength(50).WithMessage("The Location must be less than 20 characters long");

            RuleFor(request => request.SettingProfileId)
               .NotEmpty().WithMessage("The Setting Profile Id property must not be empty")
               .Must(BeAValidGuid).WithMessage("The Setting Profile Id must be a valid guid");
        }

        private bool BeAValidGuid(Guid id)
        {
            return !id.Equals(default(Guid));
        }
    }
}
