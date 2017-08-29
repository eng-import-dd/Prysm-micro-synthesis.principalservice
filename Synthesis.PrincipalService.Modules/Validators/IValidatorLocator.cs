using System;
using FluentValidation;

namespace Synthesis.PrincipalService.Validators
{
    public interface IValidatorLocator
    {
        IValidator GetValidator(Type validatorType);
    }
}
