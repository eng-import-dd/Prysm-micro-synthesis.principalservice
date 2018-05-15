using System.Threading.Tasks;
using Moq;
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

        public EmailSendingServiceTests()
        {
            _target = new EmailSendingService(_emailApiMock.Object);
        }

        [Fact]
        public async Task SendGuestVerificationEmailAsyncSendsEmail()
        {
            await _target.SendGuestVerificationEmailAsync("first_name", "abc@xyz.com");

            _emailApiMock.Verify(x => x.SendEmailAsync(It.IsAny<SendEmailRequest>()));
        }
    }
}
