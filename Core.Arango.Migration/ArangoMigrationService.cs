using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
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

        public async Task<ArangoStructureUpdate> GetCurrentStructureAsync(ArangoHandle db,
            CancellationToken cancellationToken = default)
        {
            var snapshot = new ArangoStructureUpdate();

            var collections = await _arango.Collection.ListAsync(db);
            var graphs = await _arango.Graph.ListAsync(db);
            var analyzers = await _arango.Analyzer.ListAsync(db);
            var views = await _arango.View.ListAsync(db);

            foreach (var collection in collections)
            {
                var indices = await _arango.Index.ListAsync(db, collection.Name);

                foreach (var idx in indices)
                    idx.Id = null;

                var c = new ArangoCollectionIndices
                {
                    Collection = collection,
                    Indices = indices.ToList()
                };

                snapshot.Collections.Add(c);
            }

            var analyzerToPatch = analyzers.Where(x => x.Name.Contains("::")).ToList();

            foreach (var a in analyzerToPatch)
            {
                var idx = a.Name.IndexOf("::", StringComparison.Ordinal);
                a.Name = a.Name.Substring(idx + 2);
            }

            snapshot.Analyzers = analyzerToPatch;

            foreach (var g in graphs)
            {
                if (g.ExtensionData?.ContainsKey("_key") == true)
                    g.ExtensionData.Remove("_key");
                if (g.ExtensionData?.ContainsKey("_id") == true)
                    g.ExtensionData.Remove("_id");
                if (g.ExtensionData?.ContainsKey("_rev") == true)
                    g.ExtensionData.Remove("_rev");

                if (g.Options == null && g.ExtensionData != null)
                {
                    var options = new ArangoGraphOptions();

                    if (g.ExtensionData?.ContainsKey("numberOfShards") == true)
                        options.NumberOfShards = (int) (long) g.ExtensionData["numberOfShards"];

                    if (g.ExtensionData?.ContainsKey("replicationFactor") == true)
                        options.ReplicationFactor = g.ExtensionData["replicationFactor"];

                    if (g.ExtensionData?.ContainsKey("minReplicationFactor") == true)
                        options.WriteConcern = (int) (long) g.ExtensionData["minReplicationFactor"];

                    if (g.ExtensionData?.ContainsKey("smartGraphAttribute") == true)
                        options.SmartGraphAttribute = (string) g.ExtensionData["smartGraphAttribute"];

                    g.Options = options;
                }
            }

            snapshot.Graphs = graphs.ToList();

            foreach (var view in views)
            {
                var v = await _arango.View.GetPropertiesAsync(db, view.Name);

                if (v.ExtensionData?.ContainsKey("id") == true)
                    v.ExtensionData.Remove("id");
                if (v.ExtensionData?.ContainsKey("globallyUniqueId") == true)
                    v.ExtensionData.Remove("globallyUniqueId");
                if (v.ExtensionData?.ContainsKey("error") == true)
                    v.ExtensionData.Remove("error");
                if (v.ExtensionData?.ContainsKey("code") == true)
                    v.ExtensionData.Remove("code");

                if (v.PrimarySort != null)
                    foreach (var sort in v.PrimarySort)
                    {
                        sort.Direction = (bool) sort.ExtensionData["asc"]
                            ? ArangoSortDirection.Asc
                            : ArangoSortDirection.Desc;
                        sort.ExtensionData.Remove("asc");
                    }

                snapshot.Views.Add(v);
            }

            return snapshot;
        }

        public async Task ApplyStructureUpdateAsync(ArangoHandle db, ArangoStructureUpdate update,
            ArangoMigrationOptions options = null)
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
                        await _arango.Index.CreateAsync(db, targetCollection.Collection.Name, idx);
                }
                else
                {
                    foreach (var targetIndex in targetCollection.Indices)
                    {
                        var currentIndex = currentCollection.Indices.SingleOrDefault(x => x.Name == targetIndex.Name);

                        if (currentIndex == null)
                        {
                            await _arango.Index.CreateAsync(db, targetCollection.Collection.Name, targetIndex);
                        }
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
                    await _arango.View.DropAsync(db, targetView.Name);
                    await _arango.View.CreateAsync(db, targetView);
                }
            }
        }

        /// <summary>
        ///     Add Migrations from assembly
        /// </summary>
        /// <param name="assembly"></param>
        public void AddMigrations(Assembly assembly)
        {
            var type = typeof(IArangoMigration);
            var migrations = assembly.GetTypes()
                .Where(t => type.IsAssignableFrom(t) && !t.IsInterface)
                .Select(t => Activator.CreateInstance(t, true))
                .Cast<IArangoMigration>()
                .OrderBy(x=>x.Id)
                .ToList();
            _migrations.AddRange(migrations);
        }

        /// <summary>
        ///     Add migration
        /// </summary>
        public void AddMigration(IArangoMigration migration)
        {
            _migrations.Add(migration);
        }

        /// <summary>
        ///     Apply migrations to latest
        /// </summary>
        public async Task UpgradeAsync(ArangoHandle db)
        {
            var cols = await _arango.Collection.ListAsync(db);

            if (cols.All(x => x.Name != HistoryCollection))
                await _arango.Collection.CreateAsync(db, HistoryCollection, ArangoCollectionType.Document);

            var latest =
                (await _arango.Query.FindAsync<MigrationEntity>(db, HistoryCollection, $"true SORT x._key DESC",
                    limit: 1))
                .FirstOrDefault();

            long? version = null;

            if (latest != null)
                version = long.Parse(latest.Key);

            foreach (var x in _migrations.OrderBy(x=>x.Id))
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

        /// <summary>
        ///     Export collection data to ZipArchive
        /// </summary>
        public async Task ExportAsync(ArangoHandle db, Stream output, ArangoMigrationScope scope)
        {
            using var zip = new ZipArchive(output, ZipArchiveMode.Create, true, Encoding.UTF8);

            var serializer = _arango.Configuration.Serializer;
            var collections = await _arango.Collection.ListAsync(db);

            var structure = await GetCurrentStructureAsync(db);

            if (scope.HasFlag(ArangoMigrationScope.Structure)) 
            {
                var s = zip.CreateEntry(".structure.json", CompressionLevel.Fastest);
                await using var sx = s.Open();
                await using var sxw = new StreamWriter(sx);
                await sxw.WriteLineAsync(serializer.Serialize(structure));
            }

            if (scope.HasFlag(ArangoMigrationScope.Data))
            {
                foreach (var col in collections)
                {
                    var i = 1;
                    await foreach (var batch in _arango.Document.ExportAsync<object>(db, col.Name, true, 10, 10000, 30))
                    {
                        var entry = zip.CreateEntry($"{col.Name}.{i++.ToString().PadLeft(4, '0')}.json",
                            CompressionLevel.Fastest);
                        await using var stream = entry.Open();
                        await using var sw = new StreamWriter(stream);
                        await sw.WriteAsync(serializer.Serialize(batch));
                    }
                }
            }
        }

        /// <summary>
        ///     Import collection data from ZipArchive
        /// </summary>
        public async Task ImportAsync(ArangoHandle db, Stream input, ArangoMigrationScope scope)
        {
            using var zip = new ZipArchive(input, ZipArchiveMode.Read);

            var dataBaseExists = await _arango.Database.ExistAsync(db);
            if (!dataBaseExists)
                await _arango.Database.CreateAsync(db);

            var serializer = _arango.Configuration.Serializer;

            if (scope.HasFlag(ArangoMigrationScope.Structure))
            {
                var entry = zip.Entries.SingleOrDefault(x => x.Name == ".structure.json");

                if (entry == null)
                    throw new InvalidDataException(".structure.json missing");

                await using var stream = entry.Open();
                using var sr = new StreamReader(stream);
                var structure = serializer.Deserialize<ArangoStructureUpdate>(await sr.ReadToEndAsync());

                await ApplyStructureUpdateAsync(db, structure);
            }

            foreach (var entry in zip.Entries)
            {
                if (entry.Name.StartsWith("."))
                    continue;

                if (scope.HasFlag(ArangoMigrationScope.Data))
                {
                    if (entry.Name.EndsWith(".json"))
                    {
                        var col = entry.Name.Substring(0, entry.Name.IndexOf('.'));

                        await using var stream = entry.Open();
                        using var sr = new StreamReader(stream);
                        var docs = serializer.Deserialize<List<object>>(await sr.ReadToEndAsync());

                        await _arango.Document.ImportAsync(db, col, docs);
                    }
                }
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