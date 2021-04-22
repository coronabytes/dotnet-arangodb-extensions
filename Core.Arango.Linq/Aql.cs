using System;

namespace Core.Arango.Linq
{

    [System.AttributeUsage(System.AttributeTargets.Method)  
    ]
    public class AqlFunctionAttribute : Attribute
    {
        public string Name { get; }

        public AqlFunctionAttribute(string name)
        {
            Name = name;
        }
    }
    
    public static class Aql
    {

        [AqlFunction("DATE_ADD")]
        public static DateTime DateEdd(DateTime date, double amount, string unit)
        {
            throw new NotImplementedException();
        }

        public static T LOOKUP<T>(Guid xClientKey, string clients)
        {
            throw new NotImplementedException();
        }
    }
}