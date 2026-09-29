using SQLite;

#pragma warning disable CS8618

namespace EasyReader.Models.ServiceOrders
{
    public class Technician
    {
        [PrimaryKey, AutoIncrement]
        public int ID { get; set; }

        public string TechInitials { get; set; }
        public string TechName { get; set; }
    }
}
