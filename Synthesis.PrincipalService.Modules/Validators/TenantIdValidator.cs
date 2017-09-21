namespace Synthesis.PrincipalService.Validators
{
    public class TenantIdValidator : GuidValidator
    {
        public TenantIdValidator() : base("Id")
        {
        }
    }
}