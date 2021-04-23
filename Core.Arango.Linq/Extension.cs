using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Arango.Linq
{
    public static class ArangoLinqExtension
    {

        public static IQueryable<T> AsScopeVariable<T>(this IArangoContext arango, string collection = null)
        {
            // TODO: replace with saver dummy
            
            // The base expression needs to be a constant of IArangoQueryableCollection. Its Collection property will be read by the parser.
            // It needs to support expression chaining but not enumeration. 
            return new ArangoQueryableContext<T>(null, null, collection ?? typeof(T).Name);
        }
        
        public static IQueryable<T> AsQueryable<T>(this IArangoContext arango, ArangoHandle db, string collection = null)
        {
            return new ArangoQueryableContext<T>(arango, db, collection ?? typeof(T).Name);
        }

        public static async Task<List<TSource>> ToListAsync<TSource>(
            [NotNull] this IQueryable<TSource> source,
            CancellationToken cancellationToken = default)
        {
            var list = new List<TSource>();
            await foreach (var element in source.AsAsyncEnumerable().WithCancellation(cancellationToken))
                list.Add(element);

            return list;
        }

        public static IAsyncEnumerable<TSource> AsAsyncEnumerable<TSource>(
            [NotNull] this IQueryable<TSource> source)
        {
            if (source is IAsyncEnumerable<TSource> asyncEnumerable) return asyncEnumerable;

            throw new InvalidOperationException();
        }
    }
}