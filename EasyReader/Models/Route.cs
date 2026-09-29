using SQLite;

namespace EasyReader.Models
{
#pragma warning disable CS8618
    public class Route
    {
        [PrimaryKey, AutoIncrement]
        public int ID { get; set; }

        public string RouteNumber { get; set; }
    }
}
