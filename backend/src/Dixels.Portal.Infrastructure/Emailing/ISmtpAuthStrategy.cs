using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Dixels.Portal.Emailing;

/* One way of signing in to the SMTP server on an already-connected client. SmtpAuthStrategyResolver picks the one
 * matching the ExchangeOnline.AuthType setting. Each implementation is exposed as ISmtpAuthStrategy so the resolver
 * receives all of them. */
public interface ISmtpAuthStrategy : ITransientDependency
{
    ExchangeAuthType AuthType { get; }

    Task AuthenticateAsync(MailKit.Net.Smtp.SmtpClient client, CancellationToken cancellationToken = default);
}
