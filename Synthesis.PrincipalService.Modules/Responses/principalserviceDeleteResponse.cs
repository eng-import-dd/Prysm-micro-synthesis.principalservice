namespace Synthesis.PrincipalService.Responses
{
    public class PrincipalserviceDeleteResponse
    {
        public PrincipalserviceDeleteResponse()
        {
            IsDeleted = true;
        }

        public string Code { get; set; }
        public bool IsDeleted { get; set; }
    }
}
