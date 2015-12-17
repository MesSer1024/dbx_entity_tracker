using Extension.InstanceTracker.InstanceTrackerEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dice.Frostbite.Framework
{
    class DbxMain
    {
        enum Commands
        {
            Reset,
            Load,
            Parse,
            Find,
            List,
            Info,
            Unknown,
            Exit,
            Test,
            Log,
        }
        private EntityDatabase _database;
        private Writer _writer;

        public DbxMain()
        {
            _database = new EntityDatabase();
            _database.ProgressCb = onProgress;
        }

        private void onProgress(DbxUtils.ParsingProgress obj)
        {
            _writer.Progress(String.Format("Parsing files {0}/{1}", obj.ParsedFiles.Count, obj.AllFiles.Count));
        }

        public void execute()
        {
            var input = "";
            var cmd = Commands.Unknown;
            do
            {
                input = Console.ReadLine();
                var args = input.Split(' ');
                cmd = GetCommand(args);
                RunCommand(cmd, args);
            } while (cmd != Commands.Exit);
        }

        private Commands GetCommand(string[] input)
        {
            var s = input.Length > 0 ? input[0] : "";
            int i = 0;
            foreach (var item in Enum.GetNames(typeof(Commands)))
            {
                if (s.IndexOf(item, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return (Commands)i;
                }
                i++;
            }
            return Commands.Unknown;
        }

        private void RunCommand(Commands cmd, string[] args)
        {
            switch (cmd)
            {
                case Commands.Load:
                    Load();
                    break;
                case Commands.Parse:
                    Parse();
                    break;
                case Commands.Reset:
                    reset();
                    break;
                case Commands.Find:
                    find(args);
                    break;
                case Commands.List:
                    List(args);
                    break;
                case Commands.Info:
                    WriteInfo(DateTime.Now);
                    break;
                case Commands.Test:
                    test();
                    break;
                case Commands.Log:
                    Console.WriteLine(InstanceTrackerAPI.GetLog());
                    break;
                case Commands.Unknown:
                default:
                    Console.WriteLine("Unknown command, 'exit' to quit");
                    break;
            }
        }

        private void test()
        {
            _database.SaveDatabase();
        }

        private void List(string[] args)
        {
            var start = DateTime.Now;
            Console.WriteLine("List");
            var item = find(args);
            var matches = _database.Entities.FindAll(a => a.AssetType.Contains(item));
            var sb = new StringBuilder();
            foreach (var asset in matches)
            {
                sb.Append(DbxUtils.GetAssetDescription(asset));
            }
            Console.WriteLine(sb.ToString());
            Console.WriteLine("List completed in {0:0}ms", (DateTime.Now - start).TotalMilliseconds);
        }

        private string find(string[] args)
        {
            Console.WriteLine("Find");
            var wordFilters = args.ToList();
            wordFilters.RemoveAt(0);
            //only fill items where all words exists
            var items = from item in _database.EntityTypes where wordFilters.All(a => item.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0) select item;
            var sb = new StringBuilder();
            items.All(a => { sb.AppendLine(a); return false; });
            Console.WriteLine(sb.ToString());
            return items.FirstOrDefault();
        }

        private class Writer
        {
            private StringBuilder _errors;
            private StringBuilder _idleTime;
            private StringBuilder _progress;

            private Dictionary<StringBuilder, int> _rowOffsets;

            public void begin()
            {
                _errors = new StringBuilder();
                _idleTime = new StringBuilder();
                _progress = new StringBuilder();

                _rowOffsets = new Dictionary<StringBuilder, int>();
                _rowOffsets.Add(_progress, Console.CursorTop);
                Console.WriteLine(_progress.ToString());

                _rowOffsets.Add(_idleTime, Console.CursorTop);
                Console.WriteLine(_idleTime.ToString());

                _rowOffsets.Add(_errors, Console.CursorTop);
                Console.WriteLine(_errors.ToString());
            }

            public void Error(string s)
            {
                _errors.AppendLine(s);
                write();
            }

            public string GetIdleTime()
            {
                return String.Format("User has been idle for: {0:000,000}ms\n", checkIdleTime());
            }

            public void Progress(string s)
            {
                _progress.Clear();
                _progress.AppendLine(s);
                write();
            }

            private uint checkIdleTime()
            {
                return 0;
                //return Win32API.GetIdleTime();
            }

            private void write()
            {
                Console.SetCursorPosition(0, _rowOffsets[_progress]);
                Console.WriteLine(_progress.ToString());

                Console.SetCursorPosition(0, _rowOffsets[_idleTime]);
                Console.WriteLine(GetIdleTime());

                Console.SetCursorPosition(0, _rowOffsets[_errors]);
                Console.WriteLine(_errors.ToString());
            }
        }

        private void Parse()
        {
            Console.WriteLine("Parse");
            _writer = new Writer();
            _writer.begin();
            var start = DateTime.Now;
            _database.RefreshDatabase();
            WriteInfo(start);
        }

        private void Load()
        {
            Console.WriteLine("Load");
            reset();
            var start = DateTime.Now;
            bool refresh = true;
            string actionText = "";
            switch (_database.State)
            {
                case EntityDatabase.DatabaseState.Empty:
                    if (_database.CanLoadDatabase())
                    {
                        refresh = false;
                        actionText = "Loading database -- Estimated time <10s";
                    }
                    else
                    {
                        refresh = true;
                        actionText = "Parsing all DBX-files -- Estimated time ~2min";
                    }
                    break;
            }

            Console.WriteLine(actionText);
            if (refresh)
                _database.RefreshDatabase();
            else
                _database.LoadDatabase();

            Console.WriteLine("Database actions complete, total time= {0:0}ms", (DateTime.Now - start).TotalMilliseconds);
            WriteInfo(start);
        }

        private void WriteInfo(DateTime start)
        {
            var foo = (from a in _database.Entities select a.PartitionPath).Distinct().ToList();
            Console.WriteLine("Database contains {0} entities from {1} dbx-files", _database.Entities.Count, foo.Count);
            Console.WriteLine("END, total time= {0:0}ms", (DateTime.Now - start).TotalMilliseconds);
        }

        private void reset()
        {
            _database.ResetDatabase();
            Console.WriteLine("Reset");
        }


    }
}
