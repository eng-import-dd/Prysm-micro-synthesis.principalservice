namespace Synthesis.PrincipalService.Validators
{
    /// <summary>
    /// Group Id Validator
    /// </summary>
    /// <seealso cref="Synthesis.PrincipalService.Validators.GuidValidator" />
    public class GroupIdValidator : GuidValidator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="GroupIdValidator"/> class.
        /// </summary>
        public GroupIdValidator() : base("Id")
        {
        }
    }
}
