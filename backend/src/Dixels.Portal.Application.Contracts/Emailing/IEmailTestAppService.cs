using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.Emailing;

/* Checks that email sending works: sends one email straight away (not queued) and returns what the mail server said,
 * so whoever sets up Exchange Online can see the exact reply and a hint for common problems. */
public interface IEmailTestAppService : IApplicationService
{
    Task<EmailTestResultDto> SendTestEmailAsync(SendTestEmailInput input);
}
