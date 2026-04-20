using CCMW.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class ComplaintFeedback
{
    [Key]
    [Column("feedback_id")]
    public Guid FeedbackId { get; set; }

    [Column("complaint_id")]
    public Guid ComplaintId { get; set; }

    [Column("citizen_id")]
    public Guid CitizenId { get; set; }

    [Column("rating")]
    [Range(1, 5)]
    public int Rating { get; set; }

    [Column("comments")]
    public string Comments { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

 
    public virtual Complaint Complaint { get; set; }

    public virtual User Citizen { get; set; }
}