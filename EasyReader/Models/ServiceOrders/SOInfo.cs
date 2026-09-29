using SQLite;

#pragma warning disable CS8618

namespace EasyReader.Models.ServiceOrders
{
    public class SOInfo
    {
        [PrimaryKey, AutoIncrement]
        public int ID { get; set; }
        public bool IsBackup { get; set; }

        public string FileID { get; set; }
        public string SOCreationDate { get; set; }
        public string SOStatus { get; set; }
        public string SOPriority { get; set; }
        public string Instructions { get; set; }
        public string TechAssigned { get; set; }
        public string RequestedServiceDate { get; set; }
        public string RequestedServiceStartTime { get; set; }

        // NOT RECEIVED
        public string ActionTakenCode { get; set; }
        public string ActionTakenDesc { get; set; }
        public string ServicedByInitials { get; set; }
        public string ServicedByName { get; set; }
        public string ServiceDate { get; set; }
        public string ServiceStartTime { get; set; }
        public string ServiceEndTime { get; set; }
        public string Notes { get; set; }
    }
}
