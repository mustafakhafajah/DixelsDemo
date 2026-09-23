using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Timing;
using Volo.Abp.Users;

namespace Dixels.Portal.Estate;

public class ActivityLogAppender : ITransientDependency
{
    private readonly IRepository<ActivityLogEntry, Guid> _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IGuidGenerator _guids;

    public ActivityLogAppender(IRepository<ActivityLogEntry, Guid> repository, ICurrentUser currentUser,
        IClock clock, IGuidGenerator guids)
    {
        _repository = repository;
        _currentUser = currentUser;
        _clock = clock;
        _guids = guids;
    }

    public async Task LogAsync(string action, string entityType, Guid entityId, string detail)
    {
        var detailText = detail.Length > EstateConsts.MaxDetailLength ? detail[..EstateConsts.MaxDetailLength] : detail;
        await _repository.InsertAsync(new ActivityLogEntry(_guids.Create())
        {
            TimestampUtc = _clock.Now,
            ActorUserId = _currentUser.Id ?? Guid.Empty,
            ActorName = _currentUser.Name ?? _currentUser.UserName ?? "system",
            Action = action,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            Detail = detailText,
        });
    }
}
