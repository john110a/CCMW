using CCMW.Models;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("ContractorZoneAssignments")]
public class ContractorZoneAssignment
{
    [Key]
    [Column("assignment_id")]
    public Guid AssignmentId { get; set; }

    [Column("contractor_id")]
    public Guid ContractorId { get; set; }

    [Column("zone_id")]
    public Guid ZoneId { get; set; }

    [Column("assigned_by")]
    public Guid AssignedBy { get; set; }

    [Column("assigned_date")]
    public DateTime AssignedDate { get; set; }

    [Column("contract_start")]
    public DateTime ContractStart { get; set; }

    [Column("contract_end")]
    public DateTime ContractEnd { get; set; }

    [Column("service_type")]
    public string ServiceType { get; set; }

    [Column("contract_value")]
    public decimal ContractValue { get; set; }

    [Column("performance_bond")]
    public decimal PerformanceBond { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    [Column("termination_reason")]
    public string TerminationReason { get; set; }

    [Column("terminated_at")]
    public DateTime? TerminatedAt { get; set; }

    [Column("terminated_by")]
    public Guid? TerminatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }

    // Navigation Properties
    [ForeignKey("ContractorId")]
    public virtual Contractor Contractor { get; set; }

    [ForeignKey("ZoneId")]
    public virtual Zone Zone { get; set; }

    [ForeignKey("AssignedBy")]
    public virtual User AssignedByAdmin { get; set; }

    [ForeignKey("TerminatedBy")]
    public virtual User TerminatedByAdmin { get; set; }
}