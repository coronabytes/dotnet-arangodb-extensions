using System;

namespace Core.Arango.Migration
{
    /// <summary>
    ///   Import / Export scope
    /// </summary>
    [Flags]
    public enum ArangoMigrationScope
    {
        /// <summary>
        ///  Include / Apply structure
        /// </summary>
        Structure,

        /// <summary>
        ///  Include / Apply data in collections
        /// </summary>
        Data
    }
}