using CCMW.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class ContractorPerformanceHistory
{
    [Key]
    public Guid HistoryId { get; set; }

    public Guid ContractorId { get; set; }
    public Guid? ZoneId { get; set; }
    public DateTime ReviewPeriodStart { get; set; }
    public DateTime ReviewPeriodEnd { get; set; }
    public int ComplaintsAssigned { get; set; }
    public int ComplaintsResolved { get; set; }
    public int ResolvedOnTime { get; set; }
    public decimal AvgResolutionHours { get; set; }
    public decimal SlaComplianceRate { get; set; }
    public decimal CitizenRating { get; set; }
    public decimal PerformanceScore { get; set; }
    public decimal PenaltiesAmount { get; set; }
    public decimal BonusAmount { get; set; }
    public string ReviewNotes { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation Properties
   
    public virtual Contractor Contractor { get; set; }

    
    public virtual Zone Zone { get; set; }

    
    public virtual User Reviewer { get; set; }
}