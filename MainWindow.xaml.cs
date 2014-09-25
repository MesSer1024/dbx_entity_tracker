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
        private string DBX_ROOT = @"E:\rep\ws\FutureData\Source\Gameplay";
        private string DDF_WSROOT = @"E:\rep\ws\tnt\code\ws";
        public MainWindow()
        {
            InitializeComponent();
            var lib = new LibMain();
            var allEntities = populateDDF(ref lib);
            // need to generate "qualified name of entity" i.e. convert CameraDistanceEntity --> WSShared.CameraDistanceEntityData
            // WSSoldierHealthComponent --> WSShared.WSSoldierHealthComponentData 
            Console.WriteLine("-----------------\n Total Entities: {0}\n-----------------", allEntities.Keys.Count);

            var dbxFilesWithEntities = populateDBX(ref lib);
            Console.WriteLine("Parsing all dbx-files, found {0} files that might contain entities", dbxFilesWithEntities.Count);

            var entityUsage = new Dictionary<string, List<string>>(); //Dictionary<EntityReference, DbxFile>
            foreach (var entity in dbxFilesWithEntities)
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
        }

        private Dictionary<string, string> populateDDF(ref dbx_lib.LibMain lib)
        {
            var ddfFiles = lib.GetFilesOfType(DDF_WSROOT, "ddf");

            var entityTable = new Dictionary<string, string>();

            foreach (var file in ddfFiles)
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
                            var fakename = "WSShared." + name + "Data"; //Append something
                            entityTable.Add(fakename, file.FullName);
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
            }

            return entityTable;
        }

        private List<DbxMatches> populateDBX(ref dbx_lib.LibMain lib)
        {
            var collection = new List<DbxMatches>();

            var dbxFiles = lib.GetFilesOfType(DBX_ROOT, "dbx");
            foreach (var file in dbxFiles)
            {
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
            }

            return collection;
        }
    }
}
