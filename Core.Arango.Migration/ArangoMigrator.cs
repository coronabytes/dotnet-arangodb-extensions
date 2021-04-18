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
    /// <summary>
    ///    Arango Migration System
    /// </summary>
    public class ArangoMigrator : IArangoMigrator
    {
        private readonly IArangoContext _arango;
        private readonly List<IArangoMigration> _migrations = new List<IArangoMigration>();

        /// <summary>
        ///  Arango Migration System
        /// </summary>
        public ArangoMigrator(IArangoContext arango)
        {
            _arango = arango;
        }

        /// <inheritdoc/>
        public string HistoryCollection { get; set; } = "MigrationHistory";

        /// <inheritdoc/>
        public bool Compare(object a, object b)
        {
            if (a == null && b == null)
                return true;

            if (a == null || b == null)
                return false;

            var ja = _arango.Configuration.Serializer.Serialize(a);
            var jb = _arango.Configuration.Serializer.Serialize(b);

            return ja.Equals(jb, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public async Task<ArangoStructure> GetStructureAsync(ArangoHandle db,
            CancellationToken cancellationToken = default)
        {
            var snapshot = new ArangoStructure();

            var collectionsInfos = await _arango.Collection.ListAsync(db);
            var graphs = await _arango.Graph.ListAsync(db);
            var analyzers = await _arango.Analyzer.ListAsync(db);
            var viewInfos = await _arango.View.ListAsync(db);
            var functions = await _arango.Function.ListAsync(db);

            foreach (var cinfo in collectionsInfos)
            {
                var collection = await _arango.Collection.GetAsync(db, cinfo.Name);
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

                if (g.ExtensionData != null)
                {
                    g.Options ??= new ArangoGraphOptions();

                    // Normalize graph options for standalone / cluster

                    if (g.ExtensionData?.ContainsKey("numberOfShards") == true)
                    {
                        g.Options.NumberOfShards = (int) (long) g.ExtensionData["numberOfShards"];
                        g.ExtensionData.Remove("numberOfShards");

                        if (g.Options.NumberOfShards == 1)
                            g.Options.NumberOfShards = null;
                    }

                    if (g.ExtensionData?.ContainsKey("replicationFactor") == true)
                    {
                        g.Options.ReplicationFactor = g.ExtensionData["replicationFactor"];
                        g.ExtensionData.Remove("replicationFactor");

                        if (g.Options.ReplicationFactor is long r1 && r1 == 1)
                            g.Options.ReplicationFactor = null;

                        if (g.Options.ReplicationFactor is int r2 && r2 == 1)
                            g.Options.ReplicationFactor = null;
                    }

                    if (g.ExtensionData?.ContainsKey("minReplicationFactor") == true)
                    {
                        g.Options.WriteConcern = (int) (long) g.ExtensionData["minReplicationFactor"];
                        g.ExtensionData.Remove("minReplicationFactor");

                        if (g.Options.WriteConcern == 1)
                            g.Options.WriteConcern = null;
                    }

                    if (g.ExtensionData?.ContainsKey("smartGraphAttribute") == true)
                    {
                        g.Options.SmartGraphAttribute = (string) g.ExtensionData["smartGraphAttribute"];
                        g.ExtensionData.Remove("smartGraphAttribute");
                    }
                }
            }

            snapshot.Graphs = graphs.ToList();

            foreach (var viewinfo in viewInfos)
            {
                var v = await _arango.View.GetPropertiesAsync(db, viewinfo.Name);

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

            snapshot.Functions = functions.ToList();

            return snapshot;
        }

        /// <inheritdoc/>
        public async Task ApplyStructureAsync(ArangoHandle db, ArangoStructure update,
            ArangoMigrationOptions options = null)
        {
            options ??= new ArangoMigrationOptions();

            var current = await GetStructureAsync(db);

            foreach (var targetCollection in update.Collections ?? new List<ArangoCollectionIndices>())
            {
                var currentCollection =
                    current.Collections.SingleOrDefault(x => x.Collection.Name == targetCollection.Collection.Name);

                if (currentCollection == null)
                {
                    options.Notify?.Invoke(new ArangoMigrationNotification
                    {
                        Object = ArangoMigrationObject.Collection,
                        State = ArangoMigrationState.Create,
                        Name = targetCollection.Collection.Name
                    });

                    if (!options.DryRun)
                        await _arango.Collection.CreateAsync(db, targetCollection.Collection);

                    foreach (var idx in targetCollection.Indices)
                    {
                        options.Notify?.Invoke(new ArangoMigrationNotification
                        {
                            Object = ArangoMigrationObject.Index,
                            State = ArangoMigrationState.Create,
                            Name = idx.Name
                        });

                        if (!options.DryRun)
                            await _arango.Index.CreateAsync(db, targetCollection.Collection.Name, idx);
                    }
                }
                else
                {
                    // No collection updates supported

                    options.Notify?.Invoke(new ArangoMigrationNotification
                    {
                        Object = ArangoMigrationObject.Collection,
                        State = ArangoMigrationState.Identical,
                        Name = targetCollection.Collection.Name
                    });

                    foreach (var targetIndex in targetCollection.Indices)
                    {
                        var currentIndex = currentCollection.Indices.SingleOrDefault(x => x.Name == targetIndex.Name);

                        if (currentIndex == null)
                        {
                            options.Notify?.Invoke(new ArangoMigrationNotification
                            {
                                Object = ArangoMigrationObject.Index,
                                State = ArangoMigrationState.Create,
                                Name = targetIndex.Name
                            });

                            if (!options.DryRun)
                                await _arango.Index.CreateAsync(db, targetCollection.Collection.Name, targetIndex);
                        }
                        else if (!Compare(currentIndex, targetIndex))
                        {
                            options.Notify?.Invoke(new ArangoMigrationNotification
                            {
                                Object = ArangoMigrationObject.Index,
                                State = ArangoMigrationState.Update,
                                Name = targetIndex.Name
                            });

                            if (!options.DryRun)
                            {
                                await _arango.Index.DropAsync(db, targetIndex.Name);
                                await _arango.Index.CreateAsync(db, targetCollection.Collection.Name, targetIndex);
                            }
                        }
                        else
                        {
                            options.Notify?.Invoke(new ArangoMigrationNotification
                            {
                                Object = ArangoMigrationObject.Index,
                                State = ArangoMigrationState.Identical,
                                Name = targetIndex.Name
                            });
                        }
                    }
                }
            }

            foreach (var targetGraph in update.Graphs ?? new List<ArangoGraph>())
            {
                var currentGraph =
                    current.Graphs.SingleOrDefault(x => x.Name == targetGraph.Name);

                if (currentGraph == null)
                {
                    options.Notify?.Invoke(new ArangoMigrationNotification
                    {
                        Object = ArangoMigrationObject.Graph,
                        State = ArangoMigrationState.Create,
                        Name = targetGraph.Name
                    });

                    if (!options.DryRun)
                        await _arango.Graph.CreateAsync(db, targetGraph);
                }
                else if (!Compare(currentGraph, targetGraph))
                {
                    options.Notify?.Invoke(new ArangoMigrationNotification
                    {
                        Object = ArangoMigrationObject.Graph,
                        State = ArangoMigrationState.Update,
                        Name = targetGraph.Name
                    });

                    if (!options.DryRun)
                    {
                        await _arango.Graph.DropAsync(db, targetGraph.Name);
                        await _arango.Graph.CreateAsync(db, targetGraph);
                    }
                }
                else
                {
                    options.Notify?.Invoke(new ArangoMigrationNotification
                    {
                        Object = ArangoMigrationObject.Graph,
                        State = ArangoMigrationState.Identical,
                        Name = targetGraph.Name
                    });
                }
            }

            foreach (var targetAnalyzer in update.Analyzers ?? new List<ArangoAnalyzer>())
            {
                var currentAnalyzer =
                    current.Analyzers.SingleOrDefault(x => x.Name == targetAnalyzer.Name);

                if (currentAnalyzer == null)
                {
                    options.Notify?.Invoke(new ArangoMigrationNotification
                    {
                        Object = ArangoMigrationObject.Analyzer,
                        State = ArangoMigrationState.Create,
                        Name = targetAnalyzer.Name
                    });

                    if (!options.DryRun)
                        await _arango.Analyzer.CreateAsync(db, targetAnalyzer);
                }
                else if (!Compare(currentAnalyzer, targetAnalyzer))
                {
                    options.Notify?.Invoke(new ArangoMigrationNotification
                    {
                        Object = ArangoMigrationObject.Analyzer,
                        State = ArangoMigrationState.Update,
                        Name = targetAnalyzer.Name
                    });

                    if (!options.DryRun)
                    {
                        await _arango.Analyzer.DeleteAsync(db, currentAnalyzer.Name, true);
                        await _arango.Analyzer.CreateAsync(db, currentAnalyzer);
                    }
                }
                else
                {
                    options.Notify?.Invoke(new ArangoMigrationNotification
                    {
                        Object = ArangoMigrationObject.Analyzer,
                        State = ArangoMigrationState.Identical,
                        Name = targetAnalyzer.Name
                    });
                }
            }

            foreach (var targetView in update.Views ?? new List<ArangoView>())
            {
                var currentView =
                    current.Views.SingleOrDefault(x => x.Name == targetView.Name);

                if (currentView == null)
                {
                    options.Notify?.Invoke(new ArangoMigrationNotification
                    {
                        Object = ArangoMigrationObject.View,
                        State = ArangoMigrationState.Create,
                        Name = targetView.Name
                    });

                    if (!options.DryRun)
                        await _arango.View.CreateAsync(db, targetView);
                }
                else if (!Compare(currentView, targetView))
                {
                    options.Notify?.Invoke(new ArangoMigrationNotification
                    {
                        Object = ArangoMigrationObject.View,
                        State = ArangoMigrationState.Update,
                        Name = targetView.Name
                    });

                    if (!options.DryRun)
                    {
                        await _arango.View.DropAsync(db, targetView.Name);
                        await _arango.View.CreateAsync(db, targetView);
                    }
                }
                else
                {
                    options.Notify?.Invoke(new ArangoMigrationNotification
                    {
                        Object = ArangoMigrationObject.View,
                        State = ArangoMigrationState.Identical,
                        Name = targetView.Name
                    });
                }
            }

            foreach (var targetFunction in update.Functions ?? new List<ArangoFunctionDefinition>())
            {
                var currentFunction =
                    current.Functions.SingleOrDefault(x => x.Name == targetFunction.Name);

                if (currentFunction == null)
                {
                    
                    options.Notify?.Invoke(new ArangoMigrationNotification
                    {
                        Object = ArangoMigrationObject.Function,
                        State = ArangoMigrationState.Create,
                        Name = targetFunction.Name
                    });

                    if (!options.DryRun)
                        await _arango.Function.CreateAsync(db, targetFunction);
                }
                else if (!Compare(currentFunction, targetFunction))
                {
                    options.Notify?.Invoke(new ArangoMigrationNotification
                    {
                        Object = ArangoMigrationObject.Function,
                        State = ArangoMigrationState.Update,
                        Name = targetFunction.Name
                    });


                    if (!options.DryRun)
                    {
                        await _arango.Function.RemoveAsync(db, targetFunction.Name);
                        await _arango.Function.CreateAsync(db, targetFunction);
                    }
                }
                else
                {
                    options.Notify?.Invoke(new ArangoMigrationNotification
                    {
                        Object = ArangoMigrationObject.Function,
                        State = ArangoMigrationState.Identical,
                        Name = targetFunction.Name
                    });
                }
            }

            // drop

            if (options.DropExcess)
            {
                foreach (var currentView in current.Views
                    .Where(x=> update.Views.All(y => y.Name != x.Name)))
                {
                    options.Notify?.Invoke(new ArangoMigrationNotification
                    {
                        Object = ArangoMigrationObject.View,
                        State = ArangoMigrationState.Delete,
                        Name = currentView.Name
                    });

                    if (!options.DryRun)
                        await _arango.View.DropAsync(db, currentView.Name);
                }

                foreach (var currentAnalyzer in current.Analyzers
                    .Where(x=> update.Analyzers.All(y => y.Name != x.Name)))
                {
                    options.Notify?.Invoke(new ArangoMigrationNotification
                    {
                        Object = ArangoMigrationObject.Analyzer,
                        State = ArangoMigrationState.Delete,
                        Name = currentAnalyzer.Name
                    });

                    if (!options.DryRun)
                        await _arango.Analyzer.DeleteAsync(db, currentAnalyzer.Name);
                }

                foreach (var currentGraph in current.Graphs
                    .Where(x=> update.Graphs.All(y => y.Name != x.Name)))
                {
                    options.Notify?.Invoke(new ArangoMigrationNotification
                    {
                        Object = ArangoMigrationObject.Graph,
                        State = ArangoMigrationState.Delete,
                        Name = currentGraph.Name
                    });

                    if (!options.DryRun)
                        await _arango.Graph.DropAsync(db, currentGraph.Name);
                }

                foreach (var currentCollection in current.Collections
                    .Where(x=> update.Collections.All(y => y.Collection.Name != x.Collection.Name)))
                {
                    options.Notify?.Invoke(new ArangoMigrationNotification
                    {
                        Object = ArangoMigrationObject.Collection,
                        State = ArangoMigrationState.Delete,
                        Name = currentCollection.Collection.Name
                    });

                    if (!options.DryRun)
                        await _arango.Collection.DropAsync(db, currentCollection.Collection.Name);
                }

                foreach (var currentFunction in current.Functions
                    .Where(x=> update.Functions.All(y => y.Name != x.Name)))
                {
                    options.Notify?.Invoke(new ArangoMigrationNotification
                    {
                        Object = ArangoMigrationObject.Function,
                        State = ArangoMigrationState.Delete,
                        Name = currentFunction.Name
                    });

                    if (!options.DryRun)
                        await _arango.Function.RemoveAsync(db, currentFunction.Name);
                }
            }
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public void AddMigration(IArangoMigration migration)
        {
            _migrations.Add(migration);
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public async Task ExportAsync(ArangoHandle db, Stream output, ArangoMigrationScope scope)
        {
            using var zip = new ZipArchive(output, ZipArchiveMode.Create, true, Encoding.UTF8);

            var serializer = _arango.Configuration.Serializer;
            var collections = await _arango.Collection.ListAsync(db);

            var structure = await GetStructureAsync(db);

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

        /// <inheritdoc/>
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
                var structure = serializer.Deserialize<ArangoStructure>(await sr.ReadToEndAsync());

                await ApplyStructureAsync(db, structure);
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

                        await _arango.Document.CreateManyAsync(db, col, docs, 
                            overwriteMode: ArangoOverwriteMode.Replace);
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