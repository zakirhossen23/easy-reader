using SQLite;

namespace EasyReader.Services
{
    public interface IDatabaseConnection
    {
        SQLiteConnection DbConnection();
    }
}
