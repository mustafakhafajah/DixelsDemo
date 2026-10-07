using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace Dixels.Portal.Common;

/* Text: the same email without HTML, for the plain-text part a calendar invitation carries (see PortalEmailLayout.ToText). */
public record EmailContent(string Subject, string Html)
{
    public string Text => PortalEmailLayout.ToText(Html);
}

/* A button at the end of an email. */
public record EmailButton(string Text, string? Url);

/* The look shared by every portal email: a heading, a few short paragraphs and a row of buttons (the first one
 * filled, the rest outlined, e.g. Accept / Tentative / Decline). Plain inline styles, because mail programs ignore
 * style sheets. Paragraphs are passed already HTML-encoded (see Encode). */
public static class PortalEmailLayout
{
    private const string Primary = "background:#1f6feb;color:#ffffff;border:1px solid #1f6feb";
    private const string Outlined = "background:#ffffff;color:#1f2328;border:1px solid #d0d7de";

    public static string Html(string heading, IEnumerable<string> paragraphsHtml, string? buttonText = null, string? buttonUrl = null,
        string? secondButtonText = null, string? secondButtonUrl = null)
        => Html(heading, paragraphsHtml, new List<EmailButton?>
            {
                buttonText != null ? new EmailButton(buttonText, buttonUrl) : null,
                secondButtonText != null ? new EmailButton(secondButtonText, secondButtonUrl) : null,
            }.OfType<EmailButton>().ToList());

    /* A button without an address is left out. */
    public static string Html(string heading, IEnumerable<string> paragraphsHtml, IReadOnlyList<EmailButton> buttons)
    {
        var paragraphs = string.Concat(paragraphsHtml.Select(p => $"<p style=\"margin:0 0 14px\">{p}</p>"));
        var shown = buttons.Where(b => !string.IsNullOrWhiteSpace(b.Url)).ToList();
        var row = string.Concat(shown.Select((b, i) => Button(b.Text, b.Url!, i == 0 ? Primary : Outlined + ";margin-left:8px")));
        var button = row.Length > 0 ? $"<p style=\"margin:22px 0 6px\">{row}</p>" : "";
        return "<!DOCTYPE html><html><body style=\"margin:0;padding:24px;background:#f5f6f8;font-family:Segoe UI,Arial,sans-serif;color:#1f2328;font-size:15px;line-height:1.5\">"
             + "<div style=\"max-width:560px;margin:0 auto;background:#ffffff;border-radius:8px;padding:28px\">"
             + $"<h1 style=\"font-size:20px;margin:0 0 18px\">{Encode(heading)}</h1>"
             + paragraphs + button
             + "<p style=\"margin:26px 0 0;color:#6e7781;font-size:12px\">Dixels Portal · This is an automatic email; replies are not read.</p>"
             + "</div></body></html>";
    }

    public static string Encode(string? text) => WebUtility.HtmlEncode(text ?? "");

    /* Someone's own words in an email: encoded, with their line breaks kept. */
    public static string EncodeMultiline(string text)
        => string.Join("<br>", text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(Encode));

    /* The plain-text version of an email built by Html above: paragraphs and list items on their own lines, each
     * button as "Text: address", tags removed. Only ever given our own layout, so a few patterns are enough. */
    public static string ToText(string html)
    {
        var text = Regex.Replace(html, "<a [^>]*href=\"([^\"]*)\"[^>]*>(.*?)</a>", "$2: $1\n");
        text = Regex.Replace(text, "<li[^>]*>", "- ");
        text = Regex.Replace(text, "<br>|</li>", "\n");
        text = Regex.Replace(text, "</p>|</h1>|</ul>", "\n\n");
        text = Regex.Replace(text, "<[^>]+>", "");
        text = WebUtility.HtmlDecode(text);
        var lines = text.Replace("\r", "").Split('\n').Select(l => l.Trim());
        return Regex.Replace(string.Join("\n", lines), "\n{3,}", "\n\n").Trim() + "\n";
    }

    private static string Button(string text, string url, string colours)
        => $"<a href=\"{Encode(url)}\" style=\"{colours};padding:10px 18px;border-radius:6px;text-decoration:none;display:inline-block\">{Encode(text)}</a>";
}
