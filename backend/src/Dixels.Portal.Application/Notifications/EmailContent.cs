using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Dixels.Portal.Notifications;

public record EmailContent(string Subject, string Html);

/* The look shared by every portal email: a heading, a few short paragraphs and at most one button. Plain inline
 * styles, because mail programs ignore style sheets. Paragraphs are passed already HTML-encoded (see Encode). */
public static class PortalEmailLayout
{
    public static string Html(string heading, IEnumerable<string> paragraphsHtml, string? buttonText = null, string? buttonUrl = null)
    {
        var paragraphs = string.Concat(paragraphsHtml.Select(p => $"<p style=\"margin:0 0 14px\">{p}</p>"));
        var button = buttonText != null && !string.IsNullOrWhiteSpace(buttonUrl)
            ? $"<p style=\"margin:22px 0 6px\"><a href=\"{Encode(buttonUrl)}\" style=\"background:#1f6feb;color:#ffffff;padding:10px 18px;border-radius:6px;text-decoration:none;display:inline-block\">{Encode(buttonText)}</a></p>"
            : "";
        return "<!DOCTYPE html><html><body style=\"margin:0;padding:24px;background:#f5f6f8;font-family:Segoe UI,Arial,sans-serif;color:#1f2328;font-size:15px;line-height:1.5\">"
             + "<div style=\"max-width:560px;margin:0 auto;background:#ffffff;border-radius:8px;padding:28px\">"
             + $"<h1 style=\"font-size:20px;margin:0 0 18px\">{Encode(heading)}</h1>"
             + paragraphs + button
             + "<p style=\"margin:26px 0 0;color:#6e7781;font-size:12px\">Dixels Portal · This is an automatic email; replies are not read.</p>"
             + "</div></body></html>";
    }

    public static string Encode(string? text) => WebUtility.HtmlEncode(text ?? "");
}
