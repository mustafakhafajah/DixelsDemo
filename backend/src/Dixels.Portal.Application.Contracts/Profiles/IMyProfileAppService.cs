using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Dixels.Portal.Profiles;

/* The signed-in user's own record, read-only. Only ever about the caller: it takes no user id. */
public interface IMyProfileAppService : IApplicationService
{
    Task<MyProfileDto> GetAsync();
}
