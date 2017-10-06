using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using Nancy.Helpers;
using Synthesis.Logging;
using Synthesis.PrincipalService.Dao.Models;
using Synthesis.PrincipalService.Responses;

namespace Synthesis.PrincipalService.Utilities
{
    public class EmailUtility : IEmailUtility
    {
        private readonly ILogger _loggingService;

        private readonly string _emailTemplate;
        private readonly string _guestInviteEmail;
        private readonly string _createGuestInviteEmail;
        private readonly string _resetPasswordEmail;
        private readonly string _emailHostEmail;
        private readonly string _shareContentEmail;
        private readonly string _userInviteEmail;
        private readonly string _userWelcomeEmail;
        private readonly string _userLockedEmail;

        private readonly LinkedResource _facebookIcon;
        private readonly LinkedResource _googlePlusIcon;
        private readonly LinkedResource _linkedInIcon;
        private readonly LinkedResource _prysmLogo;
        private readonly LinkedResource _twitterIcon;
        private readonly LinkedResource _youtubeIcon;

        private readonly List<LinkedResource> _linkedResources = new List<LinkedResource>();

        public EmailUtility(ILogger loggingService)
        {
            _loggingService = loggingService;

            _prysmLogo = new LinkedResource(MapPath("EmailTemplates/Images/Prysm-logo.png"), "image/png");
            _facebookIcon = new LinkedResource(MapPath("EmailTemplates/Images/facebook-icon.png"), "image/png");
            _googlePlusIcon = new LinkedResource(MapPath("EmailTemplates/Images/google-plus-icon.png"), "image/png");
            _linkedInIcon = new LinkedResource(MapPath("EmailTemplates/Images/linkedin-icon.png"), "image/png");
            _twitterIcon = new LinkedResource(MapPath("EmailTemplates/Images/twitter-icon.png"), "image/png");
            _youtubeIcon = new LinkedResource(MapPath("EmailTemplates/Images/youtube-icon.png"), "image/png");

            _linkedResources.Add(_facebookIcon);
            _linkedResources.Add(_googlePlusIcon);
            _linkedResources.Add(_linkedInIcon);
            _linkedResources.Add(_prysmLogo);
            _linkedResources.Add(_twitterIcon);
            _linkedResources.Add(_youtubeIcon);

            using (var streamReader = new StreamReader(MapPath("EmailTemplates/EmailTemplate.html")))
            {
                _emailTemplate = streamReader.ReadToEnd();
            }

            _guestInviteEmail = GetContent("EmailTemplates/GuestInvite.html");
            _createGuestInviteEmail = GetContent("EmailTemplates/VerifyNewAccount.html");
            _resetPasswordEmail = GetContent("EmailTemplates/ResetPassword.html");
            _emailHostEmail = GetContent("EmailTemplates/EmailHost.html");
            _shareContentEmail = GetContent("EmailTemplates/ShareContent.html");
            _userInviteEmail = GetContent("EmailTemplates/UserInvite.html");
            _userWelcomeEmail = GetContent("EmailTemplates/WelcomeUser.html");
            _userLockedEmail = GetContent("EmailTemplates/UserLockedEmail.html");
        }

        private string GetContent(string path)
        {
            using (var streamReader = new StreamReader(MapPath(path)))
            {
                var content = streamReader.ReadToEnd();
                content = _emailTemplate.Replace("{{CONTENT}}", content);
                content = AddLinkedResources(content);
                return content;
            }
        }

        private string AddLinkedResources(string email)
        {
            email = email.Replace("{{prysm-logo}}", string.Format("cid:{0}", _prysmLogo.ContentId));
            email = email.Replace("{{facebook-icon}}", string.Format("cid:{0}", _facebookIcon.ContentId));
            email = email.Replace("{{google-plus-icon}}", string.Format("cid:{0}", _googlePlusIcon.ContentId));
            email = email.Replace("{{linkedin-icon}}", string.Format("cid:{0}", _linkedInIcon.ContentId));
            email = email.Replace("{{twitter-icon}}", string.Format("cid:{0}", _twitterIcon.ContentId));
            email = email.Replace("{{youtube-icon}}", string.Format("cid:{0}", _youtubeIcon.ContentId));
            return email;
        }

        public async Task<bool> SendGuestInviteAsync(string projectName, string projectCode, string guestEmail, string @from)
        {
            try
            {
                var subject = "Prysm Guest Invite: " + projectName;

                var replacedContent = _guestInviteEmail.Replace("{Link}", string.Format("{0}/#/guest?accesscode={1}&email={2}",
                    ConfigurationManager.AppSettings.Get("BaseWebClientUrl"),
                    projectCode,
                    HttpUtility.UrlEncode(guestEmail)));

                replacedContent = replacedContent.Replace("{Name}", from);
                replacedContent = replacedContent.Replace("{ProjectName}", projectName);
                replacedContent = replacedContent.Replace("{ProjectCode}", projectCode.Insert(7, " ").Insert(3, " "));

                await SendEmail(guestEmail, "", "", subject, replacedContent, "");
            }
            catch (Exception ex)
            {
                _loggingService.LogMessage(LogLevel.Error, "EMAIL", ex);
                return false;
            }

            return true;
        }

        public async Task<bool> SendResetPasswordEmailAsync(string email, string name, string link)
        {
            try
            {
                const string subject = "Reset Password";

                var replacedContent = _resetPasswordEmail.Replace("{Link}", link);
                replacedContent = replacedContent.Replace("{Name}", name);

                await SendEmail(email, "", "", subject, replacedContent, "");
            }
            catch (Exception ex)
            {
                _loggingService.LogMessage(LogLevel.Error, "EMAIL", ex);
                return false;
            }

            return true;
        }

        public async Task<bool> SendVerifyAccountEmailAsync(string firstName, string email, string accessCode, string emailVerificationId)
        {
            try
            {
                const string subject = "Almost there! Please verify your Prysm account.";

                var link = string.Format("{0}/#/guest?{1}email={2}&token={3}",
                    ConfigurationManager.AppSettings.Get("BaseWebClientUrl"),
                    (string.IsNullOrWhiteSpace(email) ? string.Empty : "accesscode=" + accessCode + "&"), HttpUtility.UrlEncode(email),
                    emailVerificationId);

                var replacedContent = _createGuestInviteEmail.Replace("{Link}", link);
                replacedContent = replacedContent.Replace("{FirstName}", firstName);

                await SendEmail(email, "", "", subject, replacedContent, "");
            }
            catch (Exception ex)
            {
                _loggingService.LogMessage(LogLevel.Error, "EMAIL", ex);
                return false;
            }

            return true;
        }

        public async Task<bool> SendHostEmailAsync(string email, string userFullName, string userFirstName, string userEmail, string projectName)
        {
            try
            {
                const string subject = "You have a guest waiting for you in the lobby";

                var replacedContent = _emailHostEmail.Replace("{FullName}", userFullName);
                replacedContent = replacedContent.Replace("{Project}", projectName);
                replacedContent = replacedContent.Replace("{HostEmail}", userEmail);
                replacedContent = replacedContent.Replace("{FirstName}", userFirstName);
                replacedContent = replacedContent.Replace("{WebClientLink}", ConfigurationManager.AppSettings.Get("BaseWebClientUrl"));

                await SendEmail(email, "", "", subject, replacedContent, "");
            }
            catch (Exception ex)
            {
                _loggingService.LogMessage(LogLevel.Error, "EMAIL", ex);
                return false;
            }

            return true;
        }

        public async Task<bool> SendContentAsync(IEnumerable<string> emailAddresses, IEnumerable<Attachment> attachments, string fromFullName)
        {
            try
            {
                const string subject = "Content From Prysm Has Been Shared With You";

                string[] cc = {""};
                string[] bcc = {""};

                var replacedContent = _shareContentEmail.Replace("{Name}", fromFullName);

                var attachmentList = attachments as IList<Attachment> ?? attachments.ToList();
                if (attachmentList.FirstOrDefault() != null)
                {
                    replacedContent = replacedContent.Replace("{Images}",
                        string.Join("<br/><br/>",
                            attachmentList.Select(
                                i => string.Format("<img src=\"cid:{1}\" alt={0} width=\"400\"/>", i.Name, i.Name))));
                }

                await SendEmail(emailAddresses, cc, bcc, subject, replacedContent, "", true, attachmentList);
            }
            catch (Exception ex)
            {
                _loggingService.LogMessage(LogLevel.Error, "EMAIL", ex);
                return false;
            }

            return true;
        }

        public async Task<bool> SendUserInviteAsync(List<UserInviteResponse> newInvitedUsers)
        {
            if (newInvitedUsers == null)
            {
                throw new ArgumentNullException(nameof(newInvitedUsers));
            }

            try
            {
                foreach (var user in newInvitedUsers)
                {
                    var subject = "Prysm New User Invite";

                    // Using a temp mail template until actual template is available
                    var replacedContent = _userInviteEmail.Replace("{Link}",
                        $"{ConfigurationManager.AppSettings.Get("BaseWebClientUrl")}");

                    replacedContent = replacedContent.Replace("{Firstname}", user.FirstName);

                    await SendEmail(user.Email, "", "", subject, replacedContent, "");
                    user.LastInvitedDate = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                _loggingService.LogMessage(LogLevel.Error, "EMAIL", ex);
                return false;
            }

            return true;
        }

        public async Task<bool> SendWelcomeEmailAsync(string email, string firstname)
        {
            try
            {
                var subject = "Welcome to Prysm Workplace ";

                var replacedContent = _userWelcomeEmail.Replace("{Link}", ConfigurationManager.AppSettings.Get("BaseWebClientUrl"));

                replacedContent = replacedContent.Replace("{Firstname}", firstname);

                await SendEmail(email, "", "", subject, replacedContent, "");
            }
            catch (Exception ex)
            {
                _loggingService.LogMessage(LogLevel.Error, "EMAIL", ex);
                return false;
            }

            return true;

        }

        public async Task<bool> SendUserLockedMailAsync(List<User> orgAdmins, string userfullname, string useremail)
        {
            try
            {
                const string subject = "User is Locked";

                var replacedContent = _userLockedEmail.Replace("{Link}", ConfigurationManager.AppSettings.Get("BaseWebClientUrl"));
                replacedContent = replacedContent.Replace("{LockedUserName}", userfullname);
                replacedContent = replacedContent.Replace("{LockedUserEmail}", useremail);

                foreach (var orgAdmin in orgAdmins)
                {
                    replacedContent = replacedContent.Replace("{Firstname}", orgAdmin.FirstName);
                    await SendEmail(orgAdmin.Email, "", "", subject, replacedContent, "");
                }
               
            }
            catch (Exception ex)
            {
                _loggingService.LogMessage(LogLevel.Error, "EMAIL", ex);
                return false;
            }

            return true;
        }

        #region Send Emails

        private async Task SendEmail(string toEmail, string ccEmail, string bccEmail, string subject, string htmlBody,
            string textBody)
        {
            string[] to = {toEmail};
            string[] cc = {ccEmail};
            string[] bcc = {bccEmail};

            await SendEmail(to, cc, bcc, subject, htmlBody, textBody, true, new List<Attachment>());
        }

        private async Task SendEmail(IEnumerable<string> toEmail, IEnumerable<string> ccEmail, IEnumerable<string> bccEmail, string subject,
            string htmlBody, string textBody, bool asHtml, IEnumerable<Attachment> attachments)
        {
            MailMessage message = new MailMessage();

            foreach (string email in toEmail)
            {
                message.To.Add(new MailAddress(email));
            }

            foreach (string email in ccEmail)
            {
                if (email != "")
                {
                    message.CC.Add(new MailAddress(email));
                }
            }

            foreach (string email in bccEmail)
            {
                if (email != "")
                {
                    message.Bcc.Add(new MailAddress(email));
                }
            }

            message.Priority = MailPriority.Normal;
            message.IsBodyHtml = asHtml;
            message.Subject = subject;
            AlternateView plain = AlternateView.CreateAlternateViewFromString(textBody, new System.Net.Mime.ContentType("text/plain"));
            AlternateView html = AlternateView.CreateAlternateViewFromString(htmlBody, new System.Net.Mime.ContentType("text/html"));
            message.AlternateViews.Add(plain);
            message.AlternateViews.Add(html);

            foreach (var attachment in attachments)
            {                
                message.Attachments.Add(attachment);

                // Inline and non-inline attachements can't share a stream
                MemoryStream duplicateStream = new MemoryStream();
                attachment.ContentStream.CopyTo(duplicateStream);
                attachment.ContentStream.Position = 0;
                duplicateStream.Position = 0;

                var resource = new LinkedResource(duplicateStream, "image/png")
                {
                    ContentId = attachment.Name
                };
                html.LinkedResources.Add(resource);
            }

            foreach (var linkedResource in _linkedResources)
            {
                html.LinkedResources.Add(linkedResource);
            }

            SmtpClient client = new SmtpClient();

            try
            {
                await client.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                _loggingService.LogMessage(LogLevel.Error, "First Attempt of sending email failed", ex);
                client.Send(message);
                _loggingService.LogMessage(LogLevel.Error, "Second Attempt of sending email succeeded", ex);
            }
        }

        #endregion 

        private string MapPath(string relativePath)
        {
            return Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, relativePath);
        }
       
    }
}