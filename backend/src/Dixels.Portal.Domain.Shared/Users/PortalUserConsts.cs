namespace Dixels.Portal.Users;

/* What the portal adds to ABP's user. The address is an ABP extra property: it lives in the user's
 * ExtraProperties column, so it needs no column of its own, and ABP's Users page shows it on its form. */
public static class PortalUserConsts
{
    public const string AddressPropertyName = "Address";
    public const int MaxAddressLength = 256;

    /* A phone number as digits, optionally after a "+": 6 to 15 digits, which with the "+" fits ABP's
     * 16-character phone field (E.164 allows at most 15 digits). */
    public const string PhoneNumberPattern = @"^\+?[0-9]{6,15}$";
}
