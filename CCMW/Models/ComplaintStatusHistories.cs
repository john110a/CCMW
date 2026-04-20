using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CCMW.Models
{
    public class ComplaintStatusHistories
    {
        [Key]
        [Column("history_id")]
        public Guid HistoryId { get; set; }

        [Column("complaint_id")]
        public Guid ComplaintId { get; set; }

        [Column("old_status")]
        public string PreviousStatus { get; set; }

        [Column("new_status")]
        public string NewStatus { get; set; }

        [Column("changed_by")]
        public Guid? ChangedById { get; set; }

        [Column("changed_at")]
        public DateTime ChangedAt { get; set; } = DateTime.Now;

        public string ChangeReason { get; set; }
        public string Notes { get; set; }

      
        public virtual Complaint Complaint { get; set; }

        
        public virtual User ChangedBy { get; set; }
    }
}
