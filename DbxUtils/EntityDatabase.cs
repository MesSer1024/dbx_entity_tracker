using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Extension.InstanceTracker.InstanceTrackerEditor
{
    public class EntityDatabase
    {
        #region Private Classes/Fields/Properties
        private class save_data
        {
            public List<DbxUtils.AssetInstance> Entities { get; set; }
            public Dictionary<string, long> FileTimestamps { get; set; }
        }

        private const string SAVE_FILE = "./instance_db/_lastSave_v0.et";
        private static string _root = "D:\\dice\\ws\\ws\\FutureData\\Source";
        /// <summary>
        /// (Dictionary key=file.FullName, value=file.LastWriteTime.Ticks)
        /// </summary>
        private Dictionary<string, long> FileTimestamps { get; set; }
        #endregion

        public DatabaseState State { get; private set; }
        public enum DatabaseState
        {
            Empty,
            ParsingInProgress,
            Populated,
        }

        public List<DbxUtils.AssetInstance> Entities { get; private set; }
        public List<string> EntityTypes { get; private set; }

        public static string RootPath
        {
            get { return _root; }
            set { _root = value; }
        }

        public Action<Extension.InstanceTracker.InstanceTrackerEditor.DbxUtils.ParsingProgress> ProgressCb { get; set; }
        public EntityDatabase()
        {
            ResetDatabase();
        }

        /// <summary>
        /// Wipe database completely, if followed by RefreshDatabase() ALL DBX-files will be parsed from scratch
        /// </summary>
        public void ResetDatabase()
        {
            Entities = new List<DbxUtils.AssetInstance>();
            EntityTypes = new List<string>();
            FileTimestamps = new Dictionary<string, long>();
            State = DatabaseState.Empty;
        }

        /// <summary>
        /// If database can be loaded (there exists a previous save/autosave)
        /// </summary>
        /// <returns></returns>
        public bool CanLoadDatabase()
        {
            return File.Exists(SAVE_FILE);
        }

        /// <summary>
        /// Loads a previously saved database, should be preceded by CanLoadDatabase to avoid Exception
        /// </summary>
        public void LoadDatabase(bool refreshDatabase = true)
        {
            State = DatabaseState.ParsingInProgress;
            if (CanLoadDatabase())
            {
                var s = File.ReadAllText(SAVE_FILE);
                var load = JsonConvert.DeserializeObject<save_data>(s);
                Entities = load.Entities;
                FileTimestamps = load.FileTimestamps;
                if (refreshDatabase)
                    RefreshDatabase(true);
                if (EntityTypes.Count == 0)
                    EntityTypes = DbxUtils.GetUniqueAssetTypes(Entities);
                State = DatabaseState.Populated;
            }
            else
            {
                throw new Exception(String.Format("Unable to load \"{0}\"", SAVE_FILE));
            }
        }

        /// <summary>
        /// Checks for modified dbx-files given "RootPath" and updates the database accordingly
        /// </summary>
        public void RefreshDatabase(bool save = true)
        {
            State = DatabaseState.ParsingInProgress;
            if (!Directory.Exists(RootPath))
                throw new Exception(String.Format("Folder \"{0}\" does not exist", RootPath));
            var allFiles = DbxUtils.GetFiles(RootPath);

            var deletedFiles = DbxUtils.GetDeletedFiles(allFiles, FileTimestamps);
            RemoveEntriesByFilePath(deletedFiles);

            var dirtyFiles = DbxUtils.GetDirtyFiles(allFiles, FileTimestamps);
            UpdateDirtyInstances(dirtyFiles);

            //save changes
            bool modified = dirtyFiles.Count > 0 || deletedFiles.Count > 0;
            if (save && modified)
                SaveDatabase(Entities, FileTimestamps);
            State = DatabaseState.Populated;
        }

        private void UpdateDirtyInstances(List<FileInfo> dirtyFiles)
        {
            if (dirtyFiles.Count == 0)
                return;

            //remove all entries referred to by a dirty file
            RemoveEntriesByFilePath(dirtyFiles);

            //add dirty entries to database
            var timestamps = DbxUtils.GetFileTimestamps(dirtyFiles);
            var partitions = DbxUtils.ParseDbxFiles(dirtyFiles, ProgressCb);
            var instances = DbxUtils.CreateInstances(partitions);

            Entities.AddRange(DbxUtils.GetEntities(instances));
            FileTimestamps = FileTimestamps.Concat(timestamps).ToDictionary(x => x.Key, x => x.Value); //assumes there are no duplicate keys

            EntityTypes = DbxUtils.GetUniqueAssetTypes(Entities);
        }

        private void RemoveEntriesByFilePath(List<FileInfo> files)
        {
            foreach (var file in files)
            {
                Entities.RemoveAll(a => a.PartitionPath == file.FullName);
                FileTimestamps.Remove(file.FullName);
            }
        }

        /// <summary>
        /// Saves what is currently inside database, RefreshData uses autosave by default
        /// </summary>
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

            if (file.Exists && file.IsReadOnly)
            {
                showSaveError(String.Format("Your file {0} is write-protected", file.FullName));
                return;
            }

            if (file.Directory.Exists && file.Directory.Attributes == FileAttributes.ReadOnly)
            {
                showSaveError(String.Format("Your directory {0} is write-protected", file.Directory.FullName));
                return;
            }

            try
            {
                if (!file.Directory.Exists)
                    file.Directory.Create();

                File.WriteAllText(path, output);
            }
            catch (Exception e)
            {
                showSaveError(e.Message);
            }
        }

        private void showSaveError(string msg)
        {
            //MessageBox.Show(msg);
            Console.WriteLine(msg);
        }

    }
}
