using CCMW.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/dashboard")]
    public class DashboardController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // ========== CITIZEN DASHBOARD - PURE SQL VERSION ==========
        [HttpGet]
        [Route("citizen/{userId:guid}")]
        public IHttpActionResult GetCitizenDashboard(Guid userId)
        {
            try
            {
                // Get user with raw SQL
                var user = db.Database.SqlQuery<User>(@"
                    SELECT 
                        user_id as UserId,
                        user_type as UserType,
                        full_name as FullName,
                        email as Email,
                        password_hash as PasswordHash,
                        PhoneNumber,
                        zone_id as ZoneId,
                        is_active as IsActive,
                        created_at as CreatedAt,
                        CNIC,
                        Address,
                        ProfilePhotoUrl,
                        IsVerified,
                        UpdatedAt,
                        LastLogin
                    FROM Users 
                    WHERE user_id = @p0", userId).FirstOrDefault();

                if (user == null) return NotFound();

                // Get citizen profile with raw SQL
                var citizen = db.Database.SqlQuery<CitizenProfile>(@"
                    SELECT 
                        citizen_id as CitizenId,
                        user_id as UserId,
                        total_complaints as TotalComplaintsFiled,
                        approved_complaints as ApprovedComplaintsCount,
                        resolved_complaints as ResolvedComplaintsCount,
                        rejected_complaints as RejectedComplaintsCount,
                        contribution_score as ContributionScore,
                        leaderboard_rank as LeaderboardRank,
                        badge_level as BadgeLevel,
                        total_upvotes as TotalUpvotesReceived,
                        created_at as CreatedAt,
                        updated_at as UpdatedAt
                    FROM Citizen_Profile 
                    WHERE user_id = @p0", userId).FirstOrDefault();

                // Get zone with raw SQL if needed
                Zone zone = null;
                if (user.ZoneId.HasValue)
                {
                    zone = db.Database.SqlQuery<Zone>(@"
                        SELECT 
                            zone_id as ZoneId,
                            zone_number as ZoneNumber,
                            zone_name as ZoneName,
                            zone_code as ZoneCode,
                            boundary_coordinates as BoundaryCoordinates,
                            city as City,
                            province as Province,
                            total_area_sq_km as TotalAreaSqKm,
                            population as Population,
                            active_complaints_count as ActiveComplaintsCount,
                            total_complaints_count as TotalComplaintsCount,
                            performance_rating as PerformanceRating,
                            created_at as CreatedAt,
                            updated_at as UpdatedAt,
                            boundary_polygon as BoundaryPolygon,
                            center_latitude as CenterLatitude,
                            center_longitude as CenterLongitude,
                            color_code as ColorCode
                        FROM Zones 
                        WHERE zone_id = @p0", user.ZoneId.Value).FirstOrDefault();
                }

                // Get complaints with raw SQL
                var complaints = db.Database.SqlQuery<Complaint>(@"
                    SELECT TOP 10 
                        complaint_id as ComplaintId,
                        citizen_id as CitizenId,
                        category_id as CategoryId,
                        department_id as DepartmentId,
                        zone_id as ZoneId,
                        title as Title,
                        description as Description,
                        status as Status,
                        priority as Priority,
                        escalation_level as EscalationLevel,
                        created_at as CreatedAt,
                        resolved_at as ResolvedAt,
                        location_address as LocationAddress,
                        location_latitude as LocationLatitude,
                        location_longitude as LocationLongitude,
                        location_landmark as LocationLandmark,
                        assigned_to_id as AssignedToId,
                        assigned_at as AssignedAt,
                        ComplaintNumber,
                        ApprovedById,
                        RejectionReason,
                        StatusUpdatedAt,
                        UpvoteCount,
                        ViewCount,
                        IsDuplicate,
                        MergedIntoComplaintId,
                        UpdatedAt,
                        ClosedAt,
                        ExpectedResolutionDate,
                        IsOverdue,
                        SubmissionStatus,
                        CurrentStatus,
                        ResolutionNotes,
                        ReopenedAt  
                    FROM Complaints 
                    WHERE citizen_id = @p0 
                    ORDER BY created_at DESC", userId).ToList();

                // Statistics with raw SQL
                var totalComplaints = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM Complaints WHERE citizen_id = @p0", userId).FirstOrDefault();
                var approvedComplaints = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM Complaints WHERE citizen_id = @p0 AND SubmissionStatus = 1", userId).FirstOrDefault();
                var resolvedComplaints = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM Complaints WHERE citizen_id = @p0 AND CurrentStatus = 5", userId).FirstOrDefault();
                var pendingComplaints = db.Database.SqlQuery<int>(@"
                    SELECT COUNT(*) FROM Complaints 
                    WHERE citizen_id = @p0 AND CurrentStatus IN (0,1)", userId).FirstOrDefault();

                var upvotesReceived = db.Database.SqlQuery<int>(@"
                    SELECT COUNT(*) FROM Complaint_Upvotes 
                    WHERE complaint_id IN (SELECT complaint_id FROM Complaints WHERE citizen_id = @p0)", userId).FirstOrDefault();

                var stats = new
                {
                    TotalComplaints = totalComplaints,
                    ApprovedComplaints = approvedComplaints,
                    ResolvedComplaints = resolvedComplaints,
                    PendingComplaints = pendingComplaints,
                    UpvotesReceived = upvotesReceived,
                    LeaderboardRank = citizen?.LeaderboardRank ?? 0,
                    BadgeLevel = citizen?.BadgeLevel ?? "Newcomer"
                };

                var recentComplaints = complaints.Select(c => new
                {
                    c.ComplaintId,
                    c.ComplaintNumber,
                    c.Title,
                    CurrentStatus = c.CurrentStatus.ToString(),
                    c.CreatedAt,
                    c.LocationAddress
                }).ToList();

                return Ok(new
                {
                    User = new
                    {
                        user.UserId,
                        user.FullName,
                        user.Email,
                        user.PhoneNumber,
                        user.ProfilePhotoUrl,
                        user.IsVerified,
                        Zone = zone != null ? new
                        {
                            zone.ZoneId,
                            zone.ZoneName,
                            zone.ZoneNumber
                        } : null
                    },
                    Statistics = stats,
                    RecentComplaints = recentComplaints
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: " + ex.ToString());
                return InternalServerError(ex);
            }
        }

        [HttpGet]
        [Route("staff/{staffId:guid}")]
        public IHttpActionResult GetStaffDashboard(Guid staffId)
        {
            try
            {
                // Get staff with raw SQL
                var staff = db.Database.SqlQuery<StaffProfile>(@"
            SELECT 
                staff_id as StaffId,
                user_id as UserId,
                department_id as DepartmentId,
                zone_id as ZoneId,
                role as Role,
                employee_id as EmployeeId,
                hire_date as HireDate,
                total_assignments as TotalAssignments,
                completed_assignments as CompletedAssignments,
                pending_assignments as PendingAssignments,
                average_resolution_time as AverageResolutionTime,
                performance_score as PerformanceScore,
                is_available as IsAvailable
            FROM Staff_Profile 
            WHERE staff_id = @p0", staffId).FirstOrDefault();

                if (staff == null) return NotFound();

                // Get user info with raw SQL
                var user = db.Database.SqlQuery<User>(@"
            SELECT 
                user_id as UserId,
                user_type as UserType,
                full_name as FullName,
                email as Email,
                password_hash as PasswordHash,
                PhoneNumber,
                zone_id as ZoneId,
                is_active as IsActive,
                created_at as CreatedAt,
                CNIC,
                Address,
                ProfilePhotoUrl,
                IsVerified,
                UpdatedAt,
                LastLogin
            FROM Users 
            WHERE user_id = @p0", staff.UserId).FirstOrDefault();

                // Get department name if exists
                string departmentName = null;
                if (staff.DepartmentId.HasValue)
                {
                    departmentName = db.Database.SqlQuery<string>(@"
                SELECT department_name 
                FROM Departments 
                WHERE department_id = @p0", staff.DepartmentId.Value).FirstOrDefault();
                }

                // Get active assignments with raw SQL - FIXED COLUMN NAMES
                var assignments = db.Database.SqlQuery<AssignmentDto>(@"
            SELECT 
                a.assignment_id as AssignmentId,
                a.complaint_id as ComplaintId,
                a.assigned_at as AssignedAt,
                a.ExpectedCompletionDate as ExpectedCompletionDate,    -- Fixed
                a.AcceptedAt as AcceptedAt,                             -- Fixed
                a.StartedAt as StartedAt,                               -- Fixed
                a.CompletedAt as CompletedAt,                           -- Fixed
                c.title as ComplaintTitle,
                c.priority as ComplaintPriority,
                c.location_address as LocationAddress,
                c.CurrentStatus
            FROM ComplaintAssignments a
            INNER JOIN Complaints c ON a.complaint_id = c.complaint_id
            WHERE a.staff_id = @p0 AND a.IsActive = 1
            ORDER BY a.assigned_at DESC", staffId).ToList();

                // Statistics with raw SQL
                var totalAssignments = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM ComplaintAssignments WHERE staff_id = @p0", staffId).FirstOrDefault();
                var completedAssignments = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM ComplaintAssignments WHERE staff_id = @p0 AND CompletedAt IS NOT NULL", staffId).FirstOrDefault();
                var pendingAssignments = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM ComplaintAssignments WHERE staff_id = @p0 AND CompletedAt IS NULL AND IsActive = 1", staffId).FirstOrDefault();

                var avgResolutionTime = db.Database.SqlQuery<double?>(@"
            SELECT AVG(CAST(DATEDIFF(HOUR, assigned_at, CompletedAt) AS FLOAT))
            FROM ComplaintAssignments 
            WHERE staff_id = @p0 AND CompletedAt IS NOT NULL", staffId).FirstOrDefault() ?? 0;

                var stats = new
                {
                    TotalAssignments = totalAssignments,
                    CompletedAssignments = completedAssignments,
                    PendingAssignments = pendingAssignments,
                    AverageResolutionTime = Math.Round(avgResolutionTime, 2),
                    PerformanceScore = totalAssignments > 0 ? Math.Round((double)completedAssignments / totalAssignments * 100, 2) : 0,
                    IsAvailable = staff.IsAvailable
                };

                return Ok(new
                {
                    Staff = new
                    {
                        staff.StaffId,
                        staff.UserId,
                        FullName = user?.FullName ?? "",
                        Email = user?.Email ?? "",
                        PhoneNumber = user?.PhoneNumber ?? "",
                        staff.Role,
                        DepartmentName = departmentName,
                        staff.ZoneId,
                        staff.EmployeeId,
                        staff.HireDate
                    },
                    Statistics = stats,
                    ActiveAssignments = assignments
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("STAFF DASHBOARD ERROR: " + ex.ToString());
                return InternalServerError(ex);
            }
        }


        [HttpGet]
        [Route("department/{departmentId:guid}")]
        public IHttpActionResult GetDepartmentDashboard(Guid departmentId)
        {
            try
            {
                // Get department with raw SQL - ADDED ALL MISSING COLUMNS
                var department = db.Database.SqlQuery<Department>(@"
            SELECT 
                department_id as DepartmentId,
                department_name as DepartmentName,
                department_code as DepartmentCode,
                privatization_status as PrivatizationStatus,
                contractor_id as ContractorId,
                performance_score as PerformanceScore,
                active_complaints_count as ActiveComplaintsCount,
                resolved_complaints_count as ResolvedComplaintsCount,
                total_complaints_count as TotalComplaintsCount,
                average_resolution_time_days as AverageResolutionTimeDays,
                description as Description,              -- ADDED: Missing column
                head_admin_id as HeadAdminId,             -- ADDED: Missing column
                performance_rating as PerformanceRating,   -- ADDED: Missing column
                is_active as IsActive,                     -- ADDED: Missing column
                created_at as CreatedAt,                   -- ADDED: Missing column
                updated_at as UpdatedAt                    -- ADDED: Missing column
            FROM Departments 
            WHERE department_id = @p0", departmentId).FirstOrDefault();

                if (department == null) return NotFound();

                // Department statistics with raw SQL
                var totalComplaints = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM Complaints WHERE department_id = @p0", departmentId).FirstOrDefault();
                var activeComplaints = db.Database.SqlQuery<int>(@"
            SELECT COUNT(*) FROM Complaints 
            WHERE department_id = @p0 AND CurrentStatus IN (0,1,3,4)", departmentId).FirstOrDefault();

                var pendingApprovals = db.Database.SqlQuery<int>(@"
            SELECT COUNT(*) FROM Complaints 
            WHERE department_id = @p0 AND SubmissionStatus = 0", departmentId).FirstOrDefault();

                var resolvedThisMonth = db.Database.SqlQuery<int>(@"
            SELECT COUNT(*) FROM Complaints 
            WHERE department_id = @p0 
            AND CurrentStatus = 5 
            AND MONTH(resolved_at) = MONTH(GETDATE())
            AND YEAR(resolved_at) = YEAR(GETDATE())", departmentId).FirstOrDefault();

                var stats = new
                {
                    TotalComplaints = totalComplaints,
                    ActiveComplaints = activeComplaints,
                    PendingApprovals = pendingApprovals,
                    ResolvedThisMonth = resolvedThisMonth,
                    PerformanceScore = department.PerformanceScore,
                    AverageResolutionTimeDays = department.AverageResolutionTimeDays
                };

                // Staff performance with raw SQL
                var staffPerformance = db.Database.SqlQuery<StaffPerformanceDto>(@"
            SELECT TOP 5
                s.staff_id as StaffId,
                u.full_name as UserName,
                s.completed_assignments as CompletedAssignments,
                s.pending_assignments as PendingAssignments,
                s.performance_score as PerformanceScore,
                s.is_available as IsAvailable
            FROM Staff_Profile s
            INNER JOIN Users u ON s.user_id = u.user_id
            WHERE s.department_id = @p0
            ORDER BY s.performance_score DESC", departmentId).ToList();

                // Return more department details including the newly added fields
                return Ok(new
                {
                    Department = new
                    {
                        department.DepartmentId,
                        department.DepartmentName,
                        department.DepartmentCode,
                        department.PrivatizationStatus,
                        department.ContractorId,
                        department.Description,              // NOW INCLUDED
                        department.HeadAdminId,               // NOW INCLUDED
                        department.PerformanceRating,         // NOW INCLUDED
                        department.IsActive,                   // NOW INCLUDED
                        department.CreatedAt,                  // NOW INCLUDED
                        department.UpdatedAt                    // NOW INCLUDED
                    },
                    Statistics = stats,
                    TopStaff = staffPerformance
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("DEPARTMENT DASHBOARD ERROR: " + ex.ToString());
                return InternalServerError(ex);
            }
        }


        [HttpGet]
        [Route("admin")]
        public IHttpActionResult GetAdminDashboard()
        {
            try
            {
                // System-wide statistics with raw SQL
                var totalUsers = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM Users").FirstOrDefault();
                var totalCitizens = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM Users WHERE user_type = 'Citizen'").FirstOrDefault();
                var totalStaff = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM Staff_Profile").FirstOrDefault();
                var totalContractors = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM Contractors WHERE is_active = 1").FirstOrDefault();
                var totalComplaints = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM Complaints").FirstOrDefault();
                var resolvedComplaints = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM Complaints WHERE CurrentStatus = 5").FirstOrDefault();
                var activeComplaints = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM Complaints WHERE CurrentStatus NOT IN (5,8)").FirstOrDefault();
                var pendingApprovals = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM Complaints WHERE SubmissionStatus = 0").FirstOrDefault();
                var overdueComplaints = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM Complaints WHERE IsOverdue = 1").FirstOrDefault();
                var totalDepartments = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM Departments").FirstOrDefault();
                var totalZones = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM Zones").FirstOrDefault();

                var stats = new
                {
                    TotalUsers = totalUsers,
                    TotalCitizens = totalCitizens,
                    TotalStaff = totalStaff,
                    TotalContractors = totalContractors,
                    TotalComplaints = totalComplaints,
                    ResolvedComplaints = resolvedComplaints,
                    ActiveComplaints = activeComplaints,
                    PendingApprovals = pendingApprovals,
                    OverdueComplaints = overdueComplaints,
                    TotalDepartments = totalDepartments,
                    TotalZones = totalZones,
                    AverageResponseTime = CalculateAverageResponseTime()
                };

                // Department performance with raw SQL
                var deptPerformance = db.Database.SqlQuery<DepartmentPerformanceDto>(@"
                    SELECT TOP 5
                        department_id as DepartmentId,
                        department_name as DepartmentName,
                        performance_score as PerformanceScore,
                        active_complaints_count as ActiveComplaintsCount,
                        resolved_complaints_count as ResolvedComplaintsCount,
                        total_complaints_count as TotalComplaintsCount,
                        ISNULL(privatization_status, 'Public') as PrivatizationStatus
                    FROM Departments
                    ORDER BY performance_score DESC").ToList();

                // Zone performance with raw SQL
                var zonePerformance = db.Database.SqlQuery<ZonePerformanceDto>(@"
                    SELECT TOP 5
                        zone_id as ZoneId,
                        zone_name as ZoneName,
                        zone_number as ZoneNumber,
                        active_complaints_count as ActiveComplaintsCount,
                        total_complaints_count as TotalComplaintsCount,
                        performance_rating as PerformanceRating
                    FROM Zones
                    ORDER BY active_complaints_count DESC").ToList();

                // Contractor performance with raw SQL
                var contractorPerformance = db.Database.SqlQuery<ContractorPerformanceDto>(@"
                    SELECT TOP 5
                        c.contractor_id as ContractorId,
                        c.company_name as CompanyName,
                        c.performance_score as PerformanceScore,
                        c.sla_compliance_rate as SLAComplianceRate,
                        (SELECT COUNT(*) FROM ContractorZoneAssignments WHERE contractor_id = c.contractor_id AND is_active = 1) as AssignedZones
                    FROM Contractors c
                    WHERE c.is_active = 1
                    ORDER BY c.performance_score DESC").ToList();

                return Ok(new
                {
                    Statistics = stats,
                    TopDepartments = deptPerformance,
                    TopZones = zonePerformance,
                    TopContractors = contractorPerformance
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ADMIN DASHBOARD ERROR: " + ex.ToString());
                return InternalServerError(ex);
            }
        }


        [HttpGet]
        [Route("contractor/{contractorId:guid}")]
        public IHttpActionResult GetContractorDashboard(Guid contractorId)
        {
            try
            {
                // Get contractor with raw SQL - FIXED COLUMN NAMES
                var contractor = db.Database.SqlQuery<Contractor>(@"
            SELECT 
                contractor_id as ContractorId,
                company_name as CompanyName,
                company_registration_number as CompanyRegistrationNumber,
                contact_person_name as ContactPersonName,
                contact_person_phone as ContactPersonPhone,
                contact_email as ContactPersonEmail,        -- FIXED: was contact_person_email
                company_address as CompanyAddress,
                contract_start as ContractStartDate,        -- FIXED: was contract_start_date
                contract_end as ContractEndDate,            -- FIXED: was contract_end_date
                contract_value as ContractValue,
                performance_bond as PerformanceBond,
                performance_score as PerformanceScore,
                sla_compliance_rate as SLAComplianceRate,
                is_active as IsActive,
                created_at as CreatedAt,
                updated_at as UpdatedAt
            FROM Contractors 
            WHERE contractor_id = @p0", contractorId).FirstOrDefault();

                if (contractor == null) return NotFound();

                // Get assigned zones with raw SQL
                var assignedZones = db.Database.SqlQuery<AssignedZoneDto>(@"
            SELECT 
                cza.assignment_id as AssignmentId,
                cza.zone_id as ZoneId,
                z.zone_name as ZoneName,
                z.zone_number as ZoneNumber,
                cza.service_type as ServiceType,
                cza.contract_start as ContractStart,
                cza.contract_end as ContractEnd,
                cza.contract_value as ContractValue,
                cza.performance_bond as PerformanceBond,
                cza.assigned_date as AssignedDate,
                (SELECT COUNT(*) FROM Complaints WHERE zone_id = cza.zone_id AND CurrentStatus NOT IN (5,8)) as ActiveComplaints,
                (SELECT COUNT(*) FROM Complaints WHERE zone_id = cza.zone_id AND CurrentStatus = 5) as ResolvedComplaints
            FROM ContractorZoneAssignments cza
            INNER JOIN Zones z ON cza.zone_id = z.zone_id
            WHERE cza.contractor_id = @p0 AND cza.is_active = 1", contractorId).ToList();

                // Get performance history with raw SQL
                var performanceHistory = db.Database.SqlQuery<PerformanceHistoryDto>(@"
            SELECT TOP 6
                history_id as HistoryId,
                review_period_start as ReviewPeriodStart,
                review_period_end as ReviewPeriodEnd,
                complaints_assigned as ComplaintsAssigned,
                complaints_resolved as ComplaintsResolved,
                resolved_on_time as ResolvedOnTime,
                sla_compliance_rate as SlaComplianceRate,
                citizen_rating as CitizenRating,
                performance_score as PerformanceScore,
                penalties_amount as PenaltiesAmount,
                bonus_amount as BonusAmount
            FROM ContractorPerformanceHistory
            WHERE contractor_id = @p0
            ORDER BY review_period_end DESC", contractorId).ToList();

                // Statistics
                var stats = new
                {
                    TotalZones = assignedZones.Count,
                    TotalActiveComplaints = assignedZones.Sum(z => z.ActiveComplaints),
                    TotalResolvedComplaints = assignedZones.Sum(z => z.ResolvedComplaints),
                    AveragePerformanceScore = contractor.PerformanceScore,
                    SLAComplianceRate = contractor.SLAComplianceRate,
                    ContractValue = contractor.ContractValue,
                    PerformanceBond = contractor.PerformanceBond,
                    ContractStart = contractor.ContractStart,
                    ContractEnd = contractor.ContractEnd,
                    DaysUntilContractEnd = (contractor.ContractEnd - DateTime.Now).Days
                };

                return Ok(new
                {
                    Contractor = new
                    {
                        contractor.ContractorId,
                        contractor.CompanyName,
                        contractor.CompanyRegistrationNumber,
                        contractor.ContactPersonName,
                        contractor.ContactPersonPhone,
                        ContactPersonEmail = contractor.ContactEmail,  // Property name might be ContactPersonEmail
                        contractor.CompanyAddress,
                        contractor.IsActive,
                        contractor.CreatedAt,
                        contractor.UpdatedAt
                    },
                    Statistics = stats,
                    AssignedZones = assignedZones,
                    PerformanceHistory = performanceHistory
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("CONTRACTOR DASHBOARD ERROR: " + ex.ToString());
                return InternalServerError(ex);
            }
        }

        private double CalculateAverageResponseTime()
        {
            var result = db.Database.SqlQuery<double?>(@"
                SELECT AVG(CAST(DATEDIFF(HOUR, created_at, resolved_at) AS FLOAT))
                FROM Complaints 
                WHERE resolved_at IS NOT NULL").FirstOrDefault();

            return Math.Round(result ?? 0, 2);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }





        public class AssignmentDto
        {
            public Guid AssignmentId { get; set; }
            public Guid ComplaintId { get; set; }
            public DateTime AssignedAt { get; set; }
            public DateTime? ExpectedCompletionDate { get; set; }
            public DateTime? AcceptedAt { get; set; }
            public DateTime? StartedAt { get; set; }
            public DateTime? CompletedAt { get; set; }
            public string ComplaintTitle { get; set; }
            public string ComplaintPriority { get; set; }
            public string LocationAddress { get; set; }
            public int CurrentStatus { get; set; }
        }

        public class StaffPerformanceDto
        {
            public Guid StaffId { get; set; }
            public string UserName { get; set; }
            public int CompletedAssignments { get; set; }
            public int PendingAssignments { get; set; }
            public decimal PerformanceScore { get; set; }
            public bool IsAvailable { get; set; }
        }

        public class DepartmentPerformanceDto
        {
            public Guid DepartmentId { get; set; }
            public string DepartmentName { get; set; }
            public decimal PerformanceScore { get; set; }
            public int ActiveComplaintsCount { get; set; }
            public int ResolvedComplaintsCount { get; set; }
            public int TotalComplaintsCount { get; set; }
            public string PrivatizationStatus { get; set; }
        }

        public class ZonePerformanceDto
        {
            public Guid ZoneId { get; set; }
            public string ZoneName { get; set; }
            public int ZoneNumber { get; set; }
            public int ActiveComplaintsCount { get; set; }
            public int TotalComplaintsCount { get; set; }
            public string PerformanceRating { get; set; }
        }

        public class ContractorPerformanceDto
        {
            public Guid ContractorId { get; set; }
            public string CompanyName { get; set; }
            public decimal PerformanceScore { get; set; }
            public decimal SLAComplianceRate { get; set; }
            public int AssignedZones { get; set; }
        }

        public class AssignedZoneDto
        {
            public Guid AssignmentId { get; set; }
            public Guid ZoneId { get; set; }
            public string ZoneName { get; set; }
            public int ZoneNumber { get; set; }
            public string ServiceType { get; set; }
            public DateTime ContractStart { get; set; }
            public DateTime ContractEnd { get; set; }
            public decimal ContractValue { get; set; }
            public decimal PerformanceBond { get; set; }
            public DateTime AssignedDate { get; set; }
            public int ActiveComplaints { get; set; }
            public int ResolvedComplaints { get; set; }
        }

        public class PerformanceHistoryDto
        {
            public Guid HistoryId { get; set; }
            public DateTime ReviewPeriodStart { get; set; }
            public DateTime ReviewPeriodEnd { get; set; }
            public int ComplaintsAssigned { get; set; }
            public int ComplaintsResolved { get; set; }
            public int ResolvedOnTime { get; set; }
            public decimal SlaComplianceRate { get; set; }
            public decimal CitizenRating { get; set; }
            public decimal PerformanceScore { get; set; }
            public decimal PenaltiesAmount { get; set; }
            public decimal BonusAmount { get; set; }
        }
    }
}