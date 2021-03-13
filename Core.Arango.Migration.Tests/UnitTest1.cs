using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Arango.Protocol;
using Xunit;

namespace Core.Arango.Migration.Tests
{
    public class UnitTest1 : IAsyncLifetime
    {
        protected readonly IArangoContext Arango =
            new ArangoContext($"Server=http://localhost:8529;Realm=CI-{Guid.NewGuid():D};User=root;Password=;");

        private class M1 : IArangoMigration
        {
            public long Id => 20210313_001;
            public string Name => "Init";
            public async Task Up(IArangoContext context, ArangoHandle handle, ArangoMigrationFlags flags)
            {
                if (flags.HasFlag(ArangoMigrationFlags.Collections))
                    await context.Collection.CreateAsync(handle, "Clients", ArangoCollectionType.Document);
            }

            public Task Down(IArangoContext context, ArangoHandle handle)
            {
                return Task.CompletedTask;
            }
        }

        private class M2 : IArangoMigration
        {
            public long Id => 20210313_002;
            public string Name => "Projects";
            public async Task Up(IArangoContext context, ArangoHandle handle, ArangoMigrationFlags flags)
            {
                if (flags.HasFlag(ArangoMigrationFlags.Collections))
                    await context.Collection.CreateAsync(handle, "Projects", ArangoCollectionType.Document);

                if (flags.HasFlag(ArangoMigrationFlags.Indices))
                    await context.Index.CreateAsync(handle, "Projects", new ArangoIndex
                    {
                        Id = "IDX_ClientKey",
                        Type = ArangoIndexType.Hash,
                        Fields = new List<string> {"ClientKey"}
                    });
            }

            public Task Down(IArangoContext context, ArangoHandle handle)
            {
                return Task.CompletedTask;
            }
        }

        [Fact]
        public async Task Up()
        {
            var ms = new ArangoMigrationService(Arango);
            ms.AddMigration(new M1());
            ms.AddMigration(new M2());

            await ms.UpgradeAsync("test");
        }

        public async Task InitializeAsync()
        {
            await Arango.Database.CreateAsync("test");
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
