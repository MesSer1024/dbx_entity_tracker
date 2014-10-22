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
        public static string DBX_ROOT = @"E:\rep\ws\FutureData\Source\";
        public static string DDF_WSROOT = @"E:\rep\ws\tnt\code\ws";
        public static string ENTITY_SUFFIX = "Data";

        internal static void load()
        {
            var file = new FileInfo("./settings/config.ini");
            if (file.Exists)
            {
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
                    }
                }
            }
        }

        internal static void save()
        {
            var file = new FileInfo("./settings/config.ini");
            if(!file.Directory.Exists)
                file.Directory.Create();
            using (var sw = new StreamWriter(file.FullName, false))
            {
                sw.WriteLine("dbx|" + DBX_ROOT);
                sw.WriteLine("ddf|" + DDF_WSROOT);
                sw.WriteLine("suffix|" + ENTITY_SUFFIX);
                sw.Flush();
                sw.Close();
            }
        }
    }
}
