using CCMW.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class ContractPerformance
{
    [Key]
    public Guid PerformanceId { get; set; }
    public Guid ContractorId { get; set; }
    public Guid DepartmentId { get; set; }
    public DateTime ReviewPeriodStart { get; set; }
    public DateTime ReviewPeriodEnd { get; set; }
    public int ComplaintsHandled { get; set; }
    public int ComplaintsResolved { get; set; }
    public decimal AverageResolutionTimeDays { get; set; }
    public decimal SlaComplianceRate { get; set; }
    public decimal CitizenSatisfactionScore { get; set; }
    public decimal PerformanceScore { get; set; }
    public decimal PenaltiesIncurred { get; set; }
    public decimal BonusesEarned { get; set; }
    public string ReviewNotes { get; set; }
    public Guid ReviewedById { get; set; }
    public DateTime ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    
    public virtual Contractor Contractor { get; set; }

    
    public virtual Department Department { get; set; }

    
    public virtual User ReviewedBy { get; set; }

}
