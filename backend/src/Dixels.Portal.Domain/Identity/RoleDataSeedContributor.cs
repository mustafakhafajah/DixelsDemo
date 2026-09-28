using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.Uow;
using IdentityRole = Volo.Abp.Identity.IdentityRole;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace Dixels.Portal.Identity;

/* Seeds the application roles and a test employee. "admin" already exists - it's created
 * automatically by ABP's own IdentityDataSeedContributor, along with the default admin user.
 * This contributor adds the "employee" role (with what an employee may do) and one employee
 * login to try the app with. Runs automatically alongside every other IDataSeedContributor
 * whenever Dixels.Portal.DbMigrator seeds the database, and is safe to run again. */
public class RoleDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string EmployeeRole = "employee";
    public const string EmployeeUserName = "employee";
    public const string EmployeeEmail = "employee@dixels.io";
    public const string EmployeePassword = "1q2w3E*";

    /* What an employee may do: see the estate and blocked time, and book / change / cancel their
     * own bookings. Not Bookings.ManageAll and no estate Create/Edit/Delete - that is the admin.
     * Written as strings because PortalPermissions lives in Application.Contracts, which the
     * Domain project cannot reference; keep them in step with PortalPermissions.cs
     * (EmployeeSeedTests fails if a name here is not a defined permission). */
    public static readonly string[] EmployeePermissions =
    {
        "Portal.Buildings",
        "Portal.Floors",
        "Portal.Spaces",
        "Portal.SpaceTypes",
        "Portal.Maintenance",
        "Portal.Bookings",
        "Portal.Bookings.Create",
        "Portal.Bookings.Edit",
        "Portal.Bookings.Delete",
    };

    private readonly IdentityRoleManager _roleManager;
    private readonly IdentityUserManager _userManager;
    private readonly IPermissionDataSeeder _permissionSeeder;
    private readonly IGuidGenerator _guidGenerator;

    public RoleDataSeedContributor(IdentityRoleManager roleManager, IdentityUserManager userManager,
        IPermissionDataSeeder permissionSeeder, IGuidGenerator guidGenerator)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _permissionSeeder = permissionSeeder;
        _guidGenerator = guidGenerator;
    }

    [UnitOfWork]
    public virtual async Task SeedAsync(DataSeedContext context)
    {
        await EnsureEmployeeRoleAsync(context.TenantId);
        /* Only grants what is missing, so running the DbMigrator again changes nothing. */
        await _permissionSeeder.SeedAsync(RolePermissionValueProvider.ProviderName, EmployeeRole,
            EmployeePermissions, context.TenantId);
        await EnsureEmployeeUserAsync(context.TenantId);
    }

    /* Default role: people who register themselves become employees instead of having no role. */
    private async Task EnsureEmployeeRoleAsync(Guid? tenantId)
    {
        var role = await _roleManager.FindByNameAsync(EmployeeRole);
        if (role == null)
        {
            role = new IdentityRole(_guidGenerator.Create(), EmployeeRole, tenantId) { IsPublic = true, IsDefault = true };
            Check(await _roleManager.CreateAsync(role), $"create role '{EmployeeRole}'");
            return;
        }

        if (!role.IsDefault)
        {
            role.IsDefault = true;
            Check(await _roleManager.UpdateAsync(role), $"update role '{EmployeeRole}'");
        }
    }

    /* An existing employee user is left as it is (its password is never reset). */
    private async Task EnsureEmployeeUserAsync(Guid? tenantId)
    {
        if (await _userManager.FindByEmailAsync(EmployeeEmail) != null)
        {
            return;
        }

        var user = new IdentityUser(_guidGenerator.Create(), EmployeeUserName, EmployeeEmail, tenantId)
        {
            Name = "Employee"
        };
        user.SetEmailConfirmed(true);
        Check(await _userManager.CreateAsync(user, EmployeePassword), $"create user '{EmployeeEmail}'");
        Check(await _userManager.AddToRoleAsync(user, EmployeeRole), $"add '{EmployeeEmail}' to '{EmployeeRole}'");
    }

    private static void Check(IdentityResult result, string what)
    {
        if (!result.Succeeded)
        {
            throw new AbpException($"Could not {what}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }
}
