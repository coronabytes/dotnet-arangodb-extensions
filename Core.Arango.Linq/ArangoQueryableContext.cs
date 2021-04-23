using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using Core.Arango.Linq.Internal;

namespace Core.Arango.Linq
{

    public interface IArangoQueryableCollection
    {
        string Collection { get; }   
    }

    public interface IArangoQueryableContext
    {
        string Collection { get; }   
        Expression Expression { get; }
        IQueryProvider Provider { get; }
    }
    
    public class ArangoQueryableContext<T> : IOrderedQueryable<T>, IAsyncEnumerable<T>, IArangoQueryableContext, IArangoQueryableCollection
    {
        public string Collection { get; }

        public ArangoQueryableContext(IArangoContext arango, ArangoHandle handle, string collection)
        {
            Collection = collection;
            Provider = new ArangoProvider(arango, handle, collection);
            Expression = Expression.Constant(this);
        }

        internal ArangoQueryableContext(IQueryProvider provider, Expression expression)
        {
            Provider = provider;
            Expression = expression;
        }

        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = new CancellationToken())
        {
            if (Provider is ArangoProvider p)
                return p.ExecuteAsync<T>(Expression, cancellationToken);

            throw new InvalidOperationException();
        }


        public IEnumerator<T> GetEnumerator()
        {
            return Provider.Execute<IEnumerable<T>>(Expression).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Provider.Execute<IEnumerable>(Expression).GetEnumerator();
        }

        public Type ElementType => typeof(T);

        public Expression Expression { get; }
        public IQueryProvider Provider { get; }
        public (string aql, Dictionary<string, object> bindVars) Compile()
        {
            if (Provider is IArangoQueryProvider p)
            {
                var c = AqlExpressionConverter.ParseQuery(Expression, p.Collection);
                var (aql, bindVars, _) = c.Compile();

                return (aql, bindVars);
            }

            return default;
        }
    }
}