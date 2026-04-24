using CCMW.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class User
{
    [Key]
    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("user_type")]
    public string UserType { get; set; }

    [Column("full_name")]
    public string FullName { get; set; }

    [Column("email")]
    public string Email { get; set; }

    [Column("password_hash")]
    public string PasswordHash { get; set; }

    [Column("PhoneNumber")]
    public string PhoneNumber { get; set; }

    [Column("zone_id")]
    public Guid? ZoneId { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column("CNIC")]
    public string CNIC { get; set; }

    [Column("Address")]
    public string Address { get; set; }

    [Column("ProfilePhotoUrl")]
    public string ProfilePhotoUrl { get; set; }

    [Column("IsVerified")]
    public bool IsVerified { get; set; } = false;

    [Column("UpdatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [Column("LastLogin")]
    public DateTime? LastLogin { get; set; }

    // Navigation Properties
    public virtual CitizenProfile CitizenProfile { get; set; }
    public virtual StaffProfile StaffProfile { get; set; }
    public virtual Zone Zone { get; set; }

    // Collections
    public virtual ICollection<Complaint> Complaints { get; set; } = new HashSet<Complaint>();
    public virtual ICollection<ComplaintUpvote> Upvotes { get; set; } = new HashSet<ComplaintUpvote>();
    public virtual ICollection<ComplaintPhoto> UploadedPhotos { get; set; } = new HashSet<ComplaintPhoto>();
    public virtual ICollection<ComplaintStatusHistories> ComplaintStatusHistories { get; set; } = new HashSet<ComplaintStatusHistories>();
    public virtual ICollection<ActivityLog> ActivityLogs { get; set; } = new HashSet<ActivityLog>();

    // ✅ FIXED: Removed duplicate ComplaintAssignments collection
    // Only keep AssignedAssignments which is properly mapped in DbContext
    public virtual ICollection<ComplaintAssignment> AssignedAssignments { get; set; } = new HashSet<ComplaintAssignment>();
}