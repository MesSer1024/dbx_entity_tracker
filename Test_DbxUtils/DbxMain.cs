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

        }
        private EntityDatabase _database;

        public DbxMain()
        {
            _database = new EntityDatabase();
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
                case Commands.Unknown:
                default:
                    Console.WriteLine("Unknown command, 'exit' to quit");
                    break;
            }
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

        private void Parse()
        {
            Console.WriteLine("Parse");
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
