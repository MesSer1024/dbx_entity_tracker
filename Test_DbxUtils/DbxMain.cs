using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dice.Frostbite.Framework
{

    class DbxMain
    {
        private class DbxEntityTracker_data
        {
            public List<DbxUtils.AssetInstance> Entities { get; set; }
        }

        public List<DbxUtils.AssetInstance> Entities { get; set; }
        public List<string> EntityTypes { get; set; }

        public DbxMain()
        {

        }

        public void execute()
        {
            string save_file = "./saves/_lastsave.det";
            //Load(save_file);
            ParseAndSave();

            Console.ReadLine();
        }

        private void ParseAndSave()
        {
            string save_file = "./saves/_lastsave.det";
            ParseAllDbxFiles(DbxUtils.GetRootPath());
            Save(save_file);
        }

        private double GetMillisecondsSinceStart(DateTime start)
        {
            return (DateTime.Now - start).TotalMilliseconds;
        }

        private void ParseAllDbxFiles(string dbxRoot)
        {
            var start = DateTime.Now;

            var files = DbxUtils.GetFiles(dbxRoot);
            Console.WriteLine("---Found {0} dbx-Files from \"{2}\" --- time: {1}ms", files.Length, GetMillisecondsSinceStart(start), DbxUtils.GetRootPath());

            var sorted = files.OrderByDescending(a => a.Length).ToList();
            Console.WriteLine("---Sorted {0} Files by size --- time: {1}ms", files.Length, GetMillisecondsSinceStart(start));

            var partitions = DbxUtils.ParsePartitions(sorted, files.Length);
            Console.WriteLine("---Parsed {0} partitions from dbx-files --- time: {1}ms", partitions.Count, GetMillisecondsSinceStart(start));

            var instances = DbxUtils.CreateInstances(partitions);
            Console.WriteLine("---Found {0} instances given all partitions --- time: {1}ms", instances.Count, GetMillisecondsSinceStart(start));

            //Only save "entities" [due to OutOfMemoryException when dumping through JsonConvert...]
            Entities = instances.FindAll(a => a.AssetType.Contains("EntityData"));
            Console.WriteLine("---Found {0} entities --- time: {1}ms", Entities.Count, GetMillisecondsSinceStart(start));

            EntityTypes = DbxUtils.GetUniqueEntityTypes(Entities);
            Console.WriteLine("---Found {0} unique entity types --- time: {1}ms", EntityTypes.Count, GetMillisecondsSinceStart(start));
        }

        void Save(string path)
        {
            var save = new DbxEntityTracker_data()
            {
                Entities = Entities,
            };

            var output = JsonConvert.SerializeObject(save);

            var file = new FileInfo(path);
            if (!file.Directory.Exists)
                file.Directory.Create();
            File.WriteAllText(path, output);
        }

        void Load(string path)
        {
            var file = new FileInfo(path);
            if (file.Exists)
            {
                using (var sr = new StreamReader(file.FullName))
                {
                    string s = sr.ReadToEnd();
                    var load = JsonConvert.DeserializeObject<DbxEntityTracker_data>(s);
                    Entities = load.Entities;
                }

                EntityTypes = DbxUtils.GetUniqueEntityTypes(Entities);
            }
            else
            {
                throw new Exception(String.Format("File does not exist \"{0}\"", path));
            }
        }
    }
}
