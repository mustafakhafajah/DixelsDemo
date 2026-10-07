using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Dixels.Portal.Common;

public record EmailContent(string Subject, string Html);

/* The look shared by every portal email: a heading, a few short paragraphs and up to two buttons (the second one
 * outlined, e.g. Accept and Refuse). Plain inline styles, because mail programs ignore style sheets. Paragraphs are
 * passed already HTML-encoded (see Encode). */
public static class PortalEmailLayout
{
    public static string Html(string heading, IEnumerable<string> paragraphsHtml, string? buttonText = null, string? buttonUrl = null,
        string? secondButtonText = null, string? secondButtonUrl = null)
    {
        var paragraphs = string.Concat(paragraphsHtml.Select(p => $"<p style=\"margin:0 0 14px\">{p}</p>"));
        var buttons = Button(buttonText, buttonUrl, "background:#1f6feb;color:#ffffff;border:1px solid #1f6feb")
                      + Button(secondButtonText, secondButtonUrl, "background:#ffffff;color:#1f2328;border:1px solid #d0d7de;margin-left:8px");
        var button = buttons.Length > 0 ? $"<p style=\"margin:22px 0 6px\">{buttons}</p>" : "";
        return "<!DOCTYPE html><html><body style=\"margin:0;padding:24px;background:#f5f6f8;font-family:Segoe UI,Arial,sans-serif;color:#1f2328;font-size:15px;line-height:1.5\">"
             + "<div style=\"max-width:560px;margin:0 auto;background:#ffffff;border-radius:8px;padding:28px\">"
             + $"<h1 style=\"font-size:20px;margin:0 0 18px\">{Encode(heading)}</h1>"
             + paragraphs + button
             + "<p style=\"margin:26px 0 0;color:#6e7781;font-size:12px\">Dixels Portal · This is an automatic email; replies are not read.</p>"
             + "</div></body></html>";
    }

    public static string Encode(string? text) => WebUtility.HtmlEncode(text ?? "");

    private static string Button(string? text, string? url, string colours)
        => text != null && !string.IsNullOrWhiteSpace(url)
            ? $"<a href=\"{Encode(url)}\" style=\"{colours};padding:10px 18px;border-radius:6px;text-decoration:none;display:inline-block\">{Encode(text)}</a>"
            : "";
}
