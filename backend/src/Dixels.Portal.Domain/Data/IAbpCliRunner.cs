namespace Dixels.Portal.Data;

/* Runs the ABP CLI. PortalDbMigrationService depends on this instead of starting cmd.exe or bash itself,
 * so the OS/process details live in one place and the service can be tested without launching anything. */
public interface IAbpCliRunner
{
    /* Creates the first EF Core migration in that project folder, then runs the DbMigrator. */
    void CreateMigrationAndRunMigrator(string entityFrameworkCoreProjectFolder);
}
