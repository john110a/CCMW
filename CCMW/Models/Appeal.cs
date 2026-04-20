using CCMW.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Appeal
{
    [Key]
    public Guid AppealId { get; set; }
    public Guid ComplaintId { get; set; }
    public Guid CitizenId { get; set; }
    public string AppealReason { get; set; }
    public string SupportingDocuments { get; set; } // JSON URLs
    public string AppealStatus { get; set; } // Pending, Under_Review, Approved, Rejected
    public Guid? ReviewedById { get; set; }
    public string ReviewNotes { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.Now;
    public DateTime? ReviewedAt { get; set; }


    public virtual Complaint Complaint { get; set; }

    
    public virtual User Citizen { get; set; }

   
    public virtual User ReviewedBy { get; set; }
}
