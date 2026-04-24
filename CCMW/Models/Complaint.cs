using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace CCMW.Models
{
    public class Complaint
    {
        [Key]
        [Column("complaint_id")]
        public Guid ComplaintId { get; set; }

        [Column("citizen_id")]
        public Guid CitizenId { get; set; }

        [Column("category_id")]
        public Guid CategoryId { get; set; }

        [Column("department_id")]
        public Guid DepartmentId { get; set; }

        [Column("zone_id")]
        public Guid? ZoneId { get; set; }

        [Required]
        [Column("title")]
        public string Title { get; set; }

        [Required]
        [Column("description")]
        public string Description { get; set; }

        [Obsolete("This field is for legacy display only. Use CurrentStatus/SubmissionStatus instead.")]
        [Column("status")]
        public string Status { get; set; }

        [Column("priority")]
        public string Priority { get; set; }

        [Column("escalation_level")]
        public int EscalationLevel { get; set; } = 0;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column("resolved_at")]
        public DateTime? ResolvedAt { get; set; }

        // Location
        [Column("location_address")]
        public string LocationAddress { get; set; }

        [Column("location_latitude")]
        public decimal LocationLatitude { get; set; }

        [Column("location_longitude")]
        public decimal LocationLongitude { get; set; }

        [Column("location_landmark")]
        public string LocationLandmark { get; set; }

        // Assignment
        [Column("assigned_to_id")]
        public Guid? AssignedToId { get; set; }

        [Column("assigned_at")]
        public DateTime? AssignedAt { get; set; }

        // Status
        [Column("SubmissionStatus")]
        public SubmissionStatus SubmissionStatus { get; set; }

        [Column("CurrentStatus")]
        public ComplaintStatus CurrentStatus { get; set; }

        [Column("ApprovedById")]
        public Guid? ApprovedById { get; set; }

        [Column("RejectionReason")]
        public string RejectionReason { get; set; }

        [Column("StatusUpdatedAt")]
        public DateTime? StatusUpdatedAt { get; set; }

        // Analytics
        [Column("UpvoteCount")]
        public int UpvoteCount { get; set; }

        [Column("ViewCount")]
        public int ViewCount { get; set; }

        [Column("IsDuplicate")]
        public bool IsDuplicate { get; set; }

        [Column("MergedIntoComplaintId")]
        public Guid? MergedIntoComplaintId { get; set; }

        [Column("UpdatedAt")]
        public DateTime? UpdatedAt { get; set; }

        [Column("ClosedAt")]
        public DateTime? ClosedAt { get; set; }

        [Column("ExpectedResolutionDate")]
        public DateTime? ExpectedResolutionDate { get; set; }

        [Column("IsOverdue")]
        public bool IsOverdue { get; set; }

        [Column("ComplaintNumber")]
        public string ComplaintNumber { get; set; }

        [Column("ResolutionNotes")]
        public string ResolutionNotes { get; set; }

        [Column("ReopenedAt")]
        public DateTime? ReopenedAt { get; set; }

        // ========================
        // Navigation Properties - FIXED WITH [ForeignKey]
        // ========================

        [ForeignKey("CitizenId")]
        public virtual User Citizen { get; set; }

        [ForeignKey("CategoryId")]
        public virtual ComplaintCategory Category { get; set; }

        [ForeignKey("DepartmentId")]
        public virtual Department Department { get; set; }

        [ForeignKey("ZoneId")]
        public virtual Zone Zone { get; set; }

        [ForeignKey("ApprovedById")]
        public virtual User ApprovedBy { get; set; }

        [ForeignKey("AssignedToId")]
        public virtual StaffProfile AssignedTo { get; set; }

        [ForeignKey("MergedIntoComplaintId")]
        public virtual Complaint MergedIntoComplaint { get; set; }

        // Collections
        public virtual ICollection<ComplaintAssignment> Assignments { get; set; }
        public virtual ICollection<ComplaintStatusHistories> StatusHistory { get; set; }
        public virtual ICollection<ComplaintUpvote> Upvotes { get; set; }
        public virtual ICollection<ComplaintPhoto> ComplaintPhotos { get; set; }
        public virtual ICollection<Escalation> Escalations { get; set; }
        public virtual ICollection<ComplaintFeedback> Feedback { get; set; }

        public Complaint()
        {
            Assignments = new HashSet<ComplaintAssignment>();
            StatusHistory = new HashSet<ComplaintStatusHistories>();
            Upvotes = new HashSet<ComplaintUpvote>();
            ComplaintPhotos = new HashSet<ComplaintPhoto>();
            Escalations = new HashSet<Escalation>();
            Feedback = new HashSet<ComplaintFeedback>();
        }

        [NotMapped]
        public DateTime? LatestEscalationDate
        {
            get
            {
                return Escalations?.OrderByDescending(e => e.EscalatedAt)
                                  .FirstOrDefault()?.EscalatedAt;
            }
        }
    }
}