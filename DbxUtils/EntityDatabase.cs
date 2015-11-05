using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dice.Frostbite.Framework
{
    public class EntityDatabase
    {
        private class save_data
        {
            public List<DbxUtils.AssetInstance> Entities { get; set; }
            public Dictionary<string, long> FileTimestamps { get; set; }
        }

        private const string SAVE_FILE = "./state/instance_db/_lastSave_v0.et";
        private Dictionary<string, long> FileTimestamps { get; set; }

        public List<DbxUtils.AssetInstance> Entities { get; private set; }
        public List<string> EntityTypes { get; private set; }

        private static string _root = "D:\\dice\\ws\\ws\\FutureData\\Source";
        public static string RootPath
        {
            get { return _root; }
            set { _root = value; }
        }


        public EntityDatabase()
        {
            ResetDatabase();
        }

        public void ResetDatabase()
        {
            Entities = new List<DbxUtils.AssetInstance>();
            EntityTypes = new List<string>();
            FileTimestamps = new Dictionary<string, long>();
        }

        public bool CanLoadDatabase()
        {
            return File.Exists(SAVE_FILE);
        }

        public void LoadDatabase()
        {
            if (CanLoadDatabase())
            {
                var s = File.ReadAllText(SAVE_FILE);
                var load = JsonConvert.DeserializeObject<save_data>(s);
                Entities = load.Entities;
                FileTimestamps = load.FileTimestamps;

                RefreshDatabase();
            }
            else
            {
                throw new Exception(String.Format("Unable to load \"{0}\"", SAVE_FILE));
            }
        }

        public void RefreshDatabase()
        {
            var allFiles = DbxUtils.GetFiles(RootPath);
            var dirtyFiles = DbxUtils.GetDirtyFiles(allFiles, FileTimestamps);
            if (dirtyFiles.Count > 0)
            {
                UpdateDirtyInstances(dirtyFiles);
                SaveDatabase(Entities, FileTimestamps);
            }
        }

        public void SaveDatabase()
        {
            SaveDatabase(Entities, FileTimestamps);
        }

        private void SaveDatabase(List<DbxUtils.AssetInstance> entities, Dictionary<string, long> fileTimestamps)
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

        private void UpdateDirtyInstances(IEnumerable<FileInfo> dirtyFiles)
        {
            var timestamps = DbxUtils.GetFileTimestamps(dirtyFiles);
            var partitions = DbxUtils.ParseFiles(dirtyFiles);
            var instances = DbxUtils.CreateInstances(partitions);

            //remove all entries referred to by a dirty file
            bool notEmpty = Entities.Count > 0 || FileTimestamps.Count > 0;
            if (notEmpty)
            {
                foreach (var file in dirtyFiles)
                {
                    Entities.RemoveAll(a => a.PartitionPath == file.FullName);
                    FileTimestamps.Remove(file.FullName);
                }
            }

            //add dirty entries to database
            Entities.AddRange(DbxUtils.GetEntities(instances));
            FileTimestamps = FileTimestamps.Concat(timestamps).ToDictionary(x => x.Key, x => x.Value); //assumes there are no duplicate keys
            EntityTypes = DbxUtils.GetUniqueAssetTypes(Entities);
        }

    }
}
