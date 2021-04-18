using System;

namespace Core.Arango.Migration
{
    public class ArangoMigrationOptions
    {
        public bool DryRun { get; set; }
        public bool DropExcess { get; set; }
        public Action<ArangoMigrationNotification> Notify { get; set; }
    }
}