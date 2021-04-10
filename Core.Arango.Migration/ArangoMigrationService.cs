using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Core.Arango.Protocol;

namespace Core.Arango.Migration
{
    public class ArangoCollectionIndices
    {
        public ArangoCollection Collection { get; set; }
        public ICollection<ArangoIndex> Indices { get; set; } = new List<ArangoIndex>();
    }

    public class ArangoStructureUpdate
    {
        public ICollection<ArangoCollectionIndices> Collections { get; set; } = new List<ArangoCollectionIndices>();
        public ICollection<ArangoGraph> Graphs { get; set; } = new List<ArangoGraph>();
        public ICollection<ArangoAnalyzer> Analyzers { get; set; } = new List<ArangoAnalyzer>();
        public ICollection<ArangoView> Views { get; set; } = new List<ArangoView>();
    }

    public class ArangoMigrationOptions
    {
        public bool DropExcess { get; set; }
    }

    public class ArangoMigrationService
    {
        private readonly IArangoContext _arango;
        private readonly List<IArangoMigration> _migrations = new List<IArangoMigration>();

        public ArangoMigrationService(IArangoContext arango)
        {
            _arango = arango;
        }

        public string HistoryCollection { get; set; } = "MigrationHistory";

        public async Task<ArangoStructureUpdate> GetCurrentStructureAsync(ArangoHandle db, CancellationToken cancellationToken = default)
        {
            var snapshot = new ArangoStructureUpdate();

            var collections = await _arango.Collection.ListAsync(db);
            var graphs = await _arango.Graph.ListAsync(db);
            var analyzers = await _arango.Analyzer.ListAsync(db);
            var views = await _arango.View.ListAsync(db);

            foreach (var collection in collections)
            {
                var indices = await _arango.Index.ListAsync(db, collection, cancellationToken);

                var c = new ArangoCollectionIndices
                {
                    Collection = new ArangoCollection
                    {
                        Name = collection
                    },
                    Indices = indices.Select(i => new ArangoIndex
                    {
                        Name = i
                    }).ToList()
                };

                snapshot.Collections.Add(c);
            }

            snapshot.Analyzers = analyzers;
            snapshot.Graphs = graphs.Select(g => new ArangoGraph
            {
                Name = g
            }).ToList();

            snapshot.Views = views.Select(v => new ArangoView
            {
                Name = v
            }).ToList();

            return snapshot;
        }

        public async Task ApplyStructureUpdateAsync(ArangoHandle db, ArangoStructureUpdate update, ArangoMigrationOptions options = null)
        {
            options ??= new ArangoMigrationOptions();

            var current = await GetCurrentStructureAsync(db);

            foreach (var targetCollection in update.Collections)
            {
                var currentCollection =
                    current.Collections.SingleOrDefault(x => x.Collection.Name == targetCollection.Collection.Name);

                if (currentCollection == null)
                {
                    await _arango.Collection.CreateAsync(db, targetCollection.Collection);

                    foreach (var idx in targetCollection.Indices)
                    {
                        await _arango.Index.CreateAsync(db, targetCollection.Collection.Name, idx);
                    }
                }
                else
                {
                    foreach (var targetIndex in targetCollection.Indices)
                    {
                        var currentIndex = currentCollection.Indices.SingleOrDefault(x => x.Name == targetIndex.Name);

                        if (currentIndex == null)
                            await _arango.Index.CreateAsync(db, targetCollection.Collection.Name, targetIndex);
                        else
                        {
                            await _arango.Index.DropAsync(db, targetIndex.Name);
                            await _arango.Index.CreateAsync(db, targetCollection.Collection.Name, targetIndex);
                        }
                    }
                }
            }

            foreach (var targetGraph in update.Graphs)
            {
                var currentGraph =
                    current.Graphs.SingleOrDefault(x => x.Name == targetGraph.Name);

                if (currentGraph == null)
                {
                    await _arango.Graph.CreateAsync(db, targetGraph);
                }
                else
                {
                    await _arango.Graph.DropAsync(db, targetGraph.Name);
                    await _arango.Graph.CreateAsync(db, targetGraph);
                }
            }

            foreach (var targetAnalyzer in update.Analyzers)
            {
                var currentAnalyzer =
                    current.Analyzers.SingleOrDefault(x => x.Name == targetAnalyzer.Name);

                if (currentAnalyzer == null)
                {
                    await _arango.Analyzer.CreateAsync(db, targetAnalyzer);
                }
                else
                {
                    await _arango.Analyzer.DeleteAsync(db, currentAnalyzer.Name, true);
                    await _arango.Analyzer.CreateAsync(db, currentAnalyzer);
                }
            }

            foreach (var targetView in update.Views)
            {
                var currentView =
                    current.Views.SingleOrDefault(x => x.Name == targetView.Name);

                if (currentView == null)
                {
                    await _arango.View.CreateAsync(db, targetView);
                }
                else
                {
                    // await _arango.View.DropAsync(db, targetView.Name);
                    // await _arango.View.CreateAsync(db, targetView);
                }
            }
        }

        /// <summary>
        ///  Add Migrations from assembly
        /// </summary>
        /// <param name="assembly"></param>
        public void AddMigrations(Assembly assembly)
        {
            var type = typeof(IArangoMigration);
            var migrations = assembly.GetTypes()
                .Where(t => type.IsAssignableFrom(t) && !t.IsInterface)
                .Select(t => Activator.CreateInstance(t, true))
                .Cast<IArangoMigration>().ToList();
            _migrations.AddRange(migrations);
        }

        /// <summary>
        /// Add migration
        /// </summary>
        /// <param name="migration"></param>
        public void AddMigration(IArangoMigration migration)
        {
            _migrations.Add(migration);
        }

        /// <summary>
        ///  Apply migrations to latest 
        /// </summary>
        /// <param name="db"></param>
        /// <returns></returns>
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
                    await x.Up(_arango, db);

                    await _arango.Document.CreateAsync(db, HistoryCollection, new MigrationEntity
                    {
                        Key = x.Id.ToString(),
                        Name = x.Name,
                        Created = DateTime.UtcNow
                    });
                }
        }

        private class MigrationEntity
        {
            public string Key { get; set; }
            public string Name { get; set; }
            public DateTime Created { get; set; }
        }
    }
}