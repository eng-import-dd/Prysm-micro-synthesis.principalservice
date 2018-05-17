using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using Nancy.Helpers;
using Synthesis.EmailService.InternalApi.Models;
using Synthesis.PrincipalService.Exceptions;

namespace Synthesis.PrincipalService.Email
{
    public class VerifyGuestEmail
    {
        public static SendEmailRequest BuildRequest(string firstName, string email, string redirect, string emailVerificationId)
        {
            try
            {
                const string subject = "Almost there! Please verify your Prysm account.";

                var link = $"{ConfigurationManager.AppSettings.Get("BaseWebClientUrl")}/#/login?" +
                    $"{(string.IsNullOrWhiteSpace(redirect) ? string.Empty : "r=" + redirect + "&")}" +
                    $"email={HttpUtility.UrlEncode(email)}&token={emailVerificationId}";

                var createGuestInviteTemplate = GetContent("Email/Templates/VerifyNewAccount.html");
                createGuestInviteTemplate = createGuestInviteTemplate.Replace("{Link}", link);
                createGuestInviteTemplate = createGuestInviteTemplate.Replace("{FirstName}", firstName);

                return new SendEmailRequest
                {
                    To = new List<string> { email },
                    Subject = subject,
                    Content = createGuestInviteTemplate
                };
            }
            catch (Exception ex)
            {
                throw new BuildEmailException($"An error occurred while trying to build the {nameof(SendEmailRequest)}", ex);
            }
        }

        private static string GetContent(string relativePath)
        {
            var absolutePath = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, relativePath);
            using (var streamReader = new StreamReader(absolutePath))
            {
                var content = streamReader.ReadToEnd();
                return content;
            }
        }
    }
}
