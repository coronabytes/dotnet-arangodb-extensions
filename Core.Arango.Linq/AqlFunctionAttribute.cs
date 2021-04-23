using System;

namespace Core.Arango.Linq
{
    [AttributeUsage(AttributeTargets.Method)]
    public class AqlFunctionAttribute : Attribute
    {
        public AqlFunctionAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}