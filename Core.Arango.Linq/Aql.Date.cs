using System;
using System.Collections.Generic;

namespace Core.Arango.Linq
{
    public static partial class Aql
    {
        [AqlFunction("DATE_NOW")]
        public static long DateNow()
        {
            throw e;
        }

        [AqlFunction("DATE_ISO8601")]
        public static DateTime DateIso8601(long timestamp)
        {
            throw e;
        }

        [AqlFunction("DATE_TIMESTAMP")]
        public static long DateTimestamp(DateTime timestamp)
        {
            throw e;
        }
        
        [AqlFunction("DATE_ADD")]
        public static DateTime DateAdd(DateTime date, double amount, string unit)
        {
            throw e;
        }
    }
}