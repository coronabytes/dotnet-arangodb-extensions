using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Core.Arango.Protocol;

namespace Core.Arango.Migration
{
    public class ArangoMigrationService
    {
        private readonly IArangoContext _arango;
        private readonly List<IArangoMigration> _migrations = new List<IArangoMigration>();

        public ArangoMigrationService(IArangoContext arango)
        {
            _arango = arango;
        }

        public string HistoryCollection { get; set; } = "MigrationHistory";

        public void AddMigrations(Assembly assembly)
        {
            var type = typeof(IArangoMigration);
            var migrations = assembly.GetTypes()
                .Where(t => type.IsAssignableFrom(t) && !t.IsInterface)
                .Select(t => Activator.CreateInstance(t, true))
                .Cast<IArangoMigration>().ToList();
            _migrations.AddRange(migrations);
        }

        public void AddMigration(IArangoMigration migration)
        {
            _migrations.Add(migration);
        }

        /*public async Task MigrateAll()
        {
            var collections = await _arango.Database.ListAsync();

            foreach (var name in collections)
            {
                if (name == "master" || name == "logs")
                    continue;

                if (Guid.TryParse(name, out var tid)) await Migrate(tid);
            }
        }*/

        public async Task UpgradeAsync(ArangoHandle db)
        {
            var cols = await _arango.Collection.ListAsync(db);

            if (!cols.Contains(HistoryCollection))
                await _arango.Collection.CreateAsync(db, HistoryCollection, ArangoCollectionType.Document);

            var latest =
                (await _arango.Query.FindAsync<MigrationEntity>(db, HistoryCollection, $"true SORT x._key DESC",
                    limit: 1))
                .FirstOrDefault();

            long? version = null;

            if (latest != null)
                version = long.Parse(latest.Key);

            foreach (var x in _migrations)
                if (!version.HasValue || x.Id > version.Value)
                {
                    //_logger.LogInformation("[{tenant}] Apply migration {name}", db.ToString(), x.Name);
                    await x.Up(_arango, db, ArangoMigrationFlags.All);

                    await _arango.Document.CreateAsync(db, HistoryCollection, new MigrationEntity
                    {
                        Key = x.Id.ToString(),
                        Name = x.Name,
                        Created = DateTime.UtcNow
                    });
                }
        }

        /// <summary>
        ///     Drops and recreates views, indices and analyzers
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
        public async Task RebuildAsync(ArangoHandle db)
        {
            await _arango.Index.DropAllAsync(db);
            await _arango.View.DropAllAsync(db);
            var analyzers = await _arango.Analyzer.ListAsync(db);

            //if (analyzers.Any(x => x.Name == "text_tenant"))
            //    await _arango.Analyzer.DeleteAsync(db, "text_tenant", true);

            foreach (var x in _migrations)
                await x.Up(_arango, db, ArangoMigrationFlags.Views | ArangoMigrationFlags.Indices);
        }

        private class MigrationEntity
        {
            public string Key { get; set; }
            public string Name { get; set; }
            public DateTime Created { get; set; }
        }
    }
}