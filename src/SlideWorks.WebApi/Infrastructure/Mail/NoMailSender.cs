using Agoda.IoC.Core;

namespace SlideWorks.WebApi.Infrastructure.Mail;

// Used when no SMTP host is set: sharing still works, nothing is sent. Also the IMailSender registration: Selector picks
// this or SmtpMailSender from MailSettings, so each is registered as itself.
[RegisterSingleton(Concrete = true)]
[RegisterSingleton(For = typeof(IMailSender), Factory = typeof(Selector))]
public sealed class NoMailSender : IMailSender
{
    public Task SendAsync(MailMessageModel message, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public sealed class Selector : IComponentFactory<IMailSender>
    {
        public IMailSender Build(IComponentResolver c)
            => c.Resolve<MailSettings>().IsConfigured ? c.Resolve<SmtpMailSender>() : c.Resolve<NoMailSender>();
    }
}
