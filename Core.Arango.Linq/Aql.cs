using System;
using System.Diagnostics.CodeAnalysis;

namespace Core.Arango.Linq
{
    [SuppressMessage("CodeQuality", "IDE0060")]
    public static partial class Aql
    {
        private static Exception e => new NotImplementedException();

        [AqlFunction("DOCUMENT")]
        public static T Document<T>(string collection, object key)
        {
            throw e;
        }

        [AqlFunction("DOCUMENT")]
        public static T Document<T>(string id)
        {
            throw e;
        }

        [AqlFunction("DOCUMENT")]
        public static T[] Document<T>(string collection, object[] keys)
        {
            throw e;
        }

        [AqlFunction("DOCUMENT")]
        public static T[] Document<T>(string[] ids)
        {
            throw e;
        }

        [AqlFunction("HASH")]
        public static long Hash(object value)
        {
            throw e;
        }

        [AqlFunction("FNV64")]
        public static string Fnv64(object value)
        {
            throw e;
        }

        [AqlFunction("FLOOR")]
        public static double Floor(double value)
        {
            throw e;
        }

        [AqlFunction("CEIL")]
        public static double Ceil(double value)
        {
            throw e;
        }

        [AqlFunction("ROUND")]
        public static double Round(double value)
        {
            throw e;
        }

        [AqlFunction("ABS")]
        public static double Abs(double value)
        {
            throw e;
        }

        [AqlFunction("SQRT")]
        public static double Sqrt(double value)
        {
            throw e;
        }

        [AqlFunction("RAND")]
        public static double Rand()
        {
            throw e;
        }
    }
}