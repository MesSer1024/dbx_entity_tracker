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
            //Load(save_file);
            ParseAndSave();

            Console.ReadLine();
        }

        private void ParseAndSave()
        {
            ParseAllDbxFiles(EntityDatabase.RootPath);
        }

        private double GetMillisecondsSinceStart(DateTime start)
        {
            return (DateTime.Now - start).TotalMilliseconds;
        }

        private void ParseAllDbxFiles(string dbxRoot)
        {
            var start = DateTime.Now;

            var files = DbxUtils.GetFiles(dbxRoot);
            Console.WriteLine("---Found {0} dbx-Files from \"{2}\" --- time: {1}ms", files.Length, GetMillisecondsSinceStart(start), EntityDatabase.RootPath);

            var sorted = files.OrderByDescending(a => a.Length).ToList();
            Console.WriteLine("---Sorted {0} Files by size --- time: {1}ms", files.Length, GetMillisecondsSinceStart(start));

            var partitions = DbxUtils.ParseFiles(sorted);
            Console.WriteLine("---Parsed {0} partitions from dbx-files --- time: {1}ms", partitions.Count, GetMillisecondsSinceStart(start));

            var instances = DbxUtils.CreateInstances(partitions);
            Console.WriteLine("---Found {0} instances given all partitions --- time: {1}ms", instances.Count, GetMillisecondsSinceStart(start));

            //Only save "entities" [due to OutOfMemoryException when dumping through JsonConvert...]
            Entities = DbxUtils.GetEntities(instances);
            Console.WriteLine("---Found {0} entities --- time: {1}ms", Entities.Count, GetMillisecondsSinceStart(start));

            EntityTypes = DbxUtils.GetUniqueAssetTypes(Entities);
            Console.WriteLine("---Found {0} unique entity types --- time: {1}ms", EntityTypes.Count, GetMillisecondsSinceStart(start));
        }
    }
}
