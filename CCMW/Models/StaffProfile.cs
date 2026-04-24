using CCMW.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("Staff_Profile")]
public class StaffProfile
{
    [Key]
    [Column("staff_id")]
    public Guid StaffId { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("department_id")]
    public Guid? DepartmentId { get; set; }

    [Column("zone_id")]
    public Guid? ZoneId { get; set; }

    [Column("role")]
    public string Role { get; set; }

    [Column("employee_id")]
    public string EmployeeId { get; set; }

    [Column("hire_date")]
    public DateTime? HireDate { get; set; }

    [Column("total_assignments")]
    public int TotalAssignments { get; set; }

    [Column("completed_assignments")]
    public int CompletedAssignments { get; set; }

    [Column("pending_assignments")]
    public int PendingAssignments { get; set; }

    [Column("average_resolution_time")]
    public decimal AverageResolutionTime { get; set; }

    [Column("performance_score")]
    public decimal PerformanceScore { get; set; }

    [Column("is_available")]
    public bool IsAvailable { get; set; } = true;

    // Navigation Properties - NO FOREIGN KEY ATTRIBUTES!
    public virtual User User { get; set; }
    public virtual Department Department { get; set; }
    public virtual Zone Zone { get; set; }
    public virtual ICollection<ComplaintAssignment> Assignments { get; set; } = new HashSet<ComplaintAssignment>();
    public virtual ICollection<Complaint> AssignedComplaints { get; set; } = new HashSet<Complaint>();
}