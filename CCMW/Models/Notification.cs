using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Notification
{
    [Key]
    public Guid NotificationId { get; set; }
    public Guid UserId { get; set; }
    public string NotificationType { get; set; } // Complaint_Update, Assignment, Escalation, etc.
    public string Title { get; set; }
    public string Message { get; set; }
    public string ReferenceType { get; set; } // Complaint, Assignment
    public Guid? ReferenceId { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ReadAt { get; set; }

    
    public virtual User User { get; set; }
}
