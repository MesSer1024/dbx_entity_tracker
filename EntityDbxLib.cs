using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using dbx_lib;

namespace DbxEntityTracker
{
    class EntityDbxLib
    {
        public class EntityReference
        {
            public String EntityType { get; set; }
            public string DdfFile { get; set; }
        }



        public enum TimestampIdentifiers
        {
            StartTime,
            DdfIO,
            DdfPopulate,
            DbxIO,
            DbxPopulate,
            CheckReferences,
            TotalTime,
        }

        private List<long> _timestamps;
        private List<TimestampIdentifiers> _timestampIdentifiers;
        private IDictionary<string, string> _allEntities;

        public IDictionary<string, string> AllEntities
        {
            get { return _allEntities; }
        }
        private IDictionary<string, List<DbxMatch>> _entityUsage;

        public IDictionary<string, List<DbxMatch>> EntityUsage
        {
            get { return _entityUsage; }
        }
        public List<long> getTimestamps() { return _timestamps; }
        public List<TimestampIdentifiers> getTimestampDescriptions() { return _timestampIdentifiers; }

        public void init()
        {
            _timestampIdentifiers = new List<TimestampIdentifiers>();
            _timestamps = new List<long>(6);

            var lib = new LibMain();
            Stopwatch timer = new Stopwatch();
            timer.Start();
            createTimestamp(TimestampIdentifiers.StartTime, timer);
            var ddfFiles = lib.GetFilesOfType(AppSettings.DDF_WSROOT, "ddf");
            createTimestamp(TimestampIdentifiers.DdfIO, timer);
            _allEntities = populateDDF(ddfFiles);
            createTimestamp(TimestampIdentifiers.DdfPopulate, timer);
            Console.WriteLine("-----------------\n Total Entities: {0}\n-----------------", _allEntities.Keys.Count);

            var dbxFiles = lib.GetFilesOfType(AppSettings.DBX_ROOT, "dbx");
            createTimestamp(TimestampIdentifiers.DbxIO, timer);
            var dbxMatches = populateDBX(dbxFiles);
            createTimestamp(TimestampIdentifiers.DbxPopulate, timer);
            Console.WriteLine("Parsing all dbx-files, found {0} files that might contain entities", dbxMatches.Count);

            _entityUsage = crossReferenceEntitiesWithDbxMatches(_allEntities, dbxMatches);
            createTimestamp(TimestampIdentifiers.CheckReferences, timer);

            Console.WriteLine("Time to populate ddf files: {0}ms", (_timestamps[2] - _timestamps[0]));
            Console.WriteLine("Time to populate dbx files: {0}ms", (_timestamps[4] - _timestamps[2]));
            Console.WriteLine("Time to map dbx files to entities: {0}ms", (_timestamps[5] - _timestamps[4]));
            Console.WriteLine("Total Time: {0}ms", (_timestamps[5] - _timestamps[0]));

            var unusedEntities = findUnusedEntities(_allEntities, _entityUsage);
            writeUnusedEntities(unusedEntities);
            int foo = 0;
        }

        private void createTimestamp(TimestampIdentifiers id, Stopwatch timer)
        {
            _timestampIdentifiers.Add(id);
            _timestamps.Add(timer.ElapsedMilliseconds);
            timer.Restart();
        }

        public string FindDdfSource(string entityIdentifier)
        {
            return _allEntities.ContainsKey(entityIdentifier) ? _allEntities[entityIdentifier] : null;
        }

        public List<DbxMatch> FindDbxReferences(string entityIdentifier)
        {
            return _entityUsage.ContainsKey(entityIdentifier) ? _entityUsage[entityIdentifier] : null;
        }

        private IDictionary<string, string> populateDDF(FileInfo[] ddfFiles)
        {
            var entityTable = new ConcurrentDictionary<string, string>();

            Parallel.ForEach(ddfFiles, (file) =>
            {
                using (var sr = new StreamReader(file.FullName))
                {
                    int bracketCount = 0;
                    bool insideEntity = false;
                    while (!sr.EndOfStream)
                    {
                        var line = sr.ReadLine().Trim();
                        if (!insideEntity && line.StartsWith("entity "))
                        {
                            var name = line.Substring("entity ".Length);
                            name = name.Split(':')[0].Trim();
                            var fakename = "WSShared." + name + "Data"; //Append something
                            entityTable.GetOrAdd(fakename, file.FullName);
                            insideEntity = true;
                        }
                        else if (insideEntity)
                        {
                            if (line.Contains('{'))
                                bracketCount++;
                            if (line.Contains('}'))
                                bracketCount--;
                            if (bracketCount == 0)
                                insideEntity = false;
                        }
                    }
                }
            });

            //ConcurrentDictionary seems to be slow when it comes to lookup, negelecting to convert to regular dictionary causes crossreference to take 10s instead of <100ms
            return new Dictionary<string, string>(entityTable); 
        }

        private List<DbxParsingData> populateDBX(FileInfo[] dbxFiles)
        {
            var allCollections = new List<DbxParsingData>();
            var mutex = new object();
            Parallel.ForEach(dbxFiles, (file) =>
            {
                var collection = new List<DbxParsingData>();
                using (var sr = new StreamReader(file.FullName))
                {
                    var asset = new DbxParsingData();
                    asset.Filepath = file.FullName;
                    int lineNumber = 0;
                    bool valid = false;
                    while (!sr.EndOfStream)
                    {
                        var line = sr.ReadLine();
                        lineNumber++;
                        if (line.Contains("type=\""))
                        {
                            var assetType = DbxUtils.findSubstring(line, "type=\"", "\"");
                            asset.LineNumbers.Add(lineNumber);
                            asset.EntityType.Add(assetType);
                            valid = true;
                        }
                    }
                    if (valid)
                        collection.Add(asset);
                }
                if (collection.Count > 0)
                {
                    lock (mutex)
                    {
                        allCollections.AddRange(collection);
                    }
                }
            });
            return allCollections;
        }

        /// <summary>
        /// Dictionary<EntityName, List<DbxFile>>
        /// </summary>
        private IDictionary<string, List<DbxMatch>> crossReferenceEntitiesWithDbxMatches(IDictionary<string, string> allEntities, List<DbxParsingData> dbxMatches)
        {
            var entityUsage = new Dictionary<string, List<DbxMatch>>(); //Dictionary<EntityName, List<DbxFile>>
            foreach (var dbx in dbxMatches)
            {
                int idx = 0;
                foreach (var dbxType in dbx.EntityType)
                {
                    //if (allEntities.Keys.Contains(dbxType))
                    if (allEntities.ContainsKey(dbxType))
                    {
                        var asset = new DbxMatch() { FilePath = dbx.Filepath, LineNumber = dbx.LineNumbers[idx], EntityType = dbx.EntityType[idx] };
                        if (entityUsage.ContainsKey(dbxType))
                            entityUsage[dbxType].Add(asset);
                        else
                            entityUsage.Add(dbxType, new List<DbxMatch>() { asset });
                    }
                }
            }

            return entityUsage;
        }

        private static List<string> findUnusedEntities(IDictionary<string, string> all, IDictionary<string, List<DbxMatch>> used)
        {
            var unusedEntities = new List<string>();
            foreach (var e in all)
            {
                if (!used.ContainsKey(e.Key))
                {
                    unusedEntities.Add(e.Key);
                }
            }
            return unusedEntities;
        }

        private void writeUnusedEntities(List<string> unusedEntities)
        {
            var sb = new StringBuilder();
            foreach (var u in unusedEntities)
            {
                sb.AppendLine(u);
            }
            Console.WriteLine("Unused Entities: {0}", sb.ToString());

            using (var sw = new StreamWriter("../../_docs/unused_entities.txt", false))
            {
                sw.Write(sb.ToString());
                sw.Flush();
                sw.Close();
            }
        }

        public DbxMatch GetDbxInfo(string key, int idx)
        {
            if(_entityUsage.ContainsKey(key) && idx < _entityUsage[key].Count)
                return _entityUsage[key][idx];
            return null;
        }
    }
}
