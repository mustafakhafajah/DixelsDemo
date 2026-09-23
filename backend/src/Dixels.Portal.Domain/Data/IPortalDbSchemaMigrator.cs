using System.Threading.Tasks;

namespace Dixels.Portal.Data;

public interface IPortalDbSchemaMigrator
{
    Task MigrateAsync();
}
