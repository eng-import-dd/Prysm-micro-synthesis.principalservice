using System;
using System.Threading.Tasks;
using Moq;
using Synthesis.Configuration;
using Synthesis.EmailService.InternalApi.Api;
using Synthesis.EmailService.InternalApi.Models;
using Synthesis.PrincipalService.Email;
using Xunit;

namespace Synthesis.PrincipalService.Modules.Test.Email
{
    public class EmailSendingServiceTests
    {
        private readonly IEmailSendingService _target;

        private readonly Mock<IEmailApi> _emailApiMock = new Mock<IEmailApi>();
        private readonly Mock<IAppSettingsReader> _appSettingsReaderMock = new Mock<IAppSettingsReader>();

        public EmailSendingServiceTests()
        {
            _target = new EmailSendingService(_emailApiMock.Object, _appSettingsReaderMock.Object);
        }

        [Fact]
        public async Task SendGuestVerificationEmailAsyncSendsEmail()
        {
            await _target.SendGuestVerificationEmailAsync("first_name", "abc@xyz.com", "redirect", Guid.NewGuid());

            _emailApiMock.Verify(x => x.SendEmailAsync(It.IsAny<SendEmailRequest>()));
        }
    }
}
