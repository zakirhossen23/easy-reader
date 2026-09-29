using SQLite;

namespace EasyReader.Models.ServiceOrders
{
    public class Meter
    {
        [PrimaryKey, AutoIncrement]
        public int ID { get; set; }
        
        public string UtitlityType { get; set; }
        public string MeterID { get; set; }
        public string MeterNum { get; set; }
        public string ServiceAddress { get; set; }
        public string MeterLocation { get; set; }
        public string Route { get; set; }
        public string ReadSequence { get; set; }
        public string MXUNum { get; set; }
        public string MeterBrand { get; set; }
        public string MeterType { get; set; }
        public string MeterReadType { get; set; }
        public string MeterSize { get; set; }
        public string MeterNumDials { get; set; }
        public string MeterInstallDate { get; set; }
        public string MeterLastServiceDate { get; set; }
        public string MeterServiceCode { get; set; }
        public string LastReadDate { get; set; }
        public string LastReading { get; set; }
        public string AvgUsage { get; set; }
        public string Backflow { get; set; }
        public string Latitude { get; set; }
        public string Longitude { get; set; }
        public string MeterMisc1 { get; set; }
        public string MeterSpecialText { get; set; }

        // NOT RECEIVED IN FTP
        // -- readings
        public string CurrentReading { get; set; }
        public string FinalReading { get; set; }
        public string InitialReading { get; set; }
        public string ChangeoutDate { get; set; }

        // -- location
        public bool IsLocationSaved { get; set; }
    }
    #pragma warning disable CS8618
}
