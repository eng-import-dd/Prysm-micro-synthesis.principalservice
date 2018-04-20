using FluentValidation;
using Synthesis.PrincipalService.InternalApi.Models;

namespace Synthesis.PrincipalService.Validators
{
    /// <inheritdoc />
    /// <summary>
    ///     Create Group Request Validator class
    /// </summary>
    /// <seealso cref="T:FluentValidation.AbstractValidator`1" />
    public class CreateCustomGroupRequestValidator : AbstractValidator<Group>
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="CreateCustomGroupRequestValidator" /> class.
        /// </summary>
        public CreateCustomGroupRequestValidator()
        {
            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("The Group Name property must not be empty")
                .MaximumLength(100)
                .WithMessage("The Group Name must be less than 100 characters long");

            RuleFor(request => request.Type )
                .NotEqual(request => GroupType.Custom)
                .WithMessage("Only custom groups may be created");
        }
    }
}