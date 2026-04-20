using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CCMW.Models;

namespace CCMW.Models
{
    public class ComplaintAssignment
    {
        [Key]
        [Column("assignment_id")]
        public Guid AssignmentId { get; set; }

        // ========================
        // Foreign Keys
        // ========================

        [Column("complaint_id")]
        public Guid ComplaintId { get; set; }

        [Column("staff_id")]
        public Guid? AssignedToId { get; set; }

        [Column("assigned_by_id")]
        public Guid? AssignedById { get; set; }

        // ========================
        // Assignment Info
        // ========================

        [Column("assigned_at")]
        public DateTime AssignedAt { get; set; } = DateTime.Now;

        [Column("assignment_type")]
        public string AssignmentType { get; set; }

        [Column("assignment_notes")]
        public string AssignmentNotes { get; set; }

        [Column("expected_completion_date")]
        public DateTime? ExpectedCompletionDate { get; set; }

        [Column("is_active")]
        public bool IsActive { get; set; } = true;

        [Column("accepted_at")]
        public DateTime? AcceptedAt { get; set; }

        [Column("started_at")]
        public DateTime? StartedAt { get; set; }

        [Column("completed_at")]
        public DateTime? CompletedAt { get; set; }

        // ========================
        // Navigation Properties - NO ATTRIBUTES!
        // ========================

        public virtual Complaint Complaint { get; set; }
        public virtual StaffProfile AssignedTo { get; set; }
        public virtual User AssignedBy { get; set; }
    }
}