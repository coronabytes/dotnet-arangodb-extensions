using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Arango.Migration
{
    public interface IArangoMigrator
    {
        /// <summary>
        ///  Name of the migration history collection
        /// </summary>
        string HistoryCollection { get; set; }

        /// <summary>
        ///  Compare two objects via the default serializer
        /// </summary>
        bool Compare(object a, object b);

        /// <summary>
        ///  Get structure from database
        /// </summary>
        Task<ArangoStructure> GetStructureAsync(ArangoHandle db,
            CancellationToken cancellationToken = default);

        /// <summary>
        ///  Apply structure to database
        /// </summary>
        Task ApplyStructureAsync(ArangoHandle db, ArangoStructure update,
            ArangoMigrationOptions options = null);

        /// <summary>
        ///     Add Migrations from assembly
        /// </summary>
        void AddMigrations(Assembly assembly);

        /// <summary>
        ///     Add migration manually
        /// </summary>
        void AddMigration(IArangoMigration migration);

        /// <summary>
        ///     Apply missing migrations up to latest
        /// </summary>
        Task UpgradeAsync(ArangoHandle db);

        /// <summary>
        ///     Export database to zip archive stream
        /// </summary>
        Task ExportAsync(ArangoHandle db, Stream output, ArangoMigrationScope scope);

        /// <summary>
        ///     Import database from zip archive stream and replace existing data
        /// </summary>
        Task ImportAsync(ArangoHandle db, Stream input, ArangoMigrationScope scope);
    }
}