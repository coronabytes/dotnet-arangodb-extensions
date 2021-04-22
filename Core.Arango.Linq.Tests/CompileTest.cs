using System;
using System.Linq;
using Xunit;

namespace Core.Arango.Linq.Tests
{
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
                }).Select(x => new
                {
                    ClientKey = x.Key,
                    Max = x.Max(y => y.Value),
                    Min = x.Min(y => y.Value),
                    Avg = x.Average(y => y.Value),
                    Sum = x.Sum(y => y.Value),
                    Count = x.Count()
                }).Where(x => x.Max < 2 && x.Count > 3)
                .Distinct().ToAql();
        }
    }
}