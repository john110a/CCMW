using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class ActivityLog
{
    [Key]
    public Guid LogId { get; set; }
    public Guid UserId { get; set; }
    public string ActionType { get; set; } // Create, Update, Delete, Login, etc.
    public string EntityType { get; set; } // Complaint, User, Department, etc.
    public Guid? EntityId { get; set; }
    public string ActionDetails { get; set; } // JSON string
    public string IpAddress { get; set; }
    public string UserAgent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

  
    public virtual User User { get; set; }
}
