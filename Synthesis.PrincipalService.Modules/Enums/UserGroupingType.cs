namespace Synthesis.PrincipalService.Enums
{
    /// <summary>
    /// UserGroupingType is used to filter the get users for account call to include or exclude users from a particular grouping
    /// </summary>
    public enum UserGroupingType
    {
        /// <summary>
        /// No group
        /// </summary>
        None,
        /// <summary>
        /// Project group
        /// </summary>
        Project,
        /// <summary>
        /// Permission group
        /// </summary>
        Permission
    }
}