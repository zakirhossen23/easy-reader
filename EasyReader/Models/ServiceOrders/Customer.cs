using SQLite;

using SQLite;

#pragma warning disable CS8618

namespace EasyReader.Models.ServiceOrders
{
    public class Customer
    {
        [PrimaryKey, AutoIncrement]
        public int ID { get; set; }

        public string Accountnum { get; set; }
        public string CustomerName { get; set; }
        public string BillAddress1 { get; set; }
        public string BillAddress2 { get; set; }
        public string BillCityState { get; set; }
        public string BillZip { get; set; }
        public string Phone1 { get; set; }
        public string Phone2 { get; set; }
        public string Email { get; set; }
        public string Misc1Name { get; set; }
        public string Misc1Value { get; set; }
        public string Misc2Name { get; set; }
        public string Misc2Value { get; set; }
        public string Misc3Name { get; set; }
        public string Misc3Value { get; set; }
        public string Misc4Name { get; set; }
        public string Misc4Value { get; set; }
        public string Misc5Name { get; set; }
        public string Misc5Value { get; set; }
        public string Misc6Name { get; set; }
        public string Misc6Value { get; set; }
        public string CurrentBalance { get; set; }
        public string PastDue { get; set; }
        public string LastPaymentDate { get; set; }
        public string LastPaymentAmount { get; set; }
    }
}
