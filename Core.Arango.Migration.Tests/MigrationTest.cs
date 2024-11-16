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
        private readonly ITestOutputHelper _output;

        public MigrationTest(ITestOutputHelper output)
        {
            _output = output;
        }

        protected readonly IArangoContext Arango =
            new ArangoContext($"Server=http://localhost:8529;Realm=CI-{Guid.NewGuid():D};User=root;Password=;");

        protected readonly IArangoContext Arango2 =
            new ArangoContext($"Server=http://localhost:8529;Realm=stp;User=root;Password=;");

        public async ValueTask InitializeAsync()
        {
            await Arango.Database.CreateAsync("test");
        }

        [Fact]
        public async ValueTask Migrate2X()
        {
            var migrator = new ArangoMigrator(Arango);
            await migrator.ApplyStructureAsync("test", new ArangoStructure
            {
                Collections = new List<ArangoCollectionIndices>
                {
                    new ()
                    {
                        Collection = new ArangoCollection
                        {
                            Name = "MarketData",
                            KeyOptions = new ArangoKeyOptions
                            {
                                Type = ArangoKeyType.Padded
                            }
                        },
                        Indices = new List<ArangoIndex>
                        {
                            new ()
                            {
                                Name = "IDX_Symbol",
                                Type = ArangoIndexType.Hash,
                                Fields = new List<string> {"S"}
                            },
                            new ()
                            {
                                Name = "IDX_Time",
                                Type = ArangoIndexType.Skiplist,
                                Fields = new List<string> {"T"}
                            }
                        }
                    }
                }
            });

            await migrator.ApplyStructureAsync("test", new ArangoStructure
            {
                Collections = new List<ArangoCollectionIndices>
                {
                    new ()
                    {
                        Collection = new ArangoCollection
                        {
                            Name = "MarketData",
                            KeyOptions = new ArangoKeyOptions
                            {
                                Type = ArangoKeyType.Padded
                            }
                        },
                        Indices = new List<ArangoIndex>
                        {
                            new ()
                            {
                                Name = "IDX_Symbol",
                                Type = ArangoIndexType.Hash,
                                Fields = new List<string> {"S"}
                            },
                            new ()
                            {
                                Name = "IDX_Time",
                                Type = ArangoIndexType.Skiplist,
                                Fields = new List<string> {"T"}
                            }
                        }
                    }
                }
            });
        }

        [Fact]
        public async ValueTask Compare()
        {
            const string source = "96b02ae6-bda4-43e2-b83e-28293913ddb5";
            const string target = "target";

            if (!await Arango2.Database.ExistAsync(source))
                return;

            var migrationService = new ArangoMigrator(Arango2);
            var structure = await migrationService.GetStructureAsync(source);

            await migrationService.ApplyStructureAsync(target, structure, new ArangoMigrationOptions
            {
                DryRun = true,
                Notify = n =>
                {
                    if (n.State != ArangoMigrationState.Identical)
                        _output.WriteLine($"{n.State} {n.Object} {n.Name}");
                }
            });
        }

        [Fact]
        public async ValueTask ImportExport()
        {
            const string source = "96b02ae6-bda4-43e2-b83e-28293913ddb5";
            const string target = "target";

            if (!await Arango2.Database.ExistAsync(source))
                return;

            if (await Arango2.Database.ExistAsync(target))
                await Arango2.Database.DropAsync(target);

            var migrationService = new ArangoMigrator(Arango2);
            
            {
                await using var fs = File.Create("export.zip", 1024 * 1024);
                await migrationService.ExportAsync(source, fs, ArangoMigrationScope.Data | ArangoMigrationScope.Structure);
            }

            {
                await using var fs = File.OpenRead("export.zip");
                await migrationService.ImportAsync(target, fs, ArangoMigrationScope.Data | ArangoMigrationScope.Structure);
            }
        }

        [Fact]
        public async ValueTask ManualUp()
        {
            var migrationService = new ArangoMigrator(Arango);

            await migrationService.ApplyStructureAsync("test", new ArangoStructure
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

            await migrationService.ApplyStructureAsync("test", new ArangoStructure
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

        [Fact]
        public async ValueTask AutoMigration()
        {
            var migrator = new ArangoMigrator(Arango);
            migrator.AddMigrations(typeof(MigrationTest).Assembly);
            await migrator.UpgradeAsync("test");

            var structure = await migrator.GetStructureAsync("test");

            _output.WriteLine(structure.Serialize());
        }

        public async ValueTask DisposeAsync()
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
