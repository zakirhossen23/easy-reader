using SQLite;

namespace EasyReader.Models.ServiceOrders
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    #pragma warning disable CS8618

    public class ActionItem
    {
        [PrimaryKey, AutoIncrement]
        public int ID { get; set; }

        public string ActionCode { get; set; }
        public string ActionDesc { get; set; }
    }
}
