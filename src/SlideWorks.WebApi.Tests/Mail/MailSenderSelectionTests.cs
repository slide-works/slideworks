using Microsoft.Extensions.DependencyInjection;
using SlideWorks.WebApi.Infrastructure.Mail;

namespace SlideWorks.WebApi.Tests.Mail;

// Which IMailSender the app resolves from the settings. Resolve only: nothing here sends.
public class MailSenderSelectionTests
{
    [TestFixture]
    public class WithoutSmtpHost : ApiTestFixture
    {
        [Test]
        public void Resolve_WhenNoSmtpHost_ShouldUseNoMailSender()
        {
            using var scope = Factory.Services.CreateScope();

            scope.ServiceProvider.GetRequiredService<IMailSender>().ShouldBeOfType<NoMailSender>();
        }
    }

    [TestFixture]
    public class WithSmtpHost : ApiTestFixture
    {
        protected override Dictionary<string, string?> ExtraConfig => new() { [MailSettings.HOST_KEY] = "smtp.mail.example" };

        [Test]
        public void Resolve_WhenSmtpHostSet_ShouldUseSmtpSender()
        {
            using var scope = Factory.Services.CreateScope();

            scope.ServiceProvider.GetRequiredService<IMailSender>().ShouldBeOfType<SmtpMailSender>();
        }
    }
}
