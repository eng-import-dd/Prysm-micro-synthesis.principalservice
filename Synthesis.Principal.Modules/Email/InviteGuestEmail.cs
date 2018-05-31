using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using Nancy.Helpers;
using Synthesis.EmailService.InternalApi.Models;
using Synthesis.PrincipalService.Exceptions;

namespace Synthesis.PrincipalService.Email
{
    public class InviteGuestEmail
    {
        public static SendEmailRequest BuildRequest(string projectName, string projectCode, string guestEmail, string from)
        {
            try
            {
                var subject = "Prysm Guest Invite: " + projectName;
                var link = $"{ConfigurationManager.AppSettings.Get("BaseWebClientUrl")}/#/guest?accesscode={projectCode}&email={HttpUtility.UrlEncode(guestEmail)}";

                var inviteGuestTemplate = GetContent("Email/Templates/GuestInvite.html");
                inviteGuestTemplate = inviteGuestTemplate.Replace("{Link}", link);
                inviteGuestTemplate = inviteGuestTemplate.Replace("{Name}", link);
                inviteGuestTemplate = inviteGuestTemplate.Replace("{ProjectName}", projectName);
                inviteGuestTemplate = inviteGuestTemplate.Replace("{ProjectCode}", projectCode.Insert(7, " ").Insert(3, " "));

                return new SendEmailRequest
                {
                    To = new List<string> { guestEmail },
                  //  From = from,
                    Subject = subject,
                    Content = inviteGuestTemplate
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
