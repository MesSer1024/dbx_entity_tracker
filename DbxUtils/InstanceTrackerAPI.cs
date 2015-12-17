using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.InstanceTracker.InstanceTrackerEditor
{
    public static class InstanceTrackerAPI
    {
        public static void ClearLog()
        {
            Logger.ClearLogEntries();
        }

        public static String GetLog()
        {
            var sb = new StringBuilder();
            sb.AppendLine("For any issues, please send me an e-mail");
            sb.AppendLine(GetDevText());
            sb.AppendLine("--------------------------------");
            sb.AppendLine("User: " + Environment.UserName);
            sb.AppendLine("Domain: " + Environment.UserDomainName);
            sb.AppendFormat("Date: {0}\n", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("--------------------------------\n");
            return sb.ToString() + Logger.GetLog();
        }

        public static string GetLastVersionDate()
        {
            return "2015-12-17";
        }

        public static string GetLastVersion()
        {
            return "131";
        }

        private static string GetDevText()
        {
            var developer = "daniel.dahlkvist";
            var domain = "dice.se";
            ////////
            return String.Format("Author: {1}@{2}\nBuild date: {0}\nVersion: {3}", InstanceTrackerAPI.GetLastVersionDate(), developer, domain, InstanceTrackerAPI.GetLastVersion());
        }

        public static string GetAboutText()
        {
            var aboutText = new StringBuilder();
            aboutText.AppendLine(GetDevText());
            aboutText.AppendLine("");
            aboutText.AppendLine("For any issues please send me an email (and attach any text from the menu/view/debug output-window)");
            return aboutText.ToString();
        }
    }
}
