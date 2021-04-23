using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection.PortableExecutable;
using System.Threading.Tasks;
using Core.Arango.Protocol;
using Xunit;

namespace Core.Arango.Linq.Tests
{

    public class ArLinCompound<TBase, TDec> : IQueryable<TBase>
    {
        public TDec Declaration { get; set; }
        
        
        public IEnumerator<TBase> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public Type ElementType { get; }
        public Expression Expression { get; }
        public IQueryProvider Provider { get; }
    }
    
    public static class ArLinkExt
    {
        
        
        public static IQueryable<TKey> DeclareLet<TSource, TKey>(this IQueryable<TSource> source, Expression<Func<TSource, TKey>> keySelector) {
            throw new NotImplementedException();
        }
    }
    
    public class EntityWithVariable<T>
    {
        public T Entity { get; set; }
        
        
    }

    public class Client
    {
        public Guid Key { get; set; }
        public string Name { get; set; }
    }
    
    
    public class Project
    {
        public Guid Key { get; set; }
        public string Name { get; set; }
        public int Value { get; set; }
        
        
        public Guid ClientKey { get; set; }
        public Client Client { get; set; }

        public DateTime StartDate { get; set; }
        
        public List<string> StringList { get; set; }
    }

    public class UnitTest1 : IAsyncLifetime
    {
        
        protected readonly IArangoContext Arango =
            new ArangoContext($"Server=http://localhost:8529;Realm=CI-{Guid.NewGuid():D};User=root;Password=;");

        [Fact]
        public void TestToList()
        {
            var test = Arango.AsQueryable<Project>("test").ToList();
            Assert.True(test.Count > 0);
        }

        /// <summary>
        /// Überprüft die Funktionalität eines SingleOrDefault-Querys mit Einschränkung des Projektnamens
        /// expected query: FOR x IN Project FILTER x.Name == "A" return x
        /// </summary>
        [Fact]
        public void TestSingleOrDefault()
        {
            var test = Arango.AsQueryable<Project>("test").SingleOrDefault(x => x.Name == "A");
            Assert.True(test.Name == "A");
        }

        /// <summary>
        /// expected query: FOR x IN Project FILTER x.Name == "A" return x.Name
        /// </summary>
        [Fact]
        public void TestWhereSelect()
        {
            var test = Arango.AsQueryable<Project>("test").Where(z => z.Name == "A").Select(y => y.Name).ToList();
            foreach (var t in test)
            {
                Assert.True(t == "A");
            }
        }

        [Fact]
        public void TestWhereDateAdd()
        {
            var test = Arango.AsQueryable<Project>("test")
                // .Where(x =>  Aql.DATE_ADD(x.StartDate, 1, "day") >= DateTime.UtcNow)
                .Where(x =>  x.StartDate.AddDays(1) >= DateTime.UtcNow)
                .ToList();

            test.ToArray();
        }

        /// <summary>
        /// expected query: FOR x IN Project FILTER x.Value IN @list RETURN x
        /// </summary>
        [Fact]
        public void TestListContains()
        {
            var list = new List<int> { 1, 2, 3 };
            var test = Arango.AsQueryable<Project>("test").Where(x => list.Contains(x.Value)).ToList();
            foreach (var t in test)
            {
                Assert.Contains(t.Value, list);
            }
        }
        
        /// <summary>
        /// expected query: FOR x IN Project FILTER POSITION(x.Tags, @searchString) > 0 RETURN x
        /// </summary>
        [Fact]
        public async Task TestListContainsElement()
        {
            var tag = "hello";
            var cs = Arango.Configuration.ConnectionString;
            var test = Arango
                .AsQueryable<Project>("test")
                .Where(x => x.StringList
                    .Select(k => k + "hmm")
                    .Where(t => t.Length > 4)
                    .Any(t => t == tag + "hmm"))
                .ToList();
            foreach (var t in test)
            {
                Assert.Contains(tag, t.StringList);
            }
        }

        /// <summary>
        /// expected query: FOR x IN Project FILTER x.Value == 1 || x.Value == 2 RETURN x
        /// </summary>
        [Fact]
        public void TestOr()
        {
            var test = Arango.AsQueryable<Project>("test")
                .Where(x => x.Value == 1 || x.Value == 2)
                .ToList();
            foreach (var t in test)
            {
                Assert.True(t.Value == 1 || t.Value == 2);
            }
        }

        /// <summary>
        /// expected query: FOR x IN Project FILTER x.Name LIKE "A%" RETURN x
        /// </summary>
        [Fact]
        public void TestStringBeginsWith()
        {
            var test = Arango.AsQueryable<Project>("test").Where(x => x.Name.StartsWith("A")).ToList();
            foreach (var t in test)
            {
                Assert.StartsWith("A", t.Name);
                Assert.True(test.Count > 0);
            }
        }
        
        /// <summary>
        /// 
        /// </summary>
        [Fact]
        public void TestAqlFunc()
        {
            /*var test = Arango.AsQueryable<Project>("test")
                .Select(x => Aql.DATE_ADD(x.StartDate, 10, "years"))
                .ToList();
            foreach (var t in test)
            {
                Assert.True(test.Count > 0);
            }*/
        }

        class ProjectProj
        {
            public string Name { get; set; }
        }
        
        /// <summary>
        /// expected query: FOR x IN Project FILTER x.Name == @p || x.Name == @pUnique RETURN {Name: x.Name}
        /// </summary>
        [Fact]
        public void TestObjectProjection()
        {
            var test = Arango.AsQueryable<Project>("test")
                .Where(z => z.Name == "A" || z.Name == "B \" RETURN 42")
                .Select(y => new
                {
                    Name = y.Name,
                    Fussbad = y.Value
                })
                .ToList();
            Assert.True(test.All(x => x.Fussbad > -1));
            Assert.True(test.Count == 1);
        }
        
        [Fact]
        public void TestMultipleWheres()
        {
            var test = Arango.AsQueryable<Project>("test")
                .Where(x => x.Value == 3 || x.Value == 1)
                .Where(x => x.Name == "B" || x.Name == "C")
                .ToList();
            Assert.Single(test);
            Assert.True(test.Single().Name == "C");
        }
        
        /// <summary>
        /// expected query: FOR x IN Project FILTER x.Name == @p || x.Name == @pUnique RETURN x.Name
        /// </summary>
        [Fact]
        public void TestInjection()
        {
            // var test = Arango.AsQueryable<Project>("test").Where(x => x.Name == "A" || x.Name == "B \" RETURN 42").Select(x => new ProjectProj {Name = x.Name}).ToList();
            var test = Arango.AsQueryable<Project>("test").Where(x => x.Name == "A" || x.Name == "B \" RETURN 42").Select(x => x.Name).ToList();
            Assert.True(test.Count == 1);
        }

        /// <summary>
        /// Überprüft die Funktionalität eines SingleOrDefault-Querys mit Einschränkung der Guid
        /// expected query: FOR x IN Project FILTER x.Key == @testGuid return x
        /// </summary>
        [Fact]
        public async void TestSingleOrDefaultGuid()
        {
            var testGuid = Guid.NewGuid();

            await Arango.Document.CreateAsync("test", nameof(Project), new Project
            {
                Key = testGuid,
                Name = "TestSingleOrDefault",
                Value = 2
            });

            var test = Arango.AsQueryable<Project>("test").SingleOrDefault(x => x.Key == testGuid);

            Assert.True(test.Key == testGuid);
        }

        /// <summary>
        /// Checks whether the OrderBy is correctly applied.
        /// Expected query: FOR x IN Project SORT x.Value RETURN x
        /// </summary>
        [Fact]
        public void TestOrderBy()
        {
            var test = Arango.AsQueryable<Project>("test").OrderBy(x => x.Value).ToList();

            Assert.True(test[0].Value == 1);
        }

        /// <summary>
        /// Checks if Take is correctly applied
        /// Expected query: FOR x IN Project LIMIT 2 RETURN x
        /// </summary>
        [Fact]
        public void TestTake()
        {
            var test = Arango.AsQueryable<Project>("test").Take(2).ToList();

            Assert.True(test.Count == 2);
        }


        public static class AQL
        {
            public static double DateDiff(DateTime a, DateTime b, string format)
            {
                return 0;
            }
        }
        //todo: AQL.DateDiff(x.StartDate, x.StartDate, "h") > 0).ToList();

        [Fact]
        public void TestDateTimeNow()
        {
            var test = Arango.AsQueryable<Project>("test").Where(x =>  x.StartDate <= DateTime.UtcNow).Select(x => x.StartDate).ToList();

            Assert.True(test.Count == 2);
            foreach (var t in test)
            {
                Assert.True(t <= DateTime.UtcNow);
            }
        }
        
        [Fact]
        public void TestScopeVariable()
        {
            var clientKeys = Arango
                .AsScopeVariable<Client>()
                .Where(x => x.Name.Length > 0)
                .Select(x => x.Key);
            
            
            var test3 = Arango
                .AsQueryable<Project>("test")
                .Where(x => clientKeys.Any(k => k == x.ClientKey))
                .Select(x => new {name = "kaspar"})
                .ToList();

            Assert.True(test3.Any());

        }
        
        

        // todo
        [Fact]
        public async Task TestToListAsync()
        {
            var test = await Arango.AsQueryable<Project>("test").ToListAsync();
            Assert.True(test.Count == 3);
        }

        // todo
        //[Fact]
        //public async void TestSingleOrDefaultAsync()
        //{
        //    var test = await Arango.AsQueryable<Project>("test").Single
        //}

        [Fact]
        public async Task FindAsyncPredicate()
        {
            var test = await Arango.Query.FindAsync<Project>("test", x => x.Name == "A");
        }

        /// <summary>
        /// Initialisiert eine Datenbank und eine Collection für die Tests
        /// </summary>
        /// <returns></returns>
        public async Task InitializeAsync()
        {


            await Arango.Database.CreateAsync("test");
            await Arango.Collection.CreateAsync("test", nameof(Project), ArangoCollectionType.Document);
            await Arango.Collection.CreateAsync("test", nameof(Client), ArangoCollectionType.Document);

            var cg1 = Guid.NewGuid();
            
            await Arango.Document.CreateAsync("test", nameof(Client), new Client
            {
                Key = cg1,
                Name = "Client Peter"
            });
            
            await Arango.Document.CreateAsync("test", nameof(Project), new Project
            {
                Key = Guid.NewGuid(),
                Name = "A",
                Value = 1,
                StartDate = new DateTime(2020, 04, 03).ToUniversalTime(),
                StringList = new List<string>() {"hello", "you"},
                ClientKey = cg1
            });
            await Arango.Document.CreateAsync("test", nameof(Project), new Project
            {
                Key = Guid.NewGuid(),
                Name = "B",
                Value = 2,
                StartDate = DateTime.Now.AddDays(-1).ToUniversalTime(),
                StringList = new List<string>() {"hello2", "you", "hello"},
                ClientKey = cg1
            });
            await Arango.Document.CreateAsync("test", nameof(Project), new Project
            {
                Key = Guid.NewGuid(),
                Name = "C",
                Value = 3,
                StartDate = new DateTime(3021, 1, 5).ToUniversalTime(),
                StringList = new List<string>() {"me", "you", "everybody"}
            });
        }

        /// <summary>
        /// Löscht die angelegten Datenbanken
        /// </summary>
        /// <returns></returns>
        public async Task DisposeAsync()
        {
            try
            {
                foreach (var db in await Arango.Database.ListAsync())
                    await Arango.Database.DropAsync(db);
            }
            catch
            {
                //
            }
        }
    }
}
