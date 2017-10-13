
namespace Synthesis.PrincipalService.Validators
{
    public class MachineIdValidator : GuidValidator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MachineIdValidator"/> class.
        /// </summary>
        public MachineIdValidator() : base("Id")
        {
        }
    }
}
