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
        public static string APP_NAME = "dbx_entity_tracker";

        public static string LOG_FILE = Path.Combine(Environment.CurrentDirectory, String.Format("./output/user_{0}.txt", Environment.MachineName));
        public static string APP_ROOT_FOLDER = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), APP_NAME);
        public static string APP_SAVE_FOLDER = Path.Combine(APP_ROOT_FOLDER, "./saves/");
    }
}
