using System;
using Autofac;
using FluentValidation;

namespace Synthesis.PrincipalService.Validators
{
    public class ValidatorLocator : IValidatorLocator
    {
        private readonly ILifetimeScope _container;

        public ValidatorLocator(ILifetimeScope container)
        {
            _container = container;
        }

        public IValidator GetValidator(Type validatorType)
        {
            object validator;

            if (_container.TryResolve(validatorType, out validator))
            {
                var validatorChk = validator as IValidator;
                if (validatorChk != null)
                {
                    return validatorChk;
                }
            }

            return null;
        }
    }
}
