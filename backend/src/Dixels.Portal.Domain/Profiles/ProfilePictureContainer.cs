using Volo.Abp.BlobStoring;

namespace Dixels.Portal.Profiles;

/* Where people's profile pictures are kept: one picture per person, named by their user id. ABP keeps each tenant's
 * pictures apart (host/... and tenants/{id}/...). Which storage it uses is set by the host (the Web module). */
[BlobContainerName(ProfilePictureConsts.ContainerName)]
public class ProfilePictureContainer
{
}
