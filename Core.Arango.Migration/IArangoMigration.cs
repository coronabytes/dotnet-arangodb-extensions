using System.Threading.Tasks;

namespace Core.Arango.Migration
{
    public interface IArangoMigration
    {
        public long Id { get; }
        public string Name { get; }
        public Task Up(IArangoContext context, ArangoHandle handle, ArangoMigrationFlags flags);
        public Task Down(IArangoContext context, ArangoHandle handle);
    }
}