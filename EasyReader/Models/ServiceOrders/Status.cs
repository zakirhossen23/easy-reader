using SQLite;

#pragma warning disable CS8618

namespace EasyReader.Models.ServiceOrders
{
    public class Status
    {
        [PrimaryKey, AutoIncrement]
        public int ID { get; set; }

        public string Name { get; set; }
        public bool WriteSOs { get; set; }
        public int Count { get; set; }
        public string DisplayLabel { get; set; }
    }
}
