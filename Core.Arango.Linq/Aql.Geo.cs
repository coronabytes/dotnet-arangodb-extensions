using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Core.Arango.Linq
{
    [SuppressMessage("CodeQuality", "IDE0060")]
    public static partial class Aql
    {
        [AqlFunction("DISTANCE")]
        public static double Distance(double latitude1, double longitude1, double latitude2, double longitude2)
        {
            throw e;
        }

        [AqlFunction("GEO_CONTAINS")]
        public static bool GeoContains(object geoJsonA, object geoJsonB)
        {
            throw e;
        }

        [AqlFunction("GEO_DISTANCE")]
        public static double GeoDistance(object geoJsonA, object geoJsonB)
        {
            throw e;
        }

        [AqlFunction("GEO_DISTANCE")]
        public static double GeoDistance(object geoJsonA, object geoJsonB, string ellipsoid)
        {
            throw e;
        }
    }
}