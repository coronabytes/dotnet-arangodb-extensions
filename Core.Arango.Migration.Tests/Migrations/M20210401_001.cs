using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Core.Arango.Protocol;

namespace Core.Arango.Migration.Tests.Migrations
{
    internal class M20210401_001 : IArangoMigration
    {
        public long Id => 20210401_001;
        public string Name => "Initial";
        public async ValueTask Up(IArangoMigrator migrator, ArangoHandle handle)
        {
            await migrator.ApplyStructureAsync(handle, new ArangoStructure
            {
                Collections = new List<ArangoCollectionIndices>
                {
                    new ArangoCollectionIndices
                    {
                        Collection = new ArangoCollection
                        {
                            Name = "Vertices",
                            Type = ArangoCollectionType.Document
                        }
                    },
                    new ArangoCollectionIndices
                    {
                        Collection = new ArangoCollection
                        {
                            Name = "Edges",
                            Type = ArangoCollectionType.Edge
                        }
                    }
                }
            });
        }

        public ValueTask Down(IArangoMigrator migrator, ArangoHandle handle)
        {
            throw new NotImplementedException();
        }
    }
}
