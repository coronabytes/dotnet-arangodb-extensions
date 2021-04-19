using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Arango.Protocol;

namespace Core.Arango.Migration.Tests.Migrations
{
    internal class M20210401_002 : IArangoMigration
    {
        public long Id => 20210401_002;
        public string Name => "Graph";
        public async Task Up(IArangoMigrator migrator, ArangoHandle handle)
        {
            await migrator.ApplyStructureAsync(handle, new ArangoStructure
            {
                Graphs = new List<ArangoGraph>
                {
                    new ArangoGraph
                    {
                        Name = "Graph",
                        EdgeDefinitions = new List<ArangoEdgeDefinition>
                        {
                            new ArangoEdgeDefinition
                            {
                                Collection = "Edges",
                                From = new List<string>{ "Vertices" },
                                To = new List<string>{ "Vertices" }
                            }
                        }
                    }
                }
            });

            await migrator.Context.Graph.Vertex.CreateAsync(handle, "Graph", "Vertices", new
            {
                Key = "alice"
            });

            await migrator.Context.Graph.Vertex.CreateAsync(handle, "Graph", "Vertices", new
            {
                Key = "bob"
            });

            await migrator.Context.Graph.Edge.CreateAsync(handle, "Graph", "Edges", new
            {
                Key = "alicebob",
                From = "Vertices/alice",
                To = "Vertices/bob"
            });
        }

        public Task Down(IArangoMigrator migrator, ArangoHandle handle)
        {
            throw new NotImplementedException();
        }
    }
}