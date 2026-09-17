using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIbillingRAGBuilder.Dtos
{
    public enum CustomerRemarkType
    {
        Internal,
        External,
        WorkOrder,
        Parts,
    }

    public class WorkOrderRemarkDto
    {
        // PK is WorkOrderID + WorkOrderRemarkID
        public int WorkOrderID { get; set; }
        public int WorkOrderRemarkID { get; set; }

        public int TechnicianId { get; set; }
        public string? Technician { get; set; }

        public DateTime? Time { get; set; }

        public CustomerRemarkType RemarkType { get; set; }

        public string? Content { get; set; }

    }
}
