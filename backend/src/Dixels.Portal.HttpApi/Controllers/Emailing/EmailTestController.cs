using System.Threading.Tasks;
using Dixels.Portal.Emailing;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace Dixels.Portal.Controllers.Emailing;

[RemoteService(Name = "Default")]
[Area("app")]
[Route("api/app/email-test")]
public class EmailTestController : PortalController, IEmailTestAppService
{
    private readonly IEmailTestAppService _emailTest;

    public EmailTestController(IEmailTestAppService emailTest)
    {
        _emailTest = emailTest;
    }

    /* { to, subject?, replyTo? } → the mail server's reply; a failed send is still 200 with succeeded: false. */
    [HttpPost]
    public Task<EmailTestResultDto> SendTestEmailAsync([FromBody] SendTestEmailInput input) => _emailTest.SendTestEmailAsync(input);
}
