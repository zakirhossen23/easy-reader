using SQLite;
using System.Threading.Tasks;

namespace EasyReader.Models
{
    public class Properties
    {
    #pragma warning disable CS8618
        [PrimaryKey]
        public int ID { get; set; }  // always 0

        // READINGS
        // -- current reading
        public int ReadingInc { get; set; }
        public int ReadingIncID { get; set; }
        public int ReadingLastEnteredID { get; set; }

        // -- account statuses
        public int TotalAccounts { get; set; }
        public int MissingAccounts { get; set; }
        public bool IsAccountDownloaded { get; set; }
        public string ReadingsFileName { get; set; }
        public string BackupReadingsFileName { get; set; }
        public string Misc1 { get; set; }
        public bool AccountChangesMade { get; set; }

        // SERVICE ORDERS
        // -- current so
        public int SOInc { get; set; }
        public int SOIncID { get; set; }

        // -- so statuses
        public int TotalServiceOrders { get; set; }
        public int OpenSOs { get; set; }
        public bool IsSODownloaded { get; set; }

        // SETTINGS
        public string ReaderDescription { get; set; }
        public string HostFolder { get; set; }
        public string SortMethod { get; set; }
        public string RouteSelected { get; set; }
        public bool IsMissingOnly { get; set; }
        public bool IsOpenOnly { get; set; }
        public bool IsAutoAdvance { get; set; }
        public bool IsVarianceEnabled { get; set; }
        public string Variance { get; set; }
        public int Decimals { get; set; }
        public int BackupFilesDurationDays { get; set; }

        // DIRECTORIES
        public string AccountsFolderPath { get; set; }
        public string AccountsBackupFilesFolderPath { get; set; }
        public string ImportedFilesFolderPath { get; set; }
        public string ExportedFilesFolderPath { get; set; }
        public string ServiceOrdersFolderPath { get; set; }
        public string ServiceOrdersBackupFilesFolderPath { get; set; }

        // OLD DIRECTORIES - ACTIVE ONLY IN 2.1.7
        public string AccountsFolderPath_OLD { get; set; }
        public string AccountsBackupFilesFolderPath_OLD { get; set; }
        public string ImportedFilesFolderPath_OLD { get; set; }
        public string ExportedFilesFolderPath_OLD { get; set; }
        public string ServiceOrdersFolderPath_OLD { get; set; }
        public string ServiceOrdersBackupFilesFolderPath_OLD { get; set; }

        // IMPLICIT OPERATOR
        public static implicit operator Properties(Task<Properties> p)
        {
            return p.Result;
        }
    }
}
