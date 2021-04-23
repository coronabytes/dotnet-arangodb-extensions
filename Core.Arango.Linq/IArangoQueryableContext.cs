using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Core.Arango.Linq
{
    public interface IArangoQueryableContext
    {
        string Collection { get; }
        Expression Expression { get; }
        IQueryProvider Provider { get; }
        public (string aql, Dictionary<string, object> bindVars) Compile();
    }
}