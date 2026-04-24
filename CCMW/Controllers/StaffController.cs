using CCMW.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Http;
using System.Collections.Generic;
using System.Net;

namespace CCMW.Controllers
{
    [RoutePrefix("api/staff")]
    public class StaffController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        private IHttpActionResult NotFoundMsg(string message)
        {
            return Content(HttpStatusCode.NotFound, new { error = message });
        }

        // =====================================================
        // GET ALL STAFF
        // =====================================================
        [HttpGet]
        [Route("")]
        public IHttpActionResult GetAllStaff([FromUri] Guid? departmentId = null)
        {
            try
            {
                var sql = @"
                    SELECT 
                        s.staff_id as StaffId,
                        s.user_id as UserId,
                        ISNULL(u.full_name, '') as FullName,
                        ISNULL(u.email, '') as Email,
                        ISNULL(u.PhoneNumber, '') as PhoneNumber,
                        s.department_id as DepartmentId,
                        ISNULL(d.department_name, '') as DepartmentName,
                        s.zone_id as ZoneId,
                        ISNULL(z.zone_name, '') as ZoneName,
                        ISNULL(s.role, 'Field_Staff') as Role,
                        ISNULL(s.employee_id, '') as EmployeeId,
                        s.hire_date as HireDate,
                        ISNULL(s.total_assignments, 0) as TotalAssignments,
                        ISNULL(s.completed_assignments, 0) as CompletedAssignments,
                        ISNULL(s.pending_assignments, 0) as PendingAssignments,
                        ISNULL(s.average_resolution_time, 0) as AverageResolutionTime,
                        ISNULL(s.performance_score, 0) as PerformanceScore,
                        CAST(ISNULL(s.is_available, 1) AS BIT) as IsAvailable
                    FROM Staff_Profile s
                    INNER JOIN Users u ON s.user_id = u.user_id
                    LEFT JOIN Departments d ON s.department_id = d.department_id
                    LEFT JOIN Zones z ON s.zone_id = z.zone_id
                ";

                if (departmentId.HasValue)
                {
                    sql += " WHERE s.department_id = @p0";
                    var staff = db.Database.SqlQuery<StaffDto>(sql, departmentId.Value).ToList();
                    return Ok(new { TotalStaff = staff.Count, Staff = staff });
                }
                else
                {
                    var staff = db.Database.SqlQuery<StaffDto>(sql).ToList();
                    return Ok(new { TotalStaff = staff.Count, Staff = staff });
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET AVAILABLE STAFF
        // =====================================================
        [HttpGet]
        [Route("available")]
        public IHttpActionResult GetAvailableStaff([FromUri] Guid? departmentId = null, [FromUri] int maxWorkload = 5)
        {
            try
            {
                var sql = @"
                    SELECT 
                        s.staff_id as StaffId,
                        s.user_id as UserId,
                        ISNULL(u.full_name, '') as FullName,
                        ISNULL(u.email, '') as Email,
                        ISNULL(u.PhoneNumber, '') as PhoneNumber,
                        s.department_id as DepartmentId,
                        ISNULL(d.department_name, '') as DepartmentName,
                        ISNULL(s.role, 'Field_Staff') as Role,
                        ISNULL(s.pending_assignments, 0) as PendingAssignments,
                        ISNULL(s.performance_score, 0) as PerformanceScore,
                        ISNULL(s.average_resolution_time, 0) as AverageResolutionTime,
                        CAST(ISNULL(s.is_available, 1) AS BIT) as IsAvailable
                    FROM Staff_Profile s
                    INNER JOIN Users u ON s.user_id = u.user_id
                    LEFT JOIN Departments d ON s.department_id = d.department_id
                    WHERE s.is_available = 1 AND ISNULL(s.pending_assignments, 0) < @p0
                ";

                if (departmentId.HasValue)
                {
                    sql += " AND s.department_id = @p1";
                    sql += " ORDER BY s.pending_assignments ASC, s.performance_score DESC";
                    var staff = db.Database.SqlQuery<AvailableStaffDto>(sql, maxWorkload, departmentId.Value).ToList();
                    return Ok(new { TotalAvailable = staff.Count, Staff = staff });
                }
                else
                {
                    sql += " ORDER BY s.pending_assignments ASC, s.performance_score DESC";
                    var staff = db.Database.SqlQuery<AvailableStaffDto>(sql, maxWorkload).ToList();
                    return Ok(new { TotalAvailable = staff.Count, Staff = staff });
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET STAFF BY DEPARTMENT - FIXED AMBIGUITY
        // =====================================================
        [HttpGet]
        [Route("department/{departmentId:guid}")]
        public IHttpActionResult GetStaffByDepartment(Guid departmentId)
        {
            try
            {
                var sql = @"
                    SELECT 
                        s.staff_id as StaffId,
                        s.user_id as UserId,
                        ISNULL(u.full_name, '') as FullName,
                        ISNULL(u.email, '') as Email,
                        ISNULL(u.PhoneNumber, '') as PhoneNumber,
                        s.department_id as DepartmentId,
                        ISNULL(s.role, 'Field_Staff') as Role,
                        ISNULL(s.employee_id, '') as EmployeeId,
                        ISNULL(s.total_assignments, 0) as TotalAssignments,
                        ISNULL(s.completed_assignments, 0) as CompletedAssignments,
                        ISNULL(s.pending_assignments, 0) as PendingAssignments,
                        ISNULL(s.performance_score, 0) as PerformanceScore,
                        CAST(ISNULL(s.is_available, 1) AS BIT) as IsAvailableFlag
                    FROM Staff_Profile s
                    INNER JOIN Users u ON s.user_id = u.user_id
                    WHERE s.department_id = @p0
                    ORDER BY s.performance_score DESC
                ";

                var staff = db.Database.SqlQuery<DepartmentStaffDto>(sql, departmentId).ToList();

                return Ok(new
                {
                    DepartmentId = departmentId,
                    TotalStaff = staff.Count,
                    AvailableStaff = staff.Count(s => s.IsAvailable && s.PendingAssignments < 5),
                    Staff = staff
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET STAFF PERFORMANCE
        // =====================================================
        [HttpGet]
        [Route("{staffId:guid}/performance")]
        public IHttpActionResult GetStaffPerformance(Guid staffId)
        {
            try
            {
                var staffSql = @"
                    SELECT 
                        s.staff_id as StaffId,
                        s.user_id as UserId,
                        ISNULL(u.full_name, '') as FullName,
                        ISNULL(u.email, '') as Email,
                        ISNULL(u.PhoneNumber, '') as PhoneNumber,
                        ISNULL(d.department_name, '') as DepartmentName,
                        ISNULL(s.role, 'Field_Staff') as Role,
                        ISNULL(s.employee_id, '') as EmployeeId,
                        s.hire_date as HireDate,
                        ISNULL(s.total_assignments, 0) as TotalAssignments,
                        ISNULL(s.completed_assignments, 0) as CompletedAssignments,
                        ISNULL(s.pending_assignments, 0) as PendingAssignments,
                        ISNULL(s.average_resolution_time, 0) as AverageResolutionTime,
                        ISNULL(s.performance_score, 0) as PerformanceScore,
                        CAST(ISNULL(s.is_available, 1) AS BIT) as IsAvailableFlag
                    FROM Staff_Profile s
                    INNER JOIN Users u ON s.user_id = u.user_id
                    LEFT JOIN Departments d ON s.department_id = d.department_id
                    WHERE s.staff_id = @p0
                ";

                var staff = db.Database.SqlQuery<StaffPerformanceDto>(staffSql, staffId).FirstOrDefault();

                if (staff == null)
                    return NotFoundMsg("Staff not found");

                // Get completed assignments in last 30 days
                var recentSql = @"
                    SELECT ISNULL(COUNT(*), 0) as Count
                    FROM ComplaintAssignments
                    WHERE staff_id = @p0 
                        AND CompletedAt IS NOT NULL 
                        AND CompletedAt >= DATEADD(day, -30, GETDATE())
                ";
                var recentCount = db.Database.SqlQuery<int>(recentSql, staffId).FirstOrDefault();

                // Calculate SLA compliance
                var slaSql = @"
                    SELECT 
                        ISNULL(COUNT(*), 0) as Total,
                        ISNULL(SUM(CASE 
                            WHEN DATEDIFF(HOUR, assigned_at, CompletedAt) <= DATEDIFF(HOUR, assigned_at, ExpectedCompletionDate) 
                            THEN 1 ELSE 0 END), 0) as MetSLA
                    FROM ComplaintAssignments
                    WHERE staff_id = @p0 
                        AND CompletedAt IS NOT NULL 
                        AND ExpectedCompletionDate IS NOT NULL
                ";
                var slaResult = db.Database.SqlQuery<SlaResultDto>(slaSql, staffId).FirstOrDefault();

                double slaRate = 0;
                if (slaResult != null && slaResult.Total > 0)
                {
                    slaRate = (double)slaResult.MetSLA / slaResult.Total * 100;
                }

                // Get monthly trends
                var monthlySql = @"
                    SELECT 
                        YEAR(CompletedAt) as Year,
                        MONTH(CompletedAt) as Month,
                        COUNT(*) as Completed,
                        AVG(DATEDIFF(HOUR, assigned_at, CompletedAt)) as AverageTime
                    FROM ComplaintAssignments
                    WHERE staff_id = @p0 AND CompletedAt IS NOT NULL
                    GROUP BY YEAR(CompletedAt), MONTH(CompletedAt)
                    ORDER BY Year DESC, Month DESC
                ";
                var monthlyTrend = db.Database.SqlQuery<MonthlyTrendDto>(monthlySql, staffId).ToList();

                // Calculate completion rate
                double completionRate = 0;
                if (staff.TotalAssignments > 0)
                {
                    completionRate = (double)staff.CompletedAssignments / staff.TotalAssignments * 100;
                }

                return Ok(new
                {
                    Staff = new
                    {
                        staff.StaffId,
                        staff.FullName,
                        staff.Email,
                        staff.PhoneNumber,
                        staff.DepartmentName,
                        staff.Role,
                        staff.EmployeeId,
                        staff.HireDate
                    },
                    Performance = new
                    {
                        TotalAssignments = staff.TotalAssignments,
                        CompletedAssignments = staff.CompletedAssignments,
                        PendingAssignments = staff.PendingAssignments,
                        CompletionRate = Math.Round(completionRate, 2),
                        AverageResolutionTime = staff.AverageResolutionTime,
                        PerformanceScore = staff.PerformanceScore,
                        SLAComplianceRate = Math.Round(slaRate, 2),
                        RecentPerformance = recentCount,
                        OnTimeDelivery = slaResult?.MetSLA ?? 0
                    },
                    MonthlyTrend = monthlyTrend
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET STAFF DASHBOARD
        // =====================================================
        [HttpGet]
        [Route("{staffId:guid}/dashboard")]
        public IHttpActionResult GetStaffDashboard(Guid staffId)
        {
            try
            {
                var staffSql = @"
                    SELECT 
                        s.staff_id as StaffId,
                        s.user_id as UserId,
                        ISNULL(u.full_name, '') as FullName,
                        ISNULL(u.email, '') as Email,
                        ISNULL(u.PhoneNumber, '') as PhoneNumber,
                        ISNULL(d.department_name, '') as DepartmentName,
                        ISNULL(s.role, 'Field_Staff') as Role,
                        ISNULL(s.employee_id, '') as EmployeeId,
                        ISNULL(s.total_assignments, 0) as TotalAssignments,
                        ISNULL(s.completed_assignments, 0) as CompletedAssignments,
                        ISNULL(s.pending_assignments, 0) as PendingAssignments,
                        ISNULL(s.performance_score, 0) as PerformanceScore,
                        CAST(ISNULL(s.is_available, 1) AS BIT) as IsAvailableFlag
                    FROM Staff_Profile s
                    INNER JOIN Users u ON s.user_id = u.user_id
                    LEFT JOIN Departments d ON s.department_id = d.department_id
                    WHERE s.staff_id = @p0
                ";

                var staff = db.Database.SqlQuery<StaffDashboardDto>(staffSql, staffId).FirstOrDefault();

                if (staff == null)
                    return NotFoundMsg("Staff not found");

                // Get active assignments
                var activeSql = @"
                    SELECT 
                        a.assignment_id as AssignmentId,
                        a.complaint_id as ComplaintId,
                        ISNULL(c.title, 'No Title') as Title,
                        ISNULL(c.priority, 'Medium') as Priority,
                        a.assigned_at as AssignedAt,
                        a.ExpectedCompletionDate as ExpectedCompletionDate,
                        CAST(CASE 
                            WHEN a.ExpectedCompletionDate IS NOT NULL AND a.ExpectedCompletionDate < GETDATE() AND a.CompletedAt IS NULL
                            THEN 1 ELSE 0 
                        END AS BIT) as IsOverdue
                    FROM ComplaintAssignments a
                    INNER JOIN Complaints c ON a.complaint_id = c.complaint_id
                    WHERE a.staff_id = @p0 
                        AND a.CompletedAt IS NULL 
                        AND a.IsActive = 1
                    ORDER BY a.assigned_at DESC
                ";
                var activeAssignments = db.Database.SqlQuery<ActiveAssignmentDto>(activeSql, staffId).ToList();

                // Get recently completed
                var recentSql = @"
                    SELECT 
                        a.assignment_id as AssignmentId,
                        a.complaint_id as ComplaintId,
                        ISNULL(c.title, 'No Title') as Title,
                        a.CompletedAt as CompletedAt,
                        ISNULL(DATEDIFF(HOUR, a.assigned_at, a.CompletedAt), 0) as ResolutionHours
                    FROM ComplaintAssignments a
                    INNER JOIN Complaints c ON a.complaint_id = c.complaint_id
                    WHERE a.staff_id = @p0 
                        AND a.CompletedAt IS NOT NULL 
                        AND a.CompletedAt >= DATEADD(day, -7, GETDATE())
                    ORDER BY a.CompletedAt DESC
                ";
                var recentCompleted = db.Database.SqlQuery<RecentCompletedDto>(recentSql, staffId).ToList();

                // Calculate weekly stats
                var weeklySql = @"
                    SELECT ISNULL(COUNT(*), 0) as Count
                    FROM ComplaintAssignments
                    WHERE staff_id = @p0 
                        AND CompletedAt IS NOT NULL 
                        AND CompletedAt >= DATEADD(day, -7, GETDATE())
                ";
                var weeklyCompleted = db.Database.SqlQuery<int>(weeklySql, staffId).FirstOrDefault();

                // Calculate completion rate
                double completionRate = 0;
                if (staff.TotalAssignments > 0)
                {
                    completionRate = (double)staff.CompletedAssignments / staff.TotalAssignments * 100;
                }

                return Ok(new
                {
                    Staff = new
                    {
                        staff.StaffId,
                        staff.FullName,
                        staff.Email,
                        staff.PhoneNumber,
                        staff.DepartmentName,
                        staff.Role,
                        staff.EmployeeId
                    },
                    Statistics = new
                    {
                        TotalAssigned = staff.TotalAssignments,
                        Completed = staff.CompletedAssignments,
                        Pending = staff.PendingAssignments,
                        CompletionRate = Math.Round(completionRate, 2),
                        ThisWeekCompleted = weeklyCompleted,
                        PerformanceScore = staff.PerformanceScore,
                        IsOverloaded = staff.PendingAssignments >= 5
                    },
                    ActiveAssignments = activeAssignments,
                    RecentCompleted = recentCompleted,
                    IsAvailable = staff.IsAvailableFlag
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // UPDATE STAFF AVAILABILITY
        // =====================================================
        [HttpPut]
        [Route("{staffId:guid}/availability")]
        public IHttpActionResult UpdateAvailability(Guid staffId, [FromBody] bool isAvailable)
        {
            try
            {
                var staff = db.StaffProfiles.Find(staffId);
                if (staff == null)
                    return NotFoundMsg("Staff not found");

                staff.IsAvailable = isAvailable;
                db.SaveChanges();

                return Ok(new
                {
                    Message = $"Staff availability updated to {(isAvailable ? "Available" : "Busy")}",
                    StaffId = staffId,
                    IsAvailable = isAvailable
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }

    // =====================================================
    // DTOs - FIXED AMBIGUITY
    // =====================================================

    public class StaffDto
    {
        public Guid StaffId { get; set; }
        public Guid UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public Guid? DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public Guid? ZoneId { get; set; }
        public string ZoneName { get; set; }
        public string Role { get; set; }
        public string EmployeeId { get; set; }
        public DateTime? HireDate { get; set; }
        public int TotalAssignments { get; set; }
        public int CompletedAssignments { get; set; }
        public int PendingAssignments { get; set; }
        public decimal AverageResolutionTime { get; set; }
        public decimal PerformanceScore { get; set; }
        public bool IsAvailable { get; set; }
    }

    public class AvailableStaffDto
    {
        public Guid StaffId { get; set; }
        public Guid UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public Guid? DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public string Role { get; set; }
        public int PendingAssignments { get; set; }
        public decimal PerformanceScore { get; set; }
        public decimal AverageResolutionTime { get; set; }
        public bool IsAvailable { get; set; }
    }

    public class DepartmentStaffDto
    {
        public Guid StaffId { get; set; }
        public Guid UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public Guid? DepartmentId { get; set; }
        public string Role { get; set; }
        public string EmployeeId { get; set; }
        public int TotalAssignments { get; set; }
        public int CompletedAssignments { get; set; }
        public int PendingAssignments { get; set; }
        public decimal PerformanceScore { get; set; }
        public bool IsAvailable { get; set; }  // Renamed to avoid ambiguity
    }

    public class StaffPerformanceDto
    {
        public Guid StaffId { get; set; }
        public Guid UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string DepartmentName { get; set; }
        public string Role { get; set; }
        public string EmployeeId { get; set; }
        public DateTime? HireDate { get; set; }
        public int TotalAssignments { get; set; }
        public int CompletedAssignments { get; set; }
        public int PendingAssignments { get; set; }
        public decimal AverageResolutionTime { get; set; }
        public decimal PerformanceScore { get; set; }
        public bool IsAvailableFlag { get; set; }
    }

    public class StaffDashboardDto
    {
        public Guid StaffId { get; set; }
        public Guid UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string DepartmentName { get; set; }
        public string Role { get; set; }
        public string EmployeeId { get; set; }
        public int TotalAssignments { get; set; }
        public int CompletedAssignments { get; set; }
        public int PendingAssignments { get; set; }
        public decimal PerformanceScore { get; set; }
        public bool IsAvailableFlag { get; set; }
    }

    public class SlaResultDto
    {
        public int Total { get; set; }
        public int MetSLA { get; set; }
    }

    public class MonthlyTrendDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public int Completed { get; set; }
        public double? AverageTime { get; set; }
    }

    public class ActiveAssignmentDto
    {
        public Guid AssignmentId { get; set; }
        public Guid ComplaintId { get; set; }
        public string Title { get; set; }
        public string Priority { get; set; }
        public DateTime AssignedAt { get; set; }
        public DateTime? ExpectedCompletionDate { get; set; }
        public bool IsOverdue { get; set; }
    }

    public class RecentCompletedDto
    {
        public Guid AssignmentId { get; set; }
        public Guid ComplaintId { get; set; }
        public string Title { get; set; }
        public DateTime CompletedAt { get; set; }
        public double ResolutionHours { get; set; }
    }
} 