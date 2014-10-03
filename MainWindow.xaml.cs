using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using dbx_lib;
using System.Collections.Concurrent;

namespace DbxEntityTracker
{
    class DbxMatches
    {
        public string Filepath { get; set; }
        public List<int> LineNumbers { get; private set; }
        public List<string> EntityType { get; private set; }

        public DbxMatches()
        {
            LineNumbers = new List<int>();
            EntityType = new List<string>();
            Filepath = "";
        }
    }


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string DBX_ROOT = @"E:\rep\ws\FutureData\Source\";
        private string DDF_WSROOT = @"E:\rep\ws\tnt\code\ws";
        
        public MainWindow()
        {
            InitializeComponent();
            var lib = new LibMain();
            var startTime = DateTime.Now;

            var ddfFiles = lib.GetFilesOfType(DDF_WSROOT, "ddf");
            var allEntities = populateDDF(ddfFiles);
            var ddfTime = DateTime.Now;
            Console.WriteLine("-----------------\n Total Entities: {0}\n-----------------", allEntities.Keys.Count);

            var dbxFiles = lib.GetFilesOfType(DBX_ROOT, "dbx");
            var dbxMatches = populateDBX(dbxFiles);
            var dbxTime = DateTime.Now;
            Console.WriteLine("Parsing all dbx-files, found {0} files that might contain entities", dbxMatches.Count);

            var entityUsage = crossReferenceEntitiesWithDbxMatches(allEntities, dbxMatches);
            var mappingTime = DateTime.Now;

            Console.WriteLine("Time to populate ddf files: {0}ms", (ddfTime - startTime).TotalMilliseconds);
            Console.WriteLine("Time to populate dbx files: {0}ms", (dbxTime - ddfTime).TotalMilliseconds);
            Console.WriteLine("Time to map dbx files to entities: {0}ms", (mappingTime - dbxTime).TotalMilliseconds);

            var unusedEntities = findUnusedEntities(allEntities, entityUsage);
            writeUnusedEntities(unusedEntities);
            int foo = 0;
        }

        private IDictionary<string, string> populateDDF(FileInfo[] ddfFiles)
        {
            var entityTable = new ConcurrentDictionary<string, string>();

            Parallel.ForEach(ddfFiles, (file) =>
            {
                using (var sr = new StreamReader(file.FullName))
                {
                    int bracketCount = 0;
                    bool insideEntity = false;
                    while (!sr.EndOfStream)
                    {
                        var line = sr.ReadLine().Trim();
                        if (!insideEntity && line.StartsWith("entity "))
                        {
                            var name = line.Substring("entity ".Length);
                            name = name.Split(':')[0].Trim();
                            var fakename = "WSShared." + name + "Data"; //Append something
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
            });

            return new Dictionary<string, string>(entityTable);
        }

        private List<DbxMatches> populateDBX(FileInfo[] dbxFiles)
        {
            var allCollections = new List<DbxMatches>();
            var mutex = new object();
            Parallel.ForEach(dbxFiles, (file) =>
            {
                var collection = new List<DbxMatches>();
                using (var sr = new StreamReader(file.FullName))
                {
                    var asset = new DbxMatches();
                    asset.Filepath = file.FullName;
                    int lineNumber = 0;
                    bool valid = false;
                    while (!sr.EndOfStream)
                    {
                        var line = sr.ReadLine();
                        lineNumber++;
                        if (line.Contains("type=\""))
                        {
                            var assetType = DbxUtils.findSubstring(line, "type=\"", "\"");
                            asset.LineNumbers.Add(lineNumber);
                            asset.EntityType.Add(assetType);
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
            });
            return allCollections;
        }

        private IDictionary<string, List<string>> crossReferenceEntitiesWithDbxMatches(IDictionary<string, string> allEntities, List<DbxMatches> dbxMatches)
        {
            var entityUsage = new Dictionary<string, List<string>>(); //Dictionary<EntityReference, DbxFile>
            foreach (var entity in dbxMatches)
            {
                foreach (var t in entity.EntityType)
                {
                    if (allEntities.Keys.Contains(t))
                    {
                        if (entityUsage.ContainsKey(t))
                            entityUsage[t].Add(entity.Filepath);
                        else
                            entityUsage.Add(t, new List<string>() { entity.Filepath });
                    }
                }
            }

            return entityUsage;
        }

        private static List<string> findUnusedEntities(IDictionary<string, string> allEntities, IDictionary<string, List<string>> entityUsage)
        {
            var unusedEntities = new List<string>();
            foreach (var e in allEntities)
            {
                if (!entityUsage.ContainsKey(e.Key))
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

            using (var sw = new StreamWriter("../../_docs/unused_entities.txt", false))
            {
                sw.Write(sb.ToString());
                sw.Flush();
                sw.Close();
            }
        }
    }
}
