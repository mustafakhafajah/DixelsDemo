using Dixels.Portal.Estate;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.TenantManagement;
using Volo.Abp.TenantManagement.EntityFrameworkCore;

namespace Dixels.Portal.EntityFrameworkCore;

[ReplaceDbContext(typeof(IIdentityDbContext))]
[ReplaceDbContext(typeof(ITenantManagementDbContext))]
[ConnectionStringName("Default")]
public class PortalDbContext :
    AbpDbContext<PortalDbContext>,
    IIdentityDbContext,
    ITenantManagementDbContext
{
    public DbSet<Building> Buildings { get; set; }
    public DbSet<Floor> Floors { get; set; }
    public DbSet<Space> Spaces { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<MaintenanceWindow> MaintenanceWindows { get; set; }

    #region Entities from the modules

    /* Notice: We only implemented IIdentityDbContext and ITenantManagementDbContext
     * and replaced them for this DbContext. This allows you to perform JOIN
     * queries for the entities of these modules over the repositories easily. You
     * typically don't need that for other modules. But, if you need, you can
     * implement the DbContext interface of the needed module and use ReplaceDbContext
     * attribute just like IIdentityDbContext and ITenantManagementDbContext.
     *
     * More info: Replacing a DbContext of a module ensures that the related module
     * uses this DbContext on runtime. Otherwise, it will use its own DbContext class.
     */

    //Identity
    public DbSet<IdentityUser> Users { get; set; }
    public DbSet<IdentityRole> Roles { get; set; }
    public DbSet<IdentityClaimType> ClaimTypes { get; set; }
    public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
    public DbSet<IdentitySecurityLog> SecurityLogs { get; set; }
    public DbSet<IdentityLinkUser> LinkUsers { get; set; }
    public DbSet<IdentityUserDelegation> UserDelegations { get; set; }
    public DbSet<IdentitySession> Sessions { get; set; }
    // Tenant Management
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantConnectionString> TenantConnectionStrings { get; set; }

    #endregion

    public PortalDbContext(DbContextOptions<PortalDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        /* Include modules to your migration db context */

        builder.ConfigurePermissionManagement();
        builder.ConfigureSettingManagement();
        builder.ConfigureBackgroundJobs();
        builder.ConfigureAuditLogging();
        builder.ConfigureIdentity();
        builder.ConfigureOpenIddict();
        builder.ConfigureFeatureManagement();
        builder.ConfigureTenantManagement();

        ConfigureEstate(builder);
    }

    private static void ConfigureEstate(ModelBuilder builder)
    {
        builder.Entity<Building>(b =>
        {
            b.ToTable(PortalConsts.DbTablePrefix + "Buildings", PortalConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(EstateConsts.MaxNameLength);
            b.Property(x => x.TimeZone).IsRequired().HasMaxLength(EstateConsts.MaxTimeZoneLength);
            b.Property(x => x.Holidays).HasColumnType("date[]");
            b.HasIndex(x => x.Name).IsUnique();
        });

        builder.Entity<Floor>(b =>
        {
            b.ToTable(PortalConsts.DbTablePrefix + "Floors", PortalConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(EstateConsts.MaxFloorNameLength);
            b.HasIndex(x => new { x.BuildingId, x.Name }).IsUnique();
            b.HasOne<Building>().WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Space>(b =>
        {
            b.ToTable(PortalConsts.DbTablePrefix + "Spaces", PortalConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(EstateConsts.MaxNameLength);
            b.Property(x => x.TimeZone).IsRequired().HasMaxLength(EstateConsts.MaxTimeZoneLength);
            b.Property(x => x.Note).HasMaxLength(EstateConsts.MaxNoteLength);
            b.HasIndex(x => x.Name).IsUnique();
            b.HasIndex(x => new { x.BuildingId, x.FloorId, x.Status });
            b.HasOne<Building>().WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne<Floor>().WithMany().HasForeignKey(x => x.FloorId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Booking>(b =>
        {
            b.ToTable(PortalConsts.DbTablePrefix + "Bookings", PortalConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.IdempotencyKey).HasMaxLength(EstateConsts.MaxIdempotencyKeyLength);
            b.HasIndex(x => new { x.SpaceId, x.Status, x.StartUtc, x.EndUtc });
            b.HasIndex(x => new { x.OwnerUserId, x.Status, x.StartUtc, x.EndUtc });
            b.HasIndex(x => x.SeriesId);
            b.HasIndex(x => x.IdempotencyKey).IsUnique().HasFilter("\"IdempotencyKey\" IS NOT NULL");
            b.HasOne<Space>().WithMany().HasForeignKey(x => x.SpaceId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<MaintenanceWindow>(b =>
        {
            b.ToTable(PortalConsts.DbTablePrefix + "MaintenanceWindows", PortalConsts.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Note).HasMaxLength(EstateConsts.MaxNoteLength);
            b.HasIndex(x => new { x.SpaceId, x.Status, x.StartUtc, x.EndUtc });
            b.HasIndex(x => x.SeriesId);
            b.HasIndex(x => new { x.ScopeType, x.ScopeId });
            b.HasOne<Space>().WithMany().HasForeignKey(x => x.SpaceId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
