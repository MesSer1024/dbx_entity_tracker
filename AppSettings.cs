using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbxEntityTracker
{
    static class AppSettings
    {
        public static string DBX_ROOT = @"d:\dice\ws\Data\Source\";
        public static string DDF_WSROOT = @"d:\dice\ws\tnt\code\";
        public static string ENTITY_SUFFIX = "Data";
        //public static string DATABASE = "Whiteshark";
        public static string APP_NAME = "dbx_entity_tracker";
        public static bool DDF_SEARCH_ENABLED = true;

        public static string LOG_FILE = Path.Combine(Environment.CurrentDirectory, String.Format("./output/user_{0}.txt", Environment.MachineName));
        

        
        public static string APP_ROOT_FOLDER = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), APP_NAME);
        public static string APP_SAVE_FOLDER = Path.Combine(APP_ROOT_FOLDER, "./saves/");
        private static string CONFIG_FILE = Path.Combine(APP_ROOT_FOLDER, "./settings/config.ini");

        internal static void loadSettings()
        {
            try
            {
                if (!File.Exists(LOG_FILE))
                {
                    var fi = new FileInfo(LOG_FILE);
                    if (!fi.Directory.Exists)
                        fi.Directory.Create();
                    File.Create(fi.FullName).Dispose();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to create file...");
            }

            var file = new FileInfo(CONFIG_FILE);
            if (!file.Exists)
            {
                saveSettings();
            } else {
                using (var sr = new StreamReader(file.FullName))
                {
                    while (!sr.EndOfStream)
                    {
                        string line = sr.ReadLine();
                        var parts = line.Split('|');
                        if(parts.Length != 2) {
                            Console.WriteLine("Unknown line in config.ini: {0}", line);
                            continue;
                        }

                        var value = parts[1].Trim();
                        if (line.StartsWith("dbx|"))
                        {
                            DBX_ROOT = value;
                        }
                        else if (line.StartsWith("ddf|"))
                        {
                            DDF_WSROOT = value;
                        }
                        else if (line.StartsWith("suffix|"))
                        {
                            ENTITY_SUFFIX = value;
                        }
                        else if (line.StartsWith("ddf_enabled|"))
                        {
                            bool foo = true;
                            var ok = Boolean.TryParse(value, out foo);
                            DDF_SEARCH_ENABLED = foo;
                        }
                        //else if (line.StartsWith("database|"))
                        //{
                        //    DATABASE = value;
                        //}
                    }
                }
            }
        }

        internal static void saveSettings()
        {
            var file = new FileInfo(CONFIG_FILE);
            if(!file.Directory.Exists)
                file.Directory.Create();
            using (var sw = new StreamWriter(file.FullName, false))
            {
                sw.WriteLine("dbx|" + DBX_ROOT);
                sw.WriteLine("ddf|" + DDF_WSROOT);
                sw.WriteLine("suffix|" + ENTITY_SUFFIX);
                sw.WriteLine("ddf_enabled|" + DDF_SEARCH_ENABLED.ToString());
                //sw.WriteLine("database|" + DATABASE);
                sw.Flush();
                sw.Close();
            }
        }
    }
}
