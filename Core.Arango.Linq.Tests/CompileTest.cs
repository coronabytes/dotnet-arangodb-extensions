using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Core.Arango.Linq.Tests
{
    public class Vertex
    {
        public string Key { get; set; }
        public string Label { get; set; }
    }

    public class Edge
    {
        public string Key { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Label { get; set; }
    }

    public class CompileTest
    {
        protected readonly IArangoContext Arango =
            new ArangoContext($"Server=http://localhost:8529;Realm=CI-{Guid.NewGuid():D};User=root;Password=;");

        [Fact]
        public void AqlFunction()
        {
            var (aql, bindVars) = Arango.AsQueryable<Project>("test")
              .Select(y => Aql.Trim(y.Name))
                .ToAql();

            Assert.Equal("FOR x IN Project\r\nRETURN TRIM(x.Name)", aql.Trim());
        }


        [Fact]
        public void AqlDocFunction()
        {
            var (aql, bindVars) = Arango.AsQueryable<Project>("test")
                .Select(y => new
                {
                    y.Key,
                    Doc = Aql.Document<Client>("Client", y.ClientKey)
                })
                .ToAql();
        }

        [Fact]
        public void Mutation()
        {
            var (aql, bindVars) = Arango.AsQueryable<Project>("test")
                .Update(x => new
                {
                    x.Key,
                    Name = x.Name + "2"
                }, "Other")
                .ToAql();

            //Assert.Equal("FOR x IN Project\r\nUPDATE { _key: x._key, Name: CONCAT(x.Name, @c} IN Project\r\nREMOVE x._key IN Queue", aql.Trim());
        }

        [Fact]
        public void Distinct()
        {
            var (aql, bindVars) = Arango.AsQueryable<Project>("test")
                .Distinct()
                .ToAql();

            Assert.Equal("FOR x IN Project\r\nRETURN DISTINCT x", aql.Trim());
        }

        [Fact]
        public void MultiFilter()
        {
            var (aql, bindVars) = Arango.AsQueryable<Project>("test")
                .Where(z => z.Name == "A")
                .OrderBy(x => x.Name)
                .Where(z => z.Name == "B")
                .Select(y => y.Name)
                .ToAql();

            Assert.Equal("FOR x IN Project\r\nFILTER x.Name == @c\r\nSORT x.Name\r\nFILTER x.Name == @c0\r\nRETURN x.Name\r\n\r\n", aql);
        }

        [Fact]
        public void WhereSortSelectProp()
        {
            var (aql, bindVars) = Arango.AsQueryable<Project>("test")
                .Where(z => z.Name == "A")
                .OrderBy(x => x.Name)
                .Select(y => y.Name)
                .ToAql();

            // TODO: proper parameter naming / remove line breaks
            Assert.Equal("FOR x IN Project\r\nFILTER x.Name == @c\r\nSORT x.Name\r\nRETURN x.Name\r\n\r\n", aql);
            Assert.Equal("A", bindVars["c"]);
        }

        [Fact]
        public void WhereSortSelectPropX()
        {
            var (aql, bindVars) = (from x in Arango.AsQueryable<Project>("test")
                where x.Name == "A"
                orderby x.Name
                select x.Name).ToAql();

            Assert.Equal("FOR x IN Project\r\nFILTER x.Name == @c\r\nSORT x.Name\r\nRETURN x.Name\r\n\r\n", aql);
            Assert.Equal("A", bindVars["c"]);
        }

        [Fact]
        public void ExpQueryLet()
        {
            var (aql, bindVars) = (from x in Arango.AsQueryable<Project>("test")
                where x.Name == "a"
                // TODO: Mark as pull over for / or detect that it's free of x
                let clients = from y in Arango.AsScopeVariable<Client>() select y
                let client = clients.SingleOrDefault(z => z.Key == x.ClientKey)
                where client.Name == "b"
                orderby x.Name, client.Name descending 
                select new { x.Name, ClientName = client.Name }).ToAql();

            aql.ToString();
        }


        [Fact]
        public void ExpQueryGroupBy()
        {
            var (aql, bindVars) = (from x in Arango.AsQueryable<Project>("test")
               group x by x.ClientKey into g
               // x is gone now
               let clients = from y in Arango.AsScopeVariable<Client>() select y
               let client = clients.SingleOrDefault(z => z.Key == g.Key)
               select new
               {
                   ClientName = client.Name,
                   // Aggregate?
                   Max = g.Max(z=> z.Value)
               }).ToAql();
            aql.ToString();
        }

        [Fact]
        public void WhereSort2SelectProp()
        {
            var (aql, bindVars) = Arango.AsQueryable<Project>("test")
                .Where(z => z.Name == "A")
                .OrderBy(x => x.Name).ThenByDescending(x => x.StartDate)
                .Select(y => y.Name)
                .ToAql();


            Assert.Equal(
                "FOR x IN Project\r\nFILTER x.Name == @c\r\nSORT x.Name ASC, x.StartDate DESC\r\nRETURN x.Name\r\n\r\n",
                aql);
            Assert.Equal("A", bindVars["c"]);
        }

        [Fact]
        public void WhereSkipTakeSelectProp()
        {
            var (aql, bindVars) = Arango.AsQueryable<Project>("test")
                .Where(z => z.Name == "A")
                .Skip(1)
                .Take(2)
                .Select(y => y.Name)
                .ToAql();

            Assert.Equal("FOR x IN Project\r\nFILTER x.Name == @c\r\nLIMIT 1,2\r\nRETURN x.Name\r\n\r\n", aql);
            Assert.Equal("A", bindVars["c"]);
        }

        [Fact]
        public void GroupBy()
        {
            var (aql, _) = Arango.AsQueryable<Project>("test")
                .GroupBy(x => new
                {
                    x.ClientKey
                }).Select(x => new
                {
                    ClientKey = x.Key,
                    Start = x.Max(y => y.StartDate)
                }).ToAql();

            /*
// simple inefficient syntax / better matches linq?
COLLECT ageGroup = FLOOR(u.age / 5) * 5 INTO g
  RETURN { 
    "ageGroup" : ageGroup,
    "minAge" : MIN(g[*].u.age),
    "maxAge" : MAX(g[*].u.age)
  }            

// complex efficient syntax

  COLLECT ageGroup = FLOOR(u.age / 5) * 5 
  AGGREGATE minAge = MIN(u.age), maxAge = MAX(u.age)
  RETURN {
    ageGroup, 
    minAge, 
    maxAge 
  }
             */

            Assert.Equal(
                "FOR x IN Project\r\nCOLLECT c = x.ClientKey AGGREGATE s = MAX(x.StartDate)\r\nRETURN { ClientKey: c, Start: s }\r\n\r\n",
                aql);
        }

        [Fact]
        public void Madness()
        {
            var (aql, _) = Arango.AsQueryable<Project>("test")
                .GroupBy(x => new
                {
                    x.ClientKey, ClientKey2 = x.ClientKey
                })
                .Select(x => new
                {
                    ClientKey = x.Key,
                    Max = x.Max(y => y.Value),
                    Min = x.Min(y => y.Value),
                    Avg = x.Average(y => y.Value),
                    Avg2 = x.Average(k => k.Value),
                    Sum = x.Sum(y => y.Value),
                    Count = x.Count()
                }).Where(x => x.Max < 2 && x.Count > 3)
                .Distinct().ToAql();
        }
    }
}