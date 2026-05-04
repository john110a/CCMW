using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CCMW.Models
{
    [Table("FakeComplaintLog")]
    public class FakeComplaintLog
    {
        [Key]
        public Guid LogId { get; set; } = Guid.NewGuid();

        public Guid ComplaintId { get; set; }
        public Guid CitizenId { get; set; }
        public int StrikeNumber { get; set; }
        public string ActionTaken { get; set; } // Warning, TempBan, PermanentBan
        public DateTime? BannedUntil { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [ForeignKey("ComplaintId")]
        public virtual Complaint Complaint { get; set; }

        [ForeignKey("CitizenId")]
        public virtual User Citizen { get; set; }
    }
}