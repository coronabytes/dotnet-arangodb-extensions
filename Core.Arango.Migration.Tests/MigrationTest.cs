using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Core.Arango.Protocol;
using Xunit;
using Xunit.Abstractions;

namespace Core.Arango.Migration.Tests
{
    public class MigrationTest : IAsyncLifetime
    {
        protected readonly IArangoContext Arango =
            new ArangoContext($"Server=http://localhost:8529;Realm=CI-{Guid.NewGuid():D};User=root;Password=;");

        public async Task InitializeAsync()
        {
            await Arango.Database.CreateAsync("test");
        }

        /*[Fact]
        public async Task ImportExport()
        {
            var migrationService = new ArangoMigrationService(Arango);

            var structure = await migrationService.GetCurrentStructureAsync("source");

            await Arango.Database.CreateAsync("target");

            await migrationService.ApplyStructureUpdateAsync("target", structure);

            {
                await using var fs = File.Create("export.zip", 1024 * 1024);
                await migrationService.ExportAsync("source", fs);
            }

            {
                await using var fs = File.OpenRead("export.zip");
                await migrationService.ImportAsync("source", fs);
            }
        }*/

        [Fact]
        public async Task Up()
        {
            var migrationService = new ArangoMigrationService(Arango);

            await migrationService.ApplyStructureUpdateAsync("test", new ArangoStructureUpdate
            {
                Collections = new List<ArangoCollectionIndices>
                {
                    new ()
                    {
                        Collection = new ArangoCollection
                        {
                            Name = "Project",
                            Type = ArangoCollectionType.Document
                        },
                        Indices = new List<ArangoIndex>
                        {
                            new ()
                            {
                                Name = "IDX_ParentKey",
                                Fields = new List<string> {"ParentKey"},
                                Type = ArangoIndexType.Hash
                            }
                        }
                    },
                    new ()
                    {
                        Collection = new ArangoCollection
                        {
                            Name = "Activity",
                            Type = ArangoCollectionType.Document
                        },
                        Indices = new List<ArangoIndex>
                        {
                            new ()
                            {
                                Name = "IDX_ProjectKey",
                                Fields = new List<string> {"ProjectKey"},
                                Type = ArangoIndexType.Hash
                            }
                        }
                    }
                }
            });

            await migrationService.ApplyStructureUpdateAsync("test", new ArangoStructureUpdate
            {
                Views = new List<ArangoView>
                {
                    new ()
                    {
                        Name = "ProjectView",
                        Links = new Dictionary<string, ArangoLinkProperty>
                        {
                            ["Project"] = new ()
                            {
                                Analyzers = new List<string> {"identity", "text_en"},
                                Fields = new Dictionary<string, ArangoLinkProperty>
                                {
                                    ["Name"] = new ()
                                }
                            }
                        }
                    }
                }
            });
        }

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
