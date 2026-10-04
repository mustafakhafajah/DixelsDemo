using System;
using System.Linq;

namespace Dixels.Portal.Emailing;

/* Exchange Online only accepts a From address the signed-in mailbox may send as; anything else is rejected with
 * 5.7.60 after the connection is made. Checking it here gives a clear error before any connection, and keeps the
 * code from sending as an address IT never approved. */
public static class SenderAddressPolicy
{
    /* True when `from` is the service mailbox or one of the comma-separated allowed addresses (case-insensitive). */
    public static bool IsAllowed(string from, string serviceMailbox, string? allowedFromAddresses)
    {
        if (string.Equals(from, serviceMailbox, StringComparison.OrdinalIgnoreCase)) return true;

        return (allowedFromAddresses ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(alias => string.Equals(from, alias, StringComparison.OrdinalIgnoreCase));
    }
}
