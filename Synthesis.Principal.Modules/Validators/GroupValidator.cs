using FluentValidation;
using Synthesis.PrincipalService.InternalApi.Models;
using Synthesis.PrincipalService.Models;

namespace Synthesis.PrincipalService.Validators
{
    /// <inheritdoc />
    /// <summary>
    ///     Create Group Request Validator class
    /// </summary>
    /// <seealso cref="T:FluentValidation.AbstractValidator`1" />
    public class CreateGroupRequestValidator : AbstractValidator<Group>
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="CreateGroupRequestValidator" /> class.
        /// </summary>
        public CreateGroupRequestValidator()
        {
            RuleFor(request => request.Name)
                .NotEmpty().WithMessage("The Group Name property must not be empty")
                .MaximumLength(100).WithMessage("The Group Name must be less than 100 characters long");
        }
    }
}