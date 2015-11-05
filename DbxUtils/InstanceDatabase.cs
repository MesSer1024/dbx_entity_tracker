using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dice.Frostbite.Framework
{
    public class InstanceDatabase
    {
        private class save_data
        {
            public List<DbxUtils.AssetInstance> Entities { get; set; }
            public Dictionary<string, long> FileTimestamps { get; set; }
        }

        private const string METADATA_FILE = "./state/instance_db/searched_dbx_files.meta";
        private const string SAVE_FILE = "./state/instance_db/_lastSave.et";
        public Dictionary<string, long> ParsedFilesTimestamp { get; set; }

        public List<DbxUtils.AssetInstance> Entities { get; set; }
        public List<string> EntityTypes { get; set; }

        public InstanceDatabase()
        {
            Entities = new List<DbxUtils.AssetInstance>();
            EntityTypes = new List<string>();
            ParsedFilesTimestamp = new Dictionary<string, long>();
        }

        public bool CanLoad()
        {
            return File.Exists(SAVE_FILE);
        }

        public void Load()
        {
            if (CanLoad())
            {
                var s = File.ReadAllText(SAVE_FILE);
                var load = JsonConvert.DeserializeObject<save_data>(s);
                Entities = load.Entities;
                ParsedFilesTimestamp = load.FileTimestamps;

                var files = DbxUtils.GetFiles(DbxUtils.RootPath);


                /// move this to ParseAndAdd-function, should be shareable?

                var dirtyFiles = GetDirtyFiles(files, ParsedFilesTimestamp);

                //remove all references to dirty partitions
                foreach(var file in dirtyFiles)
                {
                    Entities.RemoveAll(a => a.PartitionPath == file.FullName); //potential issues regarding capital/small letters or "not using full paths"
                    ParsedFilesTimestamp.Remove(file.FullName);
                }

                //add the 'dirty partitions' to solution
                var dirtyInstances = GetInstancesFromFiles(dirtyFiles);
                Entities.AddRange(DbxUtils.GetEntities(dirtyInstances));

                var dirtyTimestamps = DbxUtils.GetFileTimestamps(dirtyFiles);
                ParsedFilesTimestamp = ParsedFilesTimestamp.Concat(dirtyTimestamps).ToDictionary(x => x.Key, x => x.Value); //assumes there are no duplicate keys

                EntityTypes = DbxUtils.GetUniqueAssetTypes(Entities);
            }
            else
            {
                throw new Exception(String.Format("File does not exist \"{0}\"", SAVE_FILE));
            }
        }

        private List<FileInfo> GetDirtyFiles(FileInfo[] files, Dictionary<string, long> parsedFiles)
        {
            var output = new List<FileInfo>();
            foreach (var file in files)
            {
                var key = file.FullName;
                bool differentTimestamps = parsedFiles.ContainsKey(key) ? parsedFiles[key] == file.LastWriteTime.Ticks : false;
                if (differentTimestamps)
                {
                    output.Add(file);
                }
            }
            return output;
        }

        public void ParseAllFiles()
        {
            Entities.Clear();
            EntityTypes.Clear();
            ParsedFilesTimestamp.Clear();

            var files = DbxUtils.GetFiles(DbxUtils.RootPath);
            ParseAndAdd(files);
        }

        public void ParseAndAdd(FileInfo[] files)
        {
            var instances = GetInstancesFromFiles(files);

            Entities = DbxUtils.GetEntities(instances);
            EntityTypes = DbxUtils.GetUniqueAssetTypes(Entities);

            ParsedFilesTimestamp = DbxUtils.GetFileTimestamps(files);
            Save(Entities, ParsedFilesTimestamp);
        }

        private List<DbxUtils.AssetInstance> GetInstancesFromFiles(IEnumerable<FileInfo> files)
        {
            //in order to not wait for big files in the end, go through them at the beginning [optimal would probably be to let 50% of threads go from beginning and 50% go from end]
            var sorted = files.OrderByDescending(a => a.Length).ToList();
            var partitionsContent = DbxUtils.ParseFiles(sorted);
            var instances = DbxUtils.CreateInstances(partitionsContent);

            return instances;
        }

        private void Save(List<DbxUtils.AssetInstance> entities, Dictionary<string, long> fileTimestamps)
        {
            var path = SAVE_FILE;
            var save = new save_data()
            {
                Entities = entities,
                FileTimestamps = fileTimestamps,
            };

            var output = JsonConvert.SerializeObject(save);

            var file = new FileInfo(path);
            if (!file.Directory.Exists)
                file.Directory.Create();
            File.WriteAllText(path, output);
        }

    }
}
