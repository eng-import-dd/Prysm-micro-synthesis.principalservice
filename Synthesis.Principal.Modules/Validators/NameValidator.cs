using FluentValidation;

namespace Synthesis.PrincipalService.Validators
{
    public class NameValidator : AbstractValidator<string>
    {
        private readonly string _titleOfNameProperty;

        public NameValidator()
        {
            _titleOfNameProperty = "Name";
            InitializeRules();
        }

        public NameValidator(string titleOfNameProperty)
        {
            _titleOfNameProperty = titleOfNameProperty;
            InitializeRules();
        }

        private void InitializeRules()
        { 
            RuleFor(request => request)
                .NotEmpty().WithMessage($"The {_titleOfNameProperty} property must not be empty")
                .MaximumLength(100).WithMessage($"The {_titleOfNameProperty} must be less than 100 characters long");
        }
    }
}
