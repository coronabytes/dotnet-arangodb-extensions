using System;

namespace Core.Arango.Migration
{
    public class ArangoMigrationOptions
    {
        public bool DropExcess { get; set; }
    }

    [Flags]
    public enum ArangoMigrationScope
    {
        Structure,
        Data
    }
}