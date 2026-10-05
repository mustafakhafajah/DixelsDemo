namespace Dixels.Portal.Profiles;

/* A person's own profile picture, kept with ABP's BLOB storing (on disk under App_Data on the server). */
public static class ProfilePictureConsts
{
    /* ABP's BLOB container name; the folder name under the storage root. */
    public const string ContainerName = "profile-pictures";

    /* The SPA shrinks a picture to 512 px before sending it, which is far below this. */
    public const int MaxBytes = 2 * 1024 * 1024;
    public const int MaxMegabytes = MaxBytes / (1024 * 1024);

    /* Kept on the user (in ExtraProperties, no column of its own) and changed on every upload, so a new picture is
     * fetched at once instead of an old one being reused. Null when the person has no picture. */
    public const string VersionPropertyName = "ProfilePictureVersion";
}
