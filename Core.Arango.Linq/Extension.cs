using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Core.Arango.Linq.Internal;
using Core.Arango.Modules;

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

        public static (string aql, IDictionary<string, object> bindVars) ToAql<TSource>([NotNull] this IQueryable<TSource> source)
        {
            if (source is IArangoQueryableContext aqc)
                return aqc.Compile();

            return default;
        }

        public static async Task<List<T>> FindAsync<T>(this IArangoQueryModule m, ArangoHandle h, Expression<Func<T, bool>> predicate)
        {
            // TODO: Parse predicate?
            var c = AqlExpressionConverter.ParseQuery(predicate, typeof(T).Name);
            var (aql, bindVars, _) = c.Compile();
            return await m.ExecuteAsync<T>(h, aql, bindVars);
        }

        public static IQueryable<TSource> Update<TSource, TResult>([NotNull] this IQueryable<TSource> source, Expression<Func<TSource, TResult>> update, string collection)
        {
            return source;
        }

        public static IQueryable<TSource> Replace<TSource, TResult>([NotNull] this IQueryable<TSource> source, Expression<Func<TSource, TResult>> update, string collection)
        {
            return source;
        }

        public static IQueryable<TSource> Insert<TSource, TResult>([NotNull] this IQueryable<TSource> source, Expression<Func<TSource, TResult>> update, string collection)
        {
            return source;
        }

        public static IQueryable<TSource> Remove<TSource, TResult>([NotNull] this IQueryable<TSource> source, Expression<Func<TSource, TResult>> update, string collection)
        {
            return source;
        }
    }
}