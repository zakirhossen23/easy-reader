using System.IO;
using SQLite;
using EasyReader.Services;

namespace EasyReader.Platforms.Android
{
    public class SQLite_Android : IDatabaseConnection
    {
        public SQLiteConnection DbConnection()
        {
            var dbName = "SqliteDb.db3";
            string docPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
            var path = Path.Combine(docPath, dbName);

            // ensure directory exists
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // if database file does not exist, create it by opening and closing a connection
            if (!File.Exists(path))
            {
                using (var tmp = new SQLiteConnection(path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache))
                {
                    tmp.Close();
                }
            }

            return new SQLiteConnection(path, SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
        }
    }
}
