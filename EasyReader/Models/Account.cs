using System.Threading.Tasks;
using SQLite;
using CsvHelper.Configuration;

namespace EasyReader.Models
{
#pragma warning disable CS8618
    public class Account
    {
        [PrimaryKey]
        public int ID { get; set; }

        public string Route { get; set; }
        public string WalkSequence { get; set; }
        public string AccountNumber { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string Location { get; set; }
        public string MeterNumber { get; set; }
        public string CustomerType { get; set; }
        public string LastReadDate { get; set; }
        public string LastReading { get; set; }
        public string NewReadDate { get; set; }
        public string NewReadTime { get; set; }
        public string NewReading { get; set; }
        public string Notes { get; set; }
        public string Misc1 { get; set; }
        public string Utility { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public string AvgUsage { get; set; }
        public string Premise { get; set; }
        public string DemandYN { get; set; }
        public string NewDemand { get; set; }
        public string Balance { get; set; }

        // NOT IN CSV
        public string NewUsage { get; set; }
        public bool DemandNeeded { get; set; }
        public bool EnteredReading { get; set; }
        public bool IsLocationSaved { get; set; }

        public bool IsNull { get; set; }

        // IMPLICIT OPERATOR
        public static implicit operator Account(Task<Account> v)
        {
            Account acnt = v.Result;
            return acnt;
        }
    }

    // account map for csv helper
    public sealed class AccountMap : ClassMap<Account>
    {
        public AccountMap()
        {
            Map(a => a.Route).Index(0);
            Map(a => a.WalkSequence).Index(1);
            Map(a => a.AccountNumber).Index(2);
            Map(a => a.Name).Index(3);
            Map(a => a.Address).Index(4);
            Map(a => a.Location).Index(5);
            Map(a => a.MeterNumber).Index(6);
            Map(a => a.CustomerType).Index(7);
            Map(a => a.LastReadDate).Index(8);
            Map(a => a.LastReading).Index(9);
            Map(a => a.NewReadDate).Index(10);
            Map(a => a.NewReadTime).Index(11);
            Map(a => a.NewReading).Index(12);
            Map(a => a.Notes).Index(13);
            Map(a => a.Misc1).Index(14);
            Map(a => a.Utility).Index(15);
            Map(a => a.Latitude).Index(16);
            Map(a => a.Longitude).Index(17);
            Map(a => a.AvgUsage).Index(18);
            Map(a => a.Premise).Index(19);
            Map(a => a.DemandYN).Index(20);
            Map(a => a.NewDemand).Index(21);
            Map(a => a.Balance).Index(22);
        }
    }
}
