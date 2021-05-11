using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Core.Arango.Linq.Internal;
using Core.Arango.Modules;

namespace Core.Arango.Linq
{
    public class ArangoLinqExtensionAttribute : Attribute
    {
        public ArangoLinqExtensionAttribute(string identifier)
        {
            Identifier = identifier;
        }

        public string Identifier { get; set; }
    }

    public static class ArangoLinqExtension
    {
        static ArangoLinqExtension()
        {
            var extension = typeof(ArangoLinqExtension)
                .GetRuntimeMethods()
                .Where(x => x.GetCustomAttribute<ArangoLinqExtensionAttribute>() != null)
                .GroupBy(x => x.GetCustomAttribute<ArangoLinqExtensionAttribute>().Identifier)
                .Select(g => new { g.Key, Count = g.Count() })
                .FirstOrDefault(x => x.Count > 1);

            if (extension != null)
                throw new InvalidOperationException($"Multiple extention identifier {extension.Key} found");
        }

        public static IQueryable<T> AsScopeVariable<T>(this IArangoContext arango, string collection)
        {
            // TODO: replace with saver dummy
            
            // The base expression needs to be a constant of IArangoQueryableCollection. Its Collection property will be read by the parser.
            // It needs to support expression chaining but not enumeration. 
            return new ArangoQueryableContext<T>(null, null, collection ?? typeof(T).Name);
        }

        public static IQueryable<T> AsScopeVariable<T>(this IArangoContext arango)
        {
            // TODO: replace with saver dummy

            // The base expression needs to be a constant of IArangoQueryableCollection. Its Collection property will be read by the parser.
            // It needs to support expression chaining but not enumeration. 
            return new ArangoQueryableContext<T>(null, null, typeof(T).Name);
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

        public static async Task<List<T>> FindAsync<T>(this IArangoContext c, ArangoHandle h, Expression<Func<T, bool>> predicate)
        {
            return await c.AsQueryable<T>(h).Where(predicate).ToListAsync();
        }
        
        #region First

        public static Task<TSource> FirstAsync<TSource>(this IQueryable<TSource> source)
        {
            return FirstOrDefaultAsync(source, false, null);
        }

        public static Task<TSource> FirstAsync<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate)
        {
            return FirstOrDefaultAsync(source, false, predicate);
        }

        public static Task<TSource> FirstOrDefaultAsync<TSource>(this IQueryable<TSource> source)
        {
            return FirstOrDefaultAsync(source, true, null);
        }

        public static Task<TSource> FirstOrDefaultAsync<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate)
        {
            return FirstOrDefaultAsync(source, true, predicate);
        }

        private static async Task<T> FirstOrDefaultAsync<T>(this IQueryable<T> source, bool returnDefaultWhenEmpty, Expression<Func<T, bool>> predicate)
        {
            if (predicate != null)
                source = source.Where(predicate);
            var list = await source.Take(1).ToListAsync().ConfigureAwait(false);
            
            if (returnDefaultWhenEmpty)
                return list.FirstOrDefault();

            return list.First();
        }

        #endregion

        #region Single

        public static Task<TSource> SingleAsync<TSource>(this IQueryable<TSource> source)
        {
            return SingleOrDefaultAsync(source, false, null);
        }

        public static Task<TSource> SingleAsync<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate)
        {
            return SingleOrDefaultAsync(source, false, predicate);
        }

        public static Task<TSource> SingleOrDefaultAsync<TSource>(this IQueryable<TSource> source)
        {
            return SingleOrDefaultAsync(source, true, null);
        }

        public static Task<TSource> SingleOrDefaultAsync<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate)
        {
            return SingleOrDefaultAsync(source, true, predicate);
        }

        private static async Task<T> SingleOrDefaultAsync<T>(this IQueryable<T> source, bool returnDefaultWhenEmpty, Expression<Func<T, bool>> predicate)
        {
            if (predicate != null)
                source = source.Where(predicate);

            var list = await source.Take(2).ToListAsync().ConfigureAwait(false);

            if (returnDefaultWhenEmpty)
                return list.SingleOrDefault();

            return list.Single();
        }

        #endregion

        private static readonly ConcurrentDictionary<string, MethodInfo> CachedExtentions = new ConcurrentDictionary<string, MethodInfo>();

        internal static MethodInfo FindExtention(string identifier, params Type[] arguments)
        {
            string key = $"{identifier}_{string.Join("_", arguments.Select(x => x.FullName))}";

            return CachedExtentions
                .GetOrAdd(key,
                    typeof(ArangoLinqExtension).GetRuntimeMethods()
                        .ToList()
                        .First(x => x.GetCustomAttribute<ArangoLinqExtensionAttribute>()?.Identifier == identifier)
                        .MakeGenericMethod(arguments));
        }

        #region Mutation
        
        [ArangoLinqExtension("Update")]
        public static IQueryable<TSource> Update<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, object>> withSelector, string collection)
        {
            return source.Provider.CreateQuery<TSource>(
                Expression.Call(
                    FindExtention("Update", typeof(TSource)),
                    source.Expression,
                    Expression.Quote(withSelector),
                    Expression.Constant(collection)
                ));
        }

        #endregion
    }
}