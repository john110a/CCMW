using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CCMW.Models;

public class ComplaintUpvote
{
    [Key]
    [Column("upvote_id")]
    public Guid UpvoteId { get; set; }

    [Column("complaint_id")]
    public Guid ComplaintId { get; set; }

    [Column("citizen_id")]
    public Guid? CitizenId { get; set; }

    [Column("upvoted_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // Navigation properties
    
    public virtual Complaint Complaint { get; set; }

    
    public virtual User Citizen { get; set; }

    // Optional extra properties
    public string Notes { get; set; }
}
