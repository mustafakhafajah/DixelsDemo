using System;
using System.Net.Mail;
using System.Threading.Tasks;
using Dixels.Portal.Permissions;
using Microsoft.AspNetCore.Authorization;

namespace Dixels.Portal.Emailing;

[Authorize(PortalPermissions.Emailing.SendTest)]
public class EmailTestAppService : PortalAppService, IEmailTestAppService
{
    private readonly IEmailDeliveryProbe _probe;

    public EmailTestAppService(IEmailDeliveryProbe probe)
    {
        _probe = probe;
    }

    public async Task<EmailTestResultDto> SendTestEmailAsync(SendTestEmailInput input)
    {
        using var mail = new MailMessage
        {
            Subject = string.IsNullOrWhiteSpace(input.Subject) ? L["Email:TestSubject"] : input.Subject,
            Body = L["Email:TestBody", Clock.Now.ToString("yyyy-MM-dd HH:mm:ss")],
            IsBodyHtml = false
        };
        mail.To.Add(input.To);
        if (!string.IsNullOrWhiteSpace(input.ReplyTo)) mail.ReplyToList.Add(input.ReplyTo);

        var report = await _probe.SendAndReportAsync(mail);

        return new EmailTestResultDto
        {
            Succeeded = report.Succeeded,
            SendingEnabled = report.SendingEnabled,
            AuthType = report.AuthType,
            ServiceMailbox = report.ServiceMailbox,
            Host = report.Host,
            Port = report.Port,
            ServerResponse = report.ServerResponse,
            StatusCode = report.StatusCode,
            EnhancedStatusCode = report.EnhancedStatusCode,
            IsTransient = report.IsTransient,
            ErrorMessage = report.ErrorMessage,
            SettingName = report.SettingName,
            Hint = Hint(report),
            ElapsedMilliseconds = report.ElapsedMilliseconds
        };
    }

    /* The failures IT is most likely to meet when setting up Exchange Online (docs/DEPLOYMENT.md has the full list). */
    private string? Hint(EmailDeliveryReport report)
    {
        if (!report.SendingEnabled) return L["Email:Hint:Disabled"];
        if (report.Succeeded) return null;
        if (report.SettingName != null) return L["Email:Hint:Setting"];

        return report.EnhancedStatusCode switch
        {
            "5.7.139" or "5.7.3" => L["Email:Hint:5.7.139"],
            "5.7.60" => L["Email:Hint:5.7.60"],
            "4.3.2" => L["Email:Hint:4.3.2"],
            "5.2.0" => L["Email:Hint:5.2.0"],
            _ when report.ErrorMessage?.Contains("AADSTS", StringComparison.Ordinal) == true
                || report.ErrorMessage?.Contains("Entra ID", StringComparison.Ordinal) == true => L["Email:Hint:EntraId"],
            _ => L["Email:Hint:Other"]
        };
    }
}
