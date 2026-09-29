using SQLite;
using SQLiteNetExtensions.Attributes;

#pragma warning disable CS8618

namespace EasyReader.Models.ServiceOrders
{
    public class SOPartItem
    {
        [PrimaryKey, AutoIncrement]
        public int ID { get; set; }

        public string Code { get; set; }

        [ForeignKey(typeof(ServiceOrder))]
        public int ServiceOrderID { get; set; }
    }
}
