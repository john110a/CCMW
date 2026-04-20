using CCMW.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Escalation
{
    [Key]
    public Guid EscalationId { get; set; }

    public Guid ComplaintId { get; set; }
    public int EscalationLevel { get; set; } // 1,2,3
    public Guid EscalatedFromId { get; set; }
    public Guid EscalatedToId { get; set; }
    public Guid EscalatedById { get; set; }

    public string EscalationReason { get; set; } // e.g., TimeExceeded, Manual, PriorityIncrease
    public decimal HoursElapsed { get; set; }
    public string EscalationNotes { get; set; }
    public bool Resolved { get; set; } = false;
    public DateTime? ResolvedAt { get; set; }
    public DateTime EscalatedAt { get; set; } = DateTime.Now;

    
    public virtual Complaint Complaint { get; set; }

    
    public virtual User EscalatedFrom { get; set; }

    
    public virtual User EscalatedTo { get; set; }

    
    public virtual User EscalatedBy { get; set; }
}
