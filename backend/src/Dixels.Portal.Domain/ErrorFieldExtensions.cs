using Volo.Abp;

namespace Dixels.Portal;

/* Tells the SPA which form field an error is about, so it can show the message right under that field.
 * The key travels in the error's data as "field" (camelCase, e.g. "name", "closeHourOverride", "start"). */
public static class ErrorFieldExtensions
{
    public const string FieldKey = "field";

    public static UserFriendlyException ForField(this UserFriendlyException exception, string field)
    {
        exception.WithData(FieldKey, field);
        return exception;
    }
}
