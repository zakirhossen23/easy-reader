using System;
using System.Collections.Generic;
using SQLite;
using SQLiteNetExtensions.Attributes;
using EasyReader.Models.ServiceOrders;

namespace EasyReader.Models
{
    public class ServiceOrder
    {
        [PrimaryKey, AutoIncrement]
        public int ID { get; set; }

        public string SONum { get; set; }
        public bool IsClosed { get; set; }

        public bool IsBackup { get; set; }
        public DateTime? BackupDT { get; set; }
        public bool IsNull { get; set; }
        public bool SignatureSaved { get; set; }

        [ManyToOne]
        public Customer Customer { get; set; }
        [ForeignKey(typeof(Customer))]
        public int CustomerID { get; set; }

        [ManyToOne]
        public Meter Meter { get; set; }
        [ForeignKey(typeof(Meter))]
        public int MeterID { get; set; }

        [ManyToOne]
        public SOInfo SOInfo { get; set; }
        [ForeignKey(typeof(SOInfo))]
        public int SOInfoID { get; set; }

        [OneToMany(CascadeOperations = CascadeOperation.All)]
        public List<WorkItem> Work { get; set; }

        [OneToMany(CascadeOperations = CascadeOperation.All)]
        public List<SOPartItem> Parts { get; set; }
    }
    #pragma warning disable CS8618
}
