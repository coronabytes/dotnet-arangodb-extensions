using System.Threading.Tasks;

namespace Core.Arango.Migration
{
    /// <summary>
    ///  Arango Migration
    /// </summary>
    public interface IArangoMigration
    {
        /// <summary>
        ///  Unique sortable migration id (e.g. 2020_12_24_001)
        /// </summary>
        public long Id { get; }

        /// <summary>
        ///   Name of the migration
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///  Changes in this migration
        /// </summary>
        public ValueTask Up(IArangoMigrator migrator, ArangoHandle handle);

        /// <summary>
        ///  Reverse changes from this migration
        /// </summary>
        public ValueTask Down(IArangoMigrator migrator, ArangoHandle handle);
    }
}