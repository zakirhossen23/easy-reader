using SQLite;

namespace EasyReader.Models
{
    public class BackupAccount
    {
        [PrimaryKey]
    #pragma warning disable CS8618

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

        // NOT IN CSV
        public string NewUsage { get; set; }
        public bool DemandNeeded { get; set; }
        public string NewDemand { get; set; }
        public bool EnteredReading { get; set; }
        public bool IsLocationSaved { get; set; }

        public bool IsNull { get; set; }
    }
}
