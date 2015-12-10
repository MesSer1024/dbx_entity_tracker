using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Extension.InstanceTracker.InstanceTrackerEditor
{
    internal class DatabaseData
    {
        public DatabaseData()
        {
            Assets = new List<DbxUtils.AssetInstance>();
            FileTimestamps = new Dictionary<string, long>();
        }
        public List<DbxUtils.AssetInstance> Assets { get; private set; }
        public Dictionary<string, long> FileTimestamps { get; private set; }
    }

    internal static class SQLiteUtils
    {
        private const string SAVE_FILE = "./instance_db/InstanceTracker.sqlite";

        public static bool CanLoadDatabase()
        {
            return File.Exists(SAVE_FILE);
        }

        private static SQLiteConnection GetConnection()
        {
            return new SQLiteConnection(String.Format("Data Source={0};Version=3;", SAVE_FILE));
        }

        internal static DatabaseData Load()
        {
            var data = new DatabaseData();

            using (var db = GetConnection())
            {
                db.Open();
                using (var cmd = new SQLiteCommand(db))
                {
                    //file timestamps [filestamps]
                    string sql = "select * from filestamps";
                    SQLiteCommand command = new SQLiteCommand(sql, db);
                    using (SQLiteDataReader reader = command.ExecuteReader())
                    {
                        string filePath = "";
                        long modified = 0;

                        while (reader.Read())
                        {
                            //FilePath, LastModified
                            filePath = reader.GetString(0);
                            modified = reader.GetInt64(1);

                            if (String.IsNullOrEmpty(filePath))
                                throw new Exception();
                            data.FileTimestamps.Add(filePath, modified);
                        }
                    }

                    //entities [assets]
                    command.CommandText = "select * from assets order by AssetType";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            //(AssetGuid, AssetType, PartitionGuid, PartitionPath, PartitionName);
                            var asset = new DbxUtils.AssetInstance()
                            {
                                AssetGuid = reader.GetString(0),
                                AssetType = reader.GetString(1),
                                PartitionGuid = reader.GetString(2),
                                PartitionPath = reader.GetString(3)
                            };
                            data.Assets.Add(asset);
                        }
                    }
                }
                db.Close();
            }

            return data;
        }

        internal static int RemoveItems(List<FileInfo> files)
        {
            if (files.Count == 0)
                return 0;

            int count = 0;
            using (var db = GetConnection())
            {
                db.Open();
                var sqlFilestamps = "delete from filestamps where FilePath IN '{0}'";
                var sqlEntities = "delete from assets where PartitionPath='{0}'";
                var allFilesString = new StringBuilder();
                var sb = new StringBuilder();
                files.ForEach(a => { sb.Append(a.FullName + ','); });
                var foo = sb.Remove(sb.Length - 1, 1);
                using (var cmd = new SQLiteCommand(db))
                {
                    using (var transaction = db.BeginTransaction())
                    {
                        cmd.CommandText = String.Format(sqlFilestamps, sb.ToString());
                        count += cmd.ExecuteNonQuery();
                        cmd.CommandText = String.Format(sqlEntities, sb.ToString());
                        count += cmd.ExecuteNonQuery();

                        transaction.Commit();
                    }
                }
                db.Close();
            }
            return count;
        }

        internal static int Save(List<DbxUtils.AssetInstance> entities, Dictionary<string, long> fileTimestamps)
        {
            int count = 0;
            using (var db = GetConnection())
            {
                db.Open();
                using (var cmd = new SQLiteCommand(db))
                {
                    using (var transaction = db.BeginTransaction())
                    {
                        //file timestamps [filestamps]
                        cmd.CommandText = "Create table IF NOT EXISTS filestamps (FilePath TEXT, LastModified long)";
                        count += cmd.ExecuteNonQuery();
                        foreach (var pair in fileTimestamps)
                        {
                            cmd.CommandText = String.Format("insert or replace into filestamps (FilePath, LastModified) values ('{0}', '{1}')", pair.Key, pair.Value);
                            count += cmd.ExecuteNonQuery();
                        }

                        //Entities [assets]
                        cmd.CommandText = "CREATE TABLE IF NOT EXISTS assets (AssetGuid varchar(36), AssetType TEXT, PartitionGuid varchar(36), PartitionPath TEXT, PartitionName TEXT)";
                        count += cmd.ExecuteNonQuery();
                        foreach (var asset in entities)
                        {
                            cmd.CommandText = String.Format("insert or replace into assets (AssetGuid, AssetType, PartitionGuid, PartitionPath, PartitionName) values ('{0}', '{1}', '{2}', '{3}', '{4}')", asset.AssetGuid, asset.AssetType, asset.PartitionGuid, asset.PartitionPath, asset.PartitionName);
                            count += cmd.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }

                }
                db.Close();
            }
            return count;
        }

        internal static ArrayList GetTables(SQLiteConnection connection)
        {
            ArrayList list = new ArrayList();

            // executes query that select names of all tables in master table of the database
            String query = "SELECT name FROM sqlite_master " +
                    "WHERE type = 'table'" +
                    "ORDER BY 1";
            try
            {

                DataTable table = GetDataTable(connection, query);

                // Return all table names in the ArrayList

                foreach (DataRow row in table.Rows)
                {
                    list.Add(row.ItemArray[0].ToString());
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            return list;
        }

        internal static DataTable GetDataTable(SQLiteConnection connection, string sql)
        {
            try
            {
                DataTable dt = new DataTable();
                using (var c = new SQLiteConnection(connection))
                {
                    c.Open();
                    using (SQLiteCommand cmd = new SQLiteCommand(sql, c))
                    {
                        using (SQLiteDataReader rdr = cmd.ExecuteReader())
                        {
                            dt.Load(rdr);
                            return dt;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }
    }
}
