using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.InstanceTracker.InstanceTrackerEditor
{
    enum Action
    {
        Reset,
        Load,
        Refresh,
        Remove,
        Save,
        Error,
        SelectItem,
    }

    internal static class Logger
    {
        private class Data
        {
            public Action UserAction { get; private set; }
            public string Description { get; private set; }
            public long TimeStamp { get; private set; }

            public Data(Action action, string msg)
            {
                UserAction = action;
                Description = msg;
                TimeStamp = DateTime.Now.Ticks;
            }
        }

        private static List<Data> s_logEntries = new List<Data>();
        public static int GetLogEntriesCount()
        {
            return s_logEntries.Count;
        }
        
        internal static StringBuilder GetLog()
        {
            var sb = new StringBuilder();
            foreach (var entry in s_logEntries)
            {
                sb.AppendLine(LogEntryToString(entry));
            }
            return sb;
        }

        private static string LogEntryToString(Data entry)
        {
            string action = "";
            switch(entry.UserAction)
            {
                case Action.Error:
                    action = "[ERROR]";
                    break;
                case Action.Load:
                    action = "load";
                    break;
                case Action.Refresh:
                    action = "refresh";
                    break;
                case Action.Remove:
                    action = "remove";
                    break;
                case Action.Reset:
                    action = "reset";
                    break;
                case Action.Save:
                    action = "save";
                    break;
                case Action.SelectItem:
                    action = "frosted";
                    break;
                default:
                    action = "???";
                    break;
            }
            return String.Format("Time: {0}\t{1}\t{2}", SwedishTimeFormat(entry.TimeStamp), action, entry.Description);
        }

        private static string SwedishTimeFormat(long ticks)
        {
            return new DateTime(ticks).ToString("HH:mm:ss");
        }
   
        internal static void Info(Action action)
        {
            Push(action, "");
        }

        internal static void Info(Action action, string msg)
        {
            Push(action, msg);
        }

        internal static void Error(string msg)
        {
            Push(Action.Error, msg);
        }

        private static void Push(Action action, string msg = "")
        {
            s_logEntries.Add(new Data(action, msg));
        }

        internal static void ClearLogEntries()
        {
            s_logEntries.Clear();
        }
    }

}
