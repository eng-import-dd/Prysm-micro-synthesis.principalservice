using System.Configuration;

namespace Synthesis.PrincipalService.Utilities
{
    public static class ConfigurationUtility
    {
        public static bool IsTrustAllCertificates(bool defaultValue = false)
        {
            bool rtnVal = defaultValue;

            string trustAllCertificates = ConfigurationManager.AppSettings["TrustAllCertificates"];
            if (bool.TryParse(trustAllCertificates, out var parsedValue))
            {
                rtnVal = parsedValue;
            }

            return rtnVal;
        }
    }
}