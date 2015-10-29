using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace fb_dbx
{
    class DbxMain
    {
        public List<DbxUtils.PartitionData> AllPartitions { get; set; }
        public List<DbxUtils.AssetInstance> AllInstances { get; set; }
        public List<string> AllEntities { get; set; }


        public DbxMain()
        {

        }

        public void execute()
        {
            var start = DateTime.Now;
            var files = DbxUtils.GetFiles(DbxUtils.GetRootPath());
            var parsedPartitions = new List<DbxUtils.PartitionData>();
            Console.WriteLine("---Parsing {0} Files--- total: {1}ms", files.Length, TimestampMs(start));
            int i = 0;
            foreach (var file in files)
            {
                if (file.Exists)
                {
                    var items = DbxUtils.FindInstances(file);
                    if (items.Count > 0)
                    {
                        parsedPartitions.AddRange(items);
                    }
                }
                if (i++ % 500 == 99)
                {
                    Console.WriteLine("{0} Files parsed", i);
                }
            }

            AllPartitions = parsedPartitions;
            Console.WriteLine("---Creating {0} instances--- total: {1}ms", parsedPartitions.Count, TimestampMs(start));
            var instances = DbxUtils.CreateInstances(parsedPartitions);
            AllInstances = instances;

            Console.WriteLine("---Filtering entities out of {0} instances--- total: {1}ms", instances.Count, TimestampMs(start));
            AllEntities = DbxUtils.FilterEntities(instances);

            Console.WriteLine("---Found {0} entities total: {1}ms", AllEntities.Count, TimestampMs(start));


            Console.ReadLine();
        }

        double TimestampMs(DateTime start)
        {
            return (DateTime.Now - start).TotalMilliseconds;
        }
    }
}
