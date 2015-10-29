using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace fb_dbx
{
    static class DbxUtils
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

            public string ParentGuid { get; set; }
            public string ParentPath { get; set; }
        }

        public static string GetRootPath()
        {
            return "D:\\dice\\ws\\ws\\FutureData\\Source";
        }

        public static FileInfo[] GetFiles(string rootFolder, string fileType = "dbx")
        {
            var dir = new DirectoryInfo(rootFolder.ToLower());
            if (!dir.Exists)
                throw new ArgumentException(String.Format("Folder does not exist: {0}", rootFolder));
            Console.WriteLine("Searching for all dbx-files in folder:\n\t{0}", dir.FullName);
            return dir.GetFiles("*." + fileType, SearchOption.AllDirectories);
        }

        public static List<PartitionData> FindInstances(FileInfo file)
        {
            var collection = new List<PartitionData>();
            var dbx_type_identifier = "type=\"";
            using (var sr = new StreamReader(file.FullName))
            {
                var asset = new PartitionData();
                asset.Filepath = file.FullName;
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
                            var partitionGuid = findSubstring(line, "guid=\"", 36);
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
                        var guid = findSubstring(line, "guid=\"", 36);

                        asset.InstanceLineNumbers.Add(lineNumber);
                        asset.InstanceTypes.Add(assetType);
                        asset.InstanceGuids.Add(guid);

                        valid = true;
                    }
                }
                if (valid)
                    collection.Add(asset);
            }
            return collection;
        }

        private static string findSubstring(string line, int startIdx, string endIdentifier)
        {
            var endIdx = line.IndexOf(endIdentifier, startIdx + 1);
            return line.Substring(startIdx, endIdx - startIdx);
        }

        public static string findSubstring(string source, string identifier, int count)
        {
            var idx = source.IndexOf(identifier);
            return source.Substring(idx + identifier.Length, count);
        }

        //public static string findSubstring(string source, string startIdentifier, string endIdentifier)
        //{
        //    var startIdx = source.IndexOf(startIdentifier) + startIdentifier.Length;
        //    var endIdx = source.IndexOf(endIdentifier, startIdx + 1);
        //    return source.Substring(startIdx, endIdx - startIdx);
        //}

        internal static List<AssetInstance> CreateInstances(List<PartitionData> allItems)
        {
            List<AssetInstance> instances = new List<AssetInstance>();

            foreach (var partition in allItems)
            {
                for (int i = 0; i < partition.InstanceGuids.Count; i++)
                {
                    var instance = new AssetInstance()
                    {
                        AssetType = partition.InstanceTypes[i],
                        Guid = partition.InstanceGuids[i],
                        ParentGuid = partition.PartitionGuid,
                        ParentPath = partition.Filepath
                    };
                    instances.Add(instance);
                }
            }

            return instances;
        }

        internal static List<string> FilterEntities(List<AssetInstance> instances)
        {
            var entities = new List<string>();
            foreach (var instance in instances)
            {
                if (instance.AssetType.Contains("EntityData"))
                {
                    entities.Add(instance.AssetType);
                }
            }

            var foo = entities.Distinct().ToList();
            foo.Sort();
            return foo;
        }
    }
}
