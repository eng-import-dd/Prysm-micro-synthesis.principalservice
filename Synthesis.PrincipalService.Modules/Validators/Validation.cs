using System;
using System.Collections.Generic;
using FluentValidation;

namespace Synthesis.PrincipalService.Validators
{
    public class Validation
    {
        public bool Success { get; set; }
        public IEnumerable<string> Errors { get; set; }

        public Validation(IValidator validator, dynamic item)
        {
            Success = true;
            var errors = new List<string>();

            try
            {
                var validationResult = validator.Validate(item);
                if (!validationResult.IsValid)
                {
                    foreach (var error in validationResult.Errors)
                    {
                        errors.Add(error.ErrorMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
            }

            if (errors.Count > 0)
            {
                Success = false;
                Errors = errors;
            }
        }
    }
}
