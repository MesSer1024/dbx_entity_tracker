using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Newtonsoft.Json;

namespace DbxEntityTracker
{
    class SavedData
    {
        public IDictionary<string, string> AllEntities { get; set; }
        public IDictionary<string, List<DbxMatch>> EntityUsage { get; set; }
    }

    class EntityDbxLib
    {
        public class EntityReference
        {
            public String EntityType { get; set; }
            public string DdfFile { get; set; }
        }

        private IDictionary<string, string> _allEntities;

        public IDictionary<string, string> AllEntities
        {
            get { return _allEntities; }
        }
        private IDictionary<string, List<DbxMatch>> _entityUsage;
        public FileInfo[] dbxFiles;
        public FileInfo[] ddfFiles;
        private CancellationTokenSource _cts;
        private ParallelOptions _po;

        public IDictionary<string, List<DbxMatch>> EntityUsage
        {
            get { return _entityUsage; }
        }

        public bool IsCancelled { get { return _cts.IsCancellationRequested; } }

        public void CancleTasks() {
            _cts.Cancel();
        }

        public void init()
        {
            ddfFiles = new FileInfo[0];
            dbxFiles = new FileInfo[0];
            _po = new ParallelOptions();
            _cts = new CancellationTokenSource();
            _po.CancellationToken = _cts.Token;
            //find files on harddrive
            ddfFiles = GetFilesOfType(AppSettings.DDF_WSROOT, "ddf");
            if (_cts.IsCancellationRequested)
                return;
            dbxFiles = GetFilesOfType(AppSettings.DBX_ROOT, "dbx");
        }

        public void populate() {
            _allEntities = populateDDF(ddfFiles);
            Console.WriteLine("-----------------\n Total Entities: {0}\n-----------------", _allEntities.Keys.Count);

            var dbxMatches = populateDBX(dbxFiles);
            Console.WriteLine("Parsing all dbx-files, found {0} files that might contain entities", dbxMatches.Count);

            _entityUsage = crossReferenceEntitiesWithDbxMatches(_allEntities, dbxMatches);

            var unusedEntities = findUnusedEntities(_allEntities, _entityUsage);
            writeUnusedEntities(unusedEntities);
        }

        public FileInfo[] GetFilesOfType(string rootFolder, string fileType)
        {
            var dir = new DirectoryInfo(rootFolder.ToLower());
            if (!dir.Exists)
                throw new ArgumentException(String.Format("Folder does not exist: {0}", rootFolder));
            Console.WriteLine("Searching for all dbx-files in folder:\n\t{0}", dir.FullName);
            return dir.GetFiles("*." + fileType, SearchOption.AllDirectories);
        }

        private IDictionary<string, string> populateDDF(FileInfo[] ddfFiles)
        {
            var entityTable = new ConcurrentDictionary<string, string>();

            Parallel.ForEach(ddfFiles,_po, (file, state) =>
            {
                using (var sr = new StreamReader(file.FullName))
                {
                    int bracketCount = 0;
                    bool insideEntity = false;
                    bool moduleLocated = false;
                    string module = "";
                    while (!sr.EndOfStream)
                    {
                        var line = sr.ReadLine().Trim();
                        if (!moduleLocated && line.StartsWith("module"))
                        {
                            module = line.Split(' ')[1];
                            if (module.Length > 0)
                                module = module.Remove(module.Length - 1);
                            Console.WriteLine("Module located inside file: {0}, module=''{1}''", file, module);
                            moduleLocated = false; //set to true soon... just make sure that no file contains multiple modules
                        }
                        else if (!insideEntity && line.StartsWith("entity "))
                        {
                            if (module.Length == 0)
                            {
                                //throw new Exception(String.Format("Unable to find module inside DDF-file ({0})", file));
								module = "--N/A--";
                            }
                            var name = line.Substring("entity ".Length);
                            name = name.Split(':')[0].Trim();
                            var fakename = module + "." + name + "Data";
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
                if (_po.CancellationToken.IsCancellationRequested)
                    state.Break();
            });

            //ConcurrentDictionary seems to be slow when it comes to lookup, negelecting to convert to regular dictionary causes crossreference to take 10s instead of <100ms
            return new Dictionary<string, string>(entityTable); 
        }

        private List<DbxParsingData> populateDBX(FileInfo[] dbxFiles)
        {
            var allCollections = new List<DbxParsingData>();
            var mutex = new object();
            Parallel.ForEach(dbxFiles, _po, (file, state) =>
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
                            var assetType = findSubstring(line, "type=\"", "\"");
                            asset.LineNumbers.Add(lineNumber);
                            asset.EntityTypes.Add(assetType);
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

                if (_po.CancellationToken.IsCancellationRequested)
                    state.Break();
            });
            return allCollections;
        }

        public string findSubstring(string source, string identifier, int count)
        {
            var idx = source.IndexOf(identifier);
            return source.Substring(idx + identifier.Length, count);
        }

        public string findSubstring(string source, string startIdentifier, string endIdentifier)
        {
            var startIdx = source.IndexOf(startIdentifier) + startIdentifier.Length;
            var endIdx = source.IndexOf(endIdentifier, startIdx + 1);
            return source.Substring(startIdx, endIdx - startIdx);
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
                foreach (var dbxType in dbx.EntityTypes)
                {
                    //if (allEntities.Keys.Contains(dbxType))
                    if (allEntities.ContainsKey(dbxType))
                    {
                        var asset = new DbxMatch() { FilePath = dbx.Filepath, LineNumber = dbx.LineNumbers[idx], EntityType = dbx.EntityTypes[idx] };
                        if (entityUsage.ContainsKey(dbxType))
                            entityUsage[dbxType].Add(asset);
                        else
                            entityUsage.Add(dbxType, new List<DbxMatch>() { asset });
                    }
                    idx++;
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
            writeFile("./output/unused_entities.txt", sb.ToString());
        }

        public DbxMatch GetDbxInfo(string key, int idx)
        {
            if(_entityUsage.ContainsKey(key) && idx < _entityUsage[key].Count)
                return _entityUsage[key][idx];
            return null;
        }

        public string FindDdfSource(string entityIdentifier)
        {
            return _allEntities.ContainsKey(entityIdentifier) ? _allEntities[entityIdentifier] : null;
        }

        public List<DbxMatch> FindDbxReferences(string entityIdentifier)
        {
            return _entityUsage.ContainsKey(entityIdentifier) ? _entityUsage[entityIdentifier] : null;
        }

        public void save()
        {
            var save = new SavedData();
            save.AllEntities = AllEntities;
            save.EntityUsage = EntityUsage;
            var output = JsonConvert.SerializeObject(save);
            writeFile("./output/_LastSave.det", output);
        }

        private void writeFile(string path, string data)
        {
            var file = new FileInfo(path);
            if (!file.Directory.Exists)
                file.Directory.Create();

            using (var sw = new StreamWriter(file.FullName, false))
            {
                sw.Write(data);
                sw.Flush();
            }
        }

        public void load(string filePath)
        {
            var file = new FileInfo(filePath);
            if (file.Exists)
            {
                using (var sr = new StreamReader(file.FullName))
                {
                    string s = sr.ReadToEnd();
                    var load = JsonConvert.DeserializeObject<SavedData>(s);
                    _allEntities = load.AllEntities;
                    _entityUsage = load.EntityUsage;
                }
            }
            else
            {
                throw new Exception(String.Format("File does not exist \"{0}\"", filePath));
            }
        }
    }
}
