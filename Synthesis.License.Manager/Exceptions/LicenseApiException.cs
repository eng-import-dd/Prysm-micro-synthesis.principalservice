using System;

namespace Synthesis.License.Manager.Exceptions
{
    public class LicenseApiException : Exception
    {
        public ResultCode ResultCode { get; }
        public string ClientMessage { get; }
        public LicenseApiException(string exceptionMessage, string clientMessage, ResultCode resultCode) : base(exceptionMessage)
        {
            ResultCode = resultCode;
            ClientMessage = clientMessage;
        }
    }
}