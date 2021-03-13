using System;

namespace Core.Arango.Migration
{
    [Flags]
    public enum ArangoMigrationFlags
    {
        None = 0,
        Collections = 1,
        Graphs = 2,
        Views = 4,
        Indices = 8,
        All = 0xFF
    }
}