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

        public async Task InitializeAsync()
        {
            await Arango.Database.CreateAsync("test");
        }

        [Fact]
        public async Task Compare()
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
        public async Task ImportExport()
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
        public async Task ManualUp()
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
        public async Task AutoMigration()
        {
            var migrator = new ArangoMigrator(Arango);
            migrator.AddMigrations(typeof(MigrationTest).Assembly);
            await migrator.UpgradeAsync("test");

            var structure = await migrator.GetStructureAsync("test");

            _output.WriteLine(structure.Serialize());
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
