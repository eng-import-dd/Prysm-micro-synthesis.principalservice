using System;

namespace Synthesis.License.Manager
{
    internal class LicenseApiException : Exception
    {
        public ResultCode ResultCode { get; private set; }
        public string ClientMessage { get; private set; }
        public LicenseApiException(string exceptionMessage, string clientMessage, ResultCode resultCode) : base(exceptionMessage)
        {
            ResultCode = resultCode;
            ClientMessage = clientMessage;
        }
    }
}