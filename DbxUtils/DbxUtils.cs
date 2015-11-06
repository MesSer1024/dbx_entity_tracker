using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dice.Frostbite.Framework
{
    public static class DbxUtils
    {
        public class PartitionData
        {
            public string Filepath { get; set; }
            public string PartitionGuid { get; set; }

            public List<int> InstanceLineNumbers { get; private set; }
            public List<string> InstanceTypes { get; private set; }
            public List<string> InstanceGuids { get; private set; }

            public PartitionData()
            {
                InstanceLineNumbers = new List<int>();
                InstanceTypes = new List<string>();
                InstanceGuids = new List<string>();
                Filepath = "";
            }
        }

        public class AssetInstance
        {
            public string Guid { get; set; }
            public string AssetType { get; set; }

            public string PartitionGuid { get; set; }
            public string PartitionPath { get; set; }
            public string PartitionName { get { return GetPartitionShortName(PartitionPath); } }

            private static string GetPartitionShortName(string name)
            {
                int idx = name.LastIndexOf("\\") + 1;
                return name.Substring(idx, name.Length - idx - 4); //remove ".dbx" as well
            }
        }

        public static FileInfo[] GetFiles(string rootFolder, string fileType = "dbx")
        {
            var dir = new DirectoryInfo(rootFolder.ToLower());
            return dir.GetFiles("*." + fileType, SearchOption.AllDirectories);
        }

        /// <summary>
        /// Multi-Threaded raw-text-search for locating all Instances (asset-type & asset-guid) inside a partition/dbxFile
        /// Uses ParseDbxFile(...) internally
        /// </summary>
        /// <param name="dbxFiles">unmodified by code</param>
        /// <returns>Contains raw-text-search-data related to all instances that exists in all partitions</returns>
        public static List<DbxUtils.PartitionData> ParseDbxFiles(IEnumerable<FileInfo> dbxFiles)
        {
            var files = dbxFiles.OrderByDescending(a => a.Length).ToList();
            //#TODO: See if we get performance increase if we let 50% of threads read biggest files and 50% read smallest files
            var threadLock = new object();
            var partitions = new List<DbxUtils.PartitionData>();
            Parallel.ForEach(dbxFiles, (file, state) =>
            {
                var item = DbxUtils.ParseDbxFile(file);
                if (item != null)
                {
                    lock (threadLock)
                    {
                        partitions.Add(item);
                    }
                }
            });

            return partitions;
        }

        /// <summary>
        /// Raw-text-searches for all instances inside a partition/dbxFile
        /// </summary>
        /// <param name="file"></param>
        /// <returns>null or data, CreateInstances expects this format</returns>
        public static PartitionData ParseDbxFile(FileInfo file)
        {
            if (!file.Exists)
                return null;

            const string dbx_type_identifier = "type=\"";
            const int guid_length = 36;

            using (var sr = new StreamReader(file.FullName))
            {
                var asset = new PartitionData()
                {
                    Filepath = file.FullName,
                };

                int lineNumber = 0;
                bool valid = false;
                bool primaryInstanceFound = false;
                while (!sr.EndOfStream)
                {
                    var line = sr.ReadLine();
                    lineNumber++;

                    if (!primaryInstanceFound)
                    {
                        if (lineNumber > 20)
                            throw new Exception("Unable to locate primary instance within first 20 lines of dbx-file");
                        if (line.Contains("primaryInstance"))
                        {
                            var partitionGuid = findSubstring(line, "guid=\"", guid_length);
                            asset.PartitionGuid = partitionGuid;
                            primaryInstanceFound = true;
                        }
                    }

                    var startIdx = line.IndexOf(dbx_type_identifier);
                    if (startIdx >= 0)
                    {
                        //Given a line that looks like: <instance guid="5a5cdf29-50d5-44bd-9538-a08ba983872f" type="Entity.SchematicShortcutCommonData">
                            //Extract the type-identifier: Entity.SchematicShortcutCommonData
                            //Extract the guid: 5a5cdf29-50d5-44bd-9538-a08ba983872f
                        var assetType = findSubstring(line, startIdx + dbx_type_identifier.Length, "\"");
                        var guid = findSubstring(line, "guid=\"", guid_length);

                        asset.InstanceLineNumbers.Add(lineNumber);
                        asset.InstanceTypes.Add(assetType);
                        asset.InstanceGuids.Add(guid);

                        valid = true;
                    }
                }
                if (valid)
                    return asset;
            }
            return null;
        }

        private static string findSubstring(string line, int startIdx, string endIdentifier)
        {
            var endIdx = line.IndexOf(endIdentifier, startIdx + 1);
            return line.Substring(startIdx, endIdx - startIdx);
        }

        private static string findSubstring(string source, string identifier, int count)
        {
            var idx = source.IndexOf(identifier);
            return source.Substring(idx + identifier.Length, count);
        }

        /// <summary>
        /// Create AssetInstances given PartitionData
        /// </summary>
        /// <param name="allItems">see ParseDbxFiles</param>
        /// <returns>Unique instances for each instance located inside a PartitionData</returns>
        public static List<AssetInstance> CreateInstances(List<PartitionData> partitions)
        {
            List<AssetInstance> instances = new List<AssetInstance>();

            foreach (var partition in partitions)
            {
                for (int i = 0; i < partition.InstanceGuids.Count; i++)
                {
                    var instance = new AssetInstance()
                    {
                        AssetType = partition.InstanceTypes[i],
                        Guid = partition.InstanceGuids[i],
                        PartitionGuid = partition.PartitionGuid,
                        PartitionPath = partition.Filepath
                    };
                    instances.Add(instance);
                }
            }

            return instances;
        }

        /// <summary>
        /// Filters all instances given 'identifier' and orders them by PartitionName
        /// </summary>
        /// <param name="instances"></param>
        /// <param name="identifier"></param>
        /// <returns></returns>
        public static List<DbxUtils.AssetInstance> GetEntities(List<DbxUtils.AssetInstance> instances, string identifier = "EntityData")
        {
            var items = instances.FindAll(a => a.AssetType.Contains(identifier));
            items.OrderBy(a => a.PartitionName);
            return items.ToList();
        }

        /// <summary>
        /// Filters all instances by unique instances.AssetTypes & then sorts them alphabetically
        /// </summary>
        /// <param name="instances"></param>
        /// <returns></returns>
        public static List<string> GetUniqueAssetTypes(List<AssetInstance> instances)
        {
            var foo = from entity in instances
                      orderby entity.AssetType ascending
                      select entity.AssetType;

            return foo.Distinct().ToList();
        }

        /// <summary>
        /// Describing an AssetInstance in a UI-friendly manner
        /// </summary>
        /// <param name="asset"></param>
        /// <returns></returns>
        public static string GetAssetDescription(DbxUtils.AssetInstance asset)
        {
            var sb = new StringBuilder();
            sb.AppendLine("PartitionInfo:");
            sb.AppendLine(String.Format("\tPartitionName: {0}", asset.PartitionName));
            sb.AppendLine(String.Format("\tPartitionPath: {0}", asset.PartitionPath));
            sb.AppendLine(String.Format("\tPartitionGuid: {0}", asset.PartitionGuid));
            sb.AppendLine("\nAssetInfo:");
            sb.AppendLine(String.Format("\tGUID: {0}", asset.Guid));
            sb.AppendLine(String.Format("\tAssetType: {0}", asset.AssetType));
            sb.AppendLine("\nFrosted Hyperlink:");
            sb.AppendLine(String.Format("\tFrosted://{0};@{1}/{2}", "", asset.PartitionGuid, asset.Guid));

            return sb.ToString();
        }

        /// <summary>
        /// Generate a collection with file.Fullename & file.LastModified
        /// </summary>
        /// <param name="files"></param>
        /// <returns></returns>
        public static Dictionary<string, long> GetFileTimestamps(IEnumerable<FileInfo> files)
        {
            var output = new Dictionary<string, long>();
            foreach (var file in files)
            {
                if (!file.Exists)
                    continue;
                output.Add(file.FullName, file.LastWriteTime.Ticks);
            }
            return output;
        }

        /// <summary>
        /// Generate a collection of all files that have been modified when compared against fileTimestamps
        /// </summary>
        /// <param name="files">potentially modified files</param>
        /// <param name="fileTimestamps">FullName/LastModified, see GetFileTimestamps</param>
        /// <returns></returns>
        public static List<FileInfo> GetDirtyFiles(FileInfo[] files, Dictionary<string, long> fileTimestamps)
        {
            var output = new List<FileInfo>();
            foreach (var file in files)
            {
                var key = file.FullName;
                bool differentTimestamps = fileTimestamps.ContainsKey(key) ? fileTimestamps[key] != file.LastWriteTime.Ticks : true;
                if (differentTimestamps)
                {
                    output.Add(file);
                }
            }
            return output;
        }

        /// <summary>
        /// Get entries that exist in database but does no longer have a dbx-file that resides in 'allFiles'
        /// </summary>
        /// <param name="allFiles">All DBX-files that should reside in collection</param>
        /// <param name="fileTimestamps">All files that have been parsed and what version that was parsed</param>
        /// <returns></returns>
        public static List<FileInfo> GetDeletedFiles(FileInfo[] allFiles, Dictionary<string, long> fileTimestamps) {
            var missingFiles = fileTimestamps.Keys.Except(from file in allFiles select file.FullName);
            var deleted = new List<FileInfo>();
            foreach (var file in missingFiles)
            {
                deleted.Add(new FileInfo(file));
            }
            Console.WriteLine("Deleted files = {0}", deleted.Count);
            return deleted;
        }

    }
}