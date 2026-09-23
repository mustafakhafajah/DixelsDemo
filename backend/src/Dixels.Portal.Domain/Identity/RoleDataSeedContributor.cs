using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace Dixels.Portal.Identity;

/* Seeds the application roles. "admin" already exists - it's created
 * automatically by ABP's own IdentityDataSeedContributor, along with the
 * default admin user. This contributor only adds the roles specific to
 * this app. Runs automatically alongside every other IDataSeedContributor
 * whenever Dixels.Portal.DbMigrator seeds the database. */
public class RoleDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IdentityRoleManager _roleManager;
    private readonly IGuidGenerator _guidGenerator;

    public RoleDataSeedContributor(IdentityRoleManager roleManager, IGuidGenerator guidGenerator)
    {
        _roleManager = roleManager;
        _guidGenerator = guidGenerator;
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        await CreateRoleIfNotExistsAsync("employee", context.TenantId);
    }

    private async Task CreateRoleIfNotExistsAsync(string roleName, Guid? tenantId)
    {
        if (await _roleManager.FindByNameAsync(roleName) != null)
        {
            return;
        }

        var role = new IdentityRole(_guidGenerator.Create(), roleName, tenantId)
        {
            IsPublic = true
        };

        var result = await _roleManager.CreateAsync(role);
        if (!result.Succeeded)
        {
            throw new AbpException(
                $"Could not create role '{roleName}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }
}
