using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;

namespace DbxEntityTracker
{
    static class Utils
    {

        public static string NextAvailableFilename(string path)
        {
            string numberPattern = "_{0:000}";
            // Short-cut if already available
            if (!File.Exists(path))
                return path;

            // If path has extension then insert the number pattern just before the extension and return next filename
            if (Path.HasExtension(path))
                return GetNextFilename(path.Insert(path.LastIndexOf(Path.GetExtension(path)), numberPattern));

            // Otherwise just append the pattern to the path and return next filename
            return GetNextFilename(path + numberPattern);
        }

        private static string GetNextFilename(string pattern)
        {
            string tmp = string.Format(pattern, 1);
            if (tmp == pattern)
                throw new ArgumentException("The pattern must include an index place-holder", "pattern");

            if (!File.Exists(tmp))
                return tmp; // short-circuit if no matches

            int min = 1, max = 2; // min is inclusive, max is exclusive/untested

            while (File.Exists(string.Format(pattern, max)))
            {
                min = max;
                max *= 2;
            }

            while (max != min + 1)
            {
                int pivot = (max + min) / 2;
                if (File.Exists(string.Format(pattern, pivot)))
                    min = pivot;
                else
                    max = pivot;
            }

            return string.Format(pattern, max);
        }

        public static void AtomicWriteToLog(string data)
        {
            try
            {
                var fi = new FileInfo(AppSettings.LOG_FILE);
                if (!fi.Directory.Exists)
                    fi.Directory.Create();
                if (!fi.Exists)
                    File.Create(fi.FullName).Dispose();

                using (FileStream fs = new FileStream(fi.FullName, FileMode.Open, FileSystemRights.AppendData, FileShare.Write, 4096, FileOptions.None))
                {
                    using (StreamWriter writer = new StreamWriter(fs))
                    {
                        var computerName = Environment.MachineName;
                        writer.WriteLine("{2}|{1}|{0}", data, computerName, DateTime.Now);
                        writer.Flush();
                    }
                }
            }
            catch (Exception e) { }
        }
    }
}
