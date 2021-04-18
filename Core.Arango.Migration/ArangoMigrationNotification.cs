namespace Core.Arango.Migration
{
    public class ArangoMigrationNotification 
    {
        public ArangoMigrationObject Object { get; set; }
        public ArangoMigrationState State { get; set; }
        public string Name { get; set; }
    }
}