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
        #region Private Utility-stuff
        private class SaveData
        {
            public List<DbxUtils.AssetInstance> Entities { get; set; }
            public Dictionary<string, long> FileTimestamps { get; set; }
        }

        private static int s_version = 0;
        private static string s_stateFolder = "D:\\dice\\ws\\ws\\FutureData\\.state\\whiteshark";
        private static string s_dbxFolder = "D:\\dice\\ws\\ws\\FutureData\\Source";
        /// <summary>
        /// (Dictionary key=file.FullName, value=file.LastWriteTime.Ticks)
        /// </summary>
        private Dictionary<string, long> FileTimestamps { get; set; }

        private static string GetSaveFilePath(string folder, int saveVersion)
        {
            var fileName = string.Format("instanceTracker_v{0}", saveVersion);
            var path = Path.Combine(folder, fileName);
            return path;
        }
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

        public static string DbxRootFolder
        {
            get { return s_dbxFolder; }
            set { s_dbxFolder = value; }
        }

        public static string StateRootFolder
        {
            get { return s_stateFolder; }
            set { s_stateFolder = value; }
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
            Logger.Info(Action.Reset);
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
            return File.Exists(GetSaveFilePath(StateRootFolder, s_version));
        }

        /// <summary>
        /// Loads a previously saved database, should be preceded by CanLoadDatabase to avoid error
        /// </summary>
        public void LoadDatabase(bool refreshDatabase = true)
        {
            Logger.Info(Action.Load, String.Format("[START] refresh={0}", refreshDatabase));
            State = DatabaseState.ParsingInProgress;
            var path = GetSaveFilePath(StateRootFolder, s_version);
            try
            {
                var s = File.ReadAllText(path);
                var load = JsonConvert.DeserializeObject<SaveData>(s);
                Entities = load.Entities;
                FileTimestamps = load.FileTimestamps;
                if (refreshDatabase)
                    RefreshDatabase(true);
                if (EntityTypes.Count == 0)
                    EntityTypes = DbxUtils.GetUniqueAssetTypes(Entities);
                State = DatabaseState.Populated;
            }
            catch (Exception e)
            {
                State = DatabaseState.Empty;
                ShowError(String.Format("Unable to load \"{0}\" is file corrupt? (Delete the file and open entity tracker-window again)\n\n Exception:\n{1}", path, e.Message));
            }
            Logger.Info(Action.Load, String.Format("[END] Entities={0}, Types={1}, FileTimeStamps={2}", Entities.Count, EntityTypes.Count, FileTimestamps.Count));
        }

        /// <summary>
        /// Checks for modified dbx-files given "RootPath" and updates the database accordingly
        /// </summary>
        public void RefreshDatabase(bool save = true)
        {
            Logger.Info(Action.Refresh, String.Format("[START] save={0}", save));
            State = DatabaseState.ParsingInProgress;
            if (!Directory.Exists(DbxRootFolder))
            {
                ShowError(String.Format("Folder \"{0}\" does not exist, this is the root path given by FrostEd", DbxRootFolder));
                return;
            }
            
            var allFiles = DbxUtils.GetFiles(DbxRootFolder);
            Logger.Info(Action.Refresh, String.Format("all files={0}", allFiles.Length));
            var deletedFiles = DbxUtils.GetDeletedFiles(allFiles, FileTimestamps);
            Logger.Info(Action.Refresh, String.Format("deleted files={0}", deletedFiles.Count));
            RemoveEntriesByFilePath(deletedFiles);

            var dirtyFiles = DbxUtils.GetDirtyFiles(allFiles, FileTimestamps);
            Logger.Info(Action.Refresh, String.Format("dirty files={0}", dirtyFiles.Count));
            UpdateDirtyInstances(dirtyFiles);

            //save changes
            bool modified = dirtyFiles.Count > 0 || deletedFiles.Count > 0;
            if (save && modified)
                SaveDatabase(Entities, FileTimestamps);
            State = DatabaseState.Populated;
            Logger.Info(Action.Refresh, String.Format("[END] Entities={0}, Types={1}, FileTimestamps={2}", Entities.Count, EntityTypes.Count, FileTimestamps.Count));
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
            if (files.Count == 0)
                return;
            Logger.Info(Action.Remove, String.Format("[BEGIN] files={0}, Entities={1}, FileTimestamps={2}", files.Count, Entities.Count, FileTimestamps.Count));
            var sw = new System.Diagnostics.Stopwatch();
            sw.Start();

            var lookup = files.ToDictionary(a => a.FullName);
            var newEntities = new List<DbxUtils.AssetInstance>();
            foreach (var entity in Entities)
            {
                if (!lookup.ContainsKey(entity.PartitionPath))
                    newEntities.Add(entity);
            }

            Entities = newEntities;
            files.ForEach(a => FileTimestamps.Remove(a.FullName));

            sw.Stop();
            Logger.Info(Action.Remove, String.Format("[END] Entities={0}, FileTimestamps={1}, time={2}ms", Entities.Count, FileTimestamps.Count, sw.ElapsedMilliseconds));
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
            var path = GetSaveFilePath(StateRootFolder, s_version);
            Logger.Info(Action.Save, path);
            var save = new SaveData()
            {
                Entities = entities,
                FileTimestamps = fileTimestamps,
            };

            var output = JsonConvert.SerializeObject(save);

            var file = new FileInfo(path);

            if (file.Exists && file.IsReadOnly)
            {
                ShowError(String.Format("Your file {0} is write-protected", file.FullName));
                return;
            }

            if (file.Directory.Exists && file.Directory.Attributes == FileAttributes.ReadOnly)
            {
                ShowError(String.Format("Your directory {0} is write-protected", file.Directory.FullName));
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
                ShowError(String.Format("Unable to write to file {0}, error\n:{1}", file.FullName, e.Message));
            }
        }

        private void ShowError(string msg)
        {
            Logger.Error(msg);
            Console.WriteLine(msg);
            //MessageBox.Show(msg);
        }

    }
}
