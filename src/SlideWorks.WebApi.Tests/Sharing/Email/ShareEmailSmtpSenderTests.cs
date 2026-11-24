using MailKit.Security;
using SlideWorks.WebApi.Infrastructure.Mail;

namespace SlideWorks.WebApi.Tests.Sharing.Email;

// SmtpMailSender without a network: the MIME it builds, and the TLS mode it picks per port.
[TestFixture]
public class ShareEmailSmtpSenderTests
{
    [Test]
    public void Build_WhenUnsubscribeUrlSet_ShouldSetListUnsubscribeAndBothParts()
    {
        var model = new MailMessageModel("to@x.example", "Sam shared a deck with you", "plain", "<p>html</p>", "https://h.example/unsubscribe#t=abc");

        var mime = SmtpMailSender.Build("SlideWorks <share@mail.slideworks.example>", model);

        mime.Headers["List-Unsubscribe"].ShouldBe("<https://h.example/unsubscribe#t=abc>");
        mime.From.Mailboxes.Single().Name.ShouldBe("SlideWorks");
        mime.From.Mailboxes.Single().Address.ShouldBe("share@mail.slideworks.example");
        mime.To.Mailboxes.Single().Address.ShouldBe("to@x.example");
        mime.Subject.ShouldBe("Sam shared a deck with you");
        mime.TextBody.ShouldBe("plain");
        mime.HtmlBody.ShouldBe("<p>html</p>");
    }

    [Test]
    public void Build_WhenNoSender_ShouldThrow()
    {
        var model = new MailMessageModel("to@x.example", "s", "t", "h", null);

        Should.Throw<InvalidOperationException>(() => SmtpMailSender.Build(null, model));
    }

    [Test]
    public void Build_WhenRecipientIsNotAMailbox_ShouldThrow()
    {
        var model = new MailMessageModel("a@x.example\r\nBcc: b@y.example", "s", "t", "h", null);

        Should.Throw<Exception>(() => SmtpMailSender.Build("a@b.example", model));
    }

    [TestCase(587, SecureSocketOptions.StartTls)]
    [TestCase(25, SecureSocketOptions.StartTls)]
    [TestCase(465, SecureSocketOptions.SslOnConnect)]
    public void TlsFor_WhenPort_ShouldPickTheMode(int port, SecureSocketOptions expected)
    {
        SmtpMailSender.TlsFor(port).ShouldBe(expected);
    }
}
