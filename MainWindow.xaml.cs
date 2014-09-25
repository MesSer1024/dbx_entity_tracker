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
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string DBX_ROOT = @"E:\rep\ws\FutureData\Source";
        private string DDF_WSROOT = @"E:\rep\ws\tnt\code\ws";
        public MainWindow()
        {
            InitializeComponent();
            var lib = new LibMain();
            //var files = lib.GetFilesOfType(DBX_ROOT, "dbx");
            var ddfFiles = lib.GetFilesOfType(DDF_WSROOT, "ddf");

            foreach (var file in ddfFiles)
            {
                using (var sr = new StreamReader(file.FullName))
                {
                    List<string> entityNames = new List<string>();
                    int bracketCount = 0;
                    bool insideEntity = false;
                    while (!sr.EndOfStream)
                    {
                        var line = sr.ReadLine().Trim();
                        if (!insideEntity && line.StartsWith("entity "))
                        {
                            entityNames.Add(line.Substring("entity ".Length));
                            //insideEntity = true; //haven't tried if this works or creates any issues...
                        }
                        //else if (insideEntity)
                        //{
                        //    if (line.Contains('{'))
                        //        bracketCount++;
                        //    if (line.Contains('}'))
                        //        bracketCount--;
                        //    if (bracketCount == 0)
                        //        insideEntity = false;
                        //}
                    }
                    if (entityNames.Count > 0)
                    {
                        Console.WriteLine("-----------------\n Parsing: {0}\n Entities: {1}\n-----------------", file.FullName, entityNames.Count);
                        entityNames.ForEach(a => Console.WriteLine(a));
                    }
                }

                bool validGuid = asset.Guid.Length == DbxUtils.GUID_LENGTH;
                bool validPrimaryInstance = asset.PrimaryInstance.Length == DbxUtils.GUID_LENGTH;
                bool validAssetType = asset.Type.Length > 3;
                if (validGuid && validPrimaryInstance && validAssetType)
                {
                    validGuid = true;
                }
            }
            //try
            //{
            //    lib.PopulateAssets(files);
            //    var assets = lib.getAllAssets();
            //    foreach (var asset in assets)
            //    {
            //        if (asset.FilePath.EndsWith("eorpersonalstats.dbx"))
            //        {
            //            int foo = 2;
            //        }
            //        if (asset.Type.ToLower() == "wsshared.clientstatsentitydata")
            //        {
            //            int foo = 1;
            //        }
            //    }
            //}
            //catch (Exception e)
            //{
            //    Console.WriteLine(e);
            //}
        }
    }
}
