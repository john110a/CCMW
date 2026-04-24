using CCMW.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/departments")]
    public class DepartmentController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // Helper method for NotFound with message
        private IHttpActionResult NotFound(string message)
        {
            return Content(HttpStatusCode.NotFound, new { error = message });
        }

        // =====================================================
        // GET ALL DEPARTMENTS - FIXED (No circular references)
        // =====================================================
        [HttpGet]
        [Route("")]
        public IHttpActionResult GetAllDepartments()
        {
            var departments = db.Departments
                .Where(d => d.IsActive)
                .Select(d => new
                {
                    d.DepartmentId,
                    d.DepartmentName,
                    d.DepartmentCode,
                    d.Description,
                    d.PrivatizationStatus,
                    d.ContractorId,
                    d.HeadAdminId,
                    d.PerformanceScore,
                    d.PerformanceRating,
                    d.ActiveComplaintsCount,
                    d.ResolvedComplaintsCount,
                    d.TotalComplaintsCount,
                    d.AverageResolutionTimeDays,
                    d.IsActive,
                    d.CreatedAt,
                    d.UpdatedAt,
                    // Only include needed Contractor fields
                    Contractor = d.Contractor != null ? new
                    {
                        d.Contractor.ContractorId,
                        d.Contractor.CompanyName,
                        d.Contractor.PerformanceScore,
                        d.Contractor.SLAComplianceRate,
                        d.Contractor.IsActive
                    } : null,
                    // Only include needed ZoneDepartment fields
                    ZoneDepartments = d.ZoneDepartments.Select(zd => new
                    {
                        zd.ZoneDeptId,
                        zd.ZoneId,
                        zd.DepartmentId,
                        zd.StaffCount,
                        zd.ActiveComplaintsCount,
                        zd.IsActive
                    })
                })
                .ToList();

            return Ok(departments);
        }

        // =====================================================
        // GET DEPARTMENT BY ID - FIXED
        // =====================================================
        [HttpGet]
        [Route("{id:guid}")]
        public IHttpActionResult GetDepartmentById(Guid id)
        {
            var department = db.Departments
                .Where(d => d.DepartmentId == id)
                .Select(d => new
                {
                    d.DepartmentId,
                    d.DepartmentName,
                    d.DepartmentCode,
                    d.Description,
                    d.PrivatizationStatus,
                    d.ContractorId,
                    d.HeadAdminId,
                    d.PerformanceScore,
                    d.PerformanceRating,
                    d.ActiveComplaintsCount,
                    d.ResolvedComplaintsCount,
                    d.TotalComplaintsCount,
                    d.AverageResolutionTimeDays,
                    d.IsActive,
                    d.CreatedAt,
                    d.UpdatedAt,
                    Contractor = d.Contractor != null ? new
                    {
                        d.Contractor.ContractorId,
                        d.Contractor.CompanyName,
                        d.Contractor.PerformanceScore,
                        d.Contractor.SLAComplianceRate
                    } : null,
                    Staffs = d.Staffs.Select(s => new
                    {
                        s.StaffId,
                        s.UserId,
                        s.Role,
                        s.EmployeeId,
                        s.PerformanceScore,
                        s.IsAvailable
                    }),
                    ComplaintsCount = d.Complaints.Count()
                })
                .FirstOrDefault();

            if (department == null)
                return NotFound("Department not found");

            return Ok(department);
        }

        // =====================================================
        // CREATE DEPARTMENT
        // =====================================================
        [HttpPost]
        [Route("create")]
        public IHttpActionResult CreateDepartment([FromBody] Department department)
        {
            if (department == null)
                return BadRequest("Department data is required.");

            department.DepartmentId = Guid.NewGuid();
            department.CreatedAt = DateTime.Now;
            department.UpdatedAt = DateTime.Now;
            department.IsActive = true;

            db.Departments.Add(department);
            db.SaveChanges();

            return Ok(new
            {
                Message = "Department created successfully",
                DepartmentId = department.DepartmentId
            });
        }

        // =====================================================
        // UPDATE DEPARTMENT
        // =====================================================
        [HttpPut]
        [Route("{id:guid}/update")]
        public IHttpActionResult UpdateDepartment(Guid id, [FromBody] Department updated)
        {
            var department = db.Departments.FirstOrDefault(d => d.DepartmentId == id);
            if (department == null)
                return NotFound("Department not found");

            if (!string.IsNullOrEmpty(updated.DepartmentName))
                department.DepartmentName = updated.DepartmentName;

            if (!string.IsNullOrEmpty(updated.Description))
                department.Description = updated.Description;

            if (!string.IsNullOrEmpty(updated.PrivatizationStatus))
                department.PrivatizationStatus = updated.PrivatizationStatus;

            if (updated.ContractorId != null && updated.ContractorId != Guid.Empty)
                department.ContractorId = updated.ContractorId;

            if (updated.HeadAdminId != null && updated.HeadAdminId != Guid.Empty)
                department.HeadAdminId = updated.HeadAdminId;

            department.UpdatedAt = DateTime.Now;

            db.SaveChanges();
            return Ok("Department updated successfully");
        }

        // =====================================================
        // ACTIVATE DEPARTMENT
        // =====================================================
        [HttpPut]
        [Route("{id:guid}/activate")]
        public IHttpActionResult ActivateDepartment(Guid id)
        {
            var department = db.Departments.FirstOrDefault(d => d.DepartmentId == id);
            if (department == null)
                return NotFound("Department not found");

            department.IsActive = true;
            department.UpdatedAt = DateTime.Now;
            db.SaveChanges();

            return Ok("Department activated");
        }

        // =====================================================
        // DEACTIVATE DEPARTMENT
        // =====================================================
        [HttpPut]
        [Route("{id:guid}/deactivate")]
        public IHttpActionResult DeactivateDepartment(Guid id)
        {
            var department = db.Departments.FirstOrDefault(d => d.DepartmentId == id);
            if (department == null)
                return NotFound("Department not found");

            department.IsActive = false;
            department.UpdatedAt = DateTime.Now;
            db.SaveChanges();

            return Ok("Department deactivated");
        }

        // =====================================================
        // GET PENDING COMPLAINTS FOR DEPARTMENT
        // =====================================================
        [HttpGet]
        [Route("{departmentId:guid}/complaints/pending")]
        public IHttpActionResult GetPendingComplaints(Guid departmentId)
        {
            var complaints = db.Complaints
                .Where(c => c.DepartmentId == departmentId && c.SubmissionStatus == SubmissionStatus.PendingApproval)
                .Select(c => new
                {
                    c.ComplaintId,
                    c.ComplaintNumber,
                    c.Title,
                    c.Description,
                    c.Priority,
                    c.CreatedAt,
                    c.LocationAddress,
                    HasPhotos = c.ComplaintPhotos.Any()
                })
                .OrderByDescending(c => c.CreatedAt)
                .ToList();

            return Ok(complaints);
        }

        // =====================================================
        // APPROVE COMPLAINT
        // =====================================================
        [HttpPost]
        [Route("complaints/{complaintId:guid}/approve")]
        public IHttpActionResult ApproveComplaint(Guid complaintId, [FromUri] Guid adminId)
        {
            var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == complaintId);
            if (complaint == null)
                return NotFound("Complaint not found");

            var oldStatus = complaint.CurrentStatus.ToString();

            complaint.SubmissionStatus = SubmissionStatus.Approved;
            complaint.CurrentStatus = ComplaintStatus.Approved;
            complaint.ApprovedById = adminId;
            complaint.UpdatedAt = DateTime.Now;

            db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
            {
                HistoryId = Guid.NewGuid(),
                ComplaintId = complaintId,
                PreviousStatus = oldStatus,
                NewStatus = ComplaintStatus.Approved.ToString(),
                ChangedById = adminId,
                ChangedAt = DateTime.Now
            });

            db.SaveChanges();
            return Ok("Complaint approved successfully");
        }

        // =====================================================
        // REJECT COMPLAINT
        // =====================================================
        [HttpPost]
        [Route("complaints/{complaintId:guid}/reject")]
        public IHttpActionResult RejectComplaint(
            Guid complaintId,
            [FromUri] Guid adminId,
            [FromBody] string rejectionReason)
        {
            if (string.IsNullOrEmpty(rejectionReason))
                return BadRequest("Rejection reason is required.");

            var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == complaintId);
            if (complaint == null)
                return NotFound("Complaint not found");

            var oldStatus = complaint.CurrentStatus.ToString();

            complaint.SubmissionStatus = SubmissionStatus.Rejected;
            complaint.CurrentStatus = ComplaintStatus.Rejected;
            complaint.RejectionReason = rejectionReason;
            complaint.ApprovedById = adminId;
            complaint.UpdatedAt = DateTime.Now;

            db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
            {
                HistoryId = Guid.NewGuid(),
                ComplaintId = complaintId,
                PreviousStatus = oldStatus,
                NewStatus = ComplaintStatus.Rejected.ToString(),
                ChangedById = adminId,
                ChangeReason = rejectionReason,
                ChangedAt = DateTime.Now
            });

            db.SaveChanges();
            return Ok("Complaint rejected successfully");
        }

        // =====================================================
        // GET DEPARTMENT STATISTICS
        // =====================================================
        [HttpGet]
        [Route("{id:guid}/stats")]
        public IHttpActionResult GetDepartmentStats(Guid id)
        {
            var department = db.Departments.FirstOrDefault(d => d.DepartmentId == id);
            if (department == null)
                return NotFound("Department not found");

            var totalComplaints = db.Complaints.Count(c => c.DepartmentId == id);
            var resolvedComplaints = db.Complaints.Count(c =>
                c.DepartmentId == id && c.CurrentStatus == ComplaintStatus.Resolved);

            return Ok(new
            {
                department.DepartmentName,
                TotalComplaints = totalComplaints,
                ResolvedComplaints = resolvedComplaints,
                department.ActiveComplaintsCount,
                department.AverageResolutionTimeDays,
                PerformanceScore = department.PerformanceScore,
                department.PerformanceRating
            });
        }

        // =====================================================
        // GET DEPARTMENT STAFF (Already fixed with DTO)
        // =====================================================
        [HttpGet]
        [Route("{departmentId:guid}/staff")]
        public IHttpActionResult GetDepartmentStaff(Guid departmentId)
        {
            try
            {
                var sql = @"
                    SELECT 
                        s.staff_id as StaffId,
                        s.user_id as UserId,
                        u.full_name as FullName,
                        u.email as Email,
                        u.PhoneNumber,
                        s.department_id as DepartmentId,
                        s.role as Role,
                        s.employee_id as EmployeeId,
                        s.total_assignments as TotalAssignments,
                        s.completed_assignments as CompletedAssignments,
                        s.pending_assignments as PendingAssignments,
                        s.performance_score as PerformanceScore,
                        s.is_available as IsAvailable
                    FROM Staff_Profile s
                    INNER JOIN Users u ON s.user_id = u.user_id
                    WHERE s.department_id = @p0
                    ORDER BY s.performance_score DESC
                ";

                var staff = db.Database.SqlQuery<DepartmentStaffDto>(sql, departmentId).ToList();

                return Ok(new
                {
                    DepartmentId = departmentId,
                    StaffCount = staff.Count,
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
        // GET DEPARTMENT CONTRACTORS - FIXED
        // =====================================================
        [HttpGet]
        [Route("{departmentId:guid}/contractors")]
        public IHttpActionResult GetDepartmentContractors(Guid departmentId)
        {
            try
            {
                var zoneIds = db.ZoneDepartments
                    .Where(zd => zd.DepartmentId == departmentId && zd.IsActive)
                    .Select(zd => zd.ZoneId)
                    .Distinct()
                    .ToList();

                if (zoneIds.Count == 0)
                {
                    return Ok(new object[0]);
                }

                var contractors = db.ContractorZoneAssignments
                    .Where(cza => zoneIds.Contains(cza.ZoneId) && cza.IsActive)
                    .Select(cza => new
                    {
                        cza.Contractor.ContractorId,
                        cza.Contractor.CompanyName,
                        cza.Contractor.PerformanceScore,
                        cza.Contractor.SLAComplianceRate,
                        cza.ServiceType,
                        cza.ContractStart,
                        cza.ContractEnd,
                        Zone = new { cza.Zone.ZoneId, cza.Zone.ZoneName }
                    })
                    .ToList();

                return Ok(contractors);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // ASSIGN COMPLAINT TO DEPARTMENT STAFF
        // =====================================================
        [HttpPost]
        [Route("complaints/{complaintId:guid}/assign-to-staff")]
        public IHttpActionResult AssignToDepartmentStaff(Guid complaintId, [FromBody] StaffAssignmentRequest request)
        {
            try
            {
                var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == complaintId);
                if (complaint == null)
                    return NotFound("Complaint not found");

                if (complaint.DepartmentId == null || complaint.DepartmentId == Guid.Empty)
                {
                    var category = db.ComplaintCategories.Find(complaint.CategoryId);
                    if (category != null && category.DepartmentId != null && category.DepartmentId != Guid.Empty)
                    {
                        complaint.DepartmentId = category.DepartmentId;
                        db.SaveChanges();
                    }
                    else
                    {
                        return BadRequest("Department not assigned to this category");
                    }
                }

                var bestStaff = db.StaffProfiles
                    .Where(s => s.DepartmentId == complaint.DepartmentId
                                && s.IsAvailable
                                && s.PendingAssignments < 5)
                    .OrderBy(s => s.PendingAssignments)
                    .ThenByDescending(s => s.PerformanceScore)
                    .FirstOrDefault();

                if (bestStaff == null)
                    return BadRequest("No available staff in this department");

                var assignment = new ComplaintAssignment
                {
                    AssignmentId = Guid.NewGuid(),
                    ComplaintId = complaint.ComplaintId,
                    AssignedToId = bestStaff.StaffId,
                    AssignedById = request.AssignedById,
                    AssignedAt = DateTime.Now,
                    AssignmentType = "Auto",
                    AssignmentNotes = "Auto-assigned based on workload",
                    IsActive = true
                };

                db.ComplaintAssignments.Add(assignment);
                complaint.AssignedToId = bestStaff.StaffId;
                complaint.AssignedAt = DateTime.Now;
                complaint.CurrentStatus = ComplaintStatus.Assigned;

                bestStaff.TotalAssignments += 1;
                bestStaff.PendingAssignments += 1;

                db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                {
                    HistoryId = Guid.NewGuid(),
                    ComplaintId = complaint.ComplaintId,
                    PreviousStatus = ComplaintStatus.Approved.ToString(),
                    NewStatus = ComplaintStatus.Assigned.ToString(),
                    ChangedById = request.AssignedById,
                    ChangedAt = DateTime.Now,
                    Notes = "Assigned to " + bestStaff.User.FullName
                });

                db.SaveChanges();

                return Ok(new
                {
                    Message = "Complaint assigned successfully",
                    AssignmentId = assignment.AssignmentId,
                    AssignedTo = bestStaff.User.FullName,
                    StaffId = bestStaff.StaffId
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET DEPARTMENT COMPLAINTS (Already fixed with DTO)
        // =====================================================
        [HttpGet]
        [Route("{departmentId:guid}/complaints")]
        public IHttpActionResult GetDepartmentComplaints(
            Guid departmentId,
            [FromUri] int page = 1,
            [FromUri] int pageSize = 20,
            [FromUri] string status = null)
        {
            try
            {
                var sql = @"
                    SELECT 
                        c.complaint_id as ComplaintId,
                        c.ComplaintNumber,
                        c.title as Title,
                        c.description as Description,
                        c.priority as Priority,
                        c.CurrentStatus,
                        c.created_at as CreatedAt,
                        cat.category_name as CategoryName,
                        z.zone_name as ZoneName,
                        c.location_address as LocationAddress,
                        c.UpvoteCount
                    FROM Complaints c
                    LEFT JOIN Complaint_Categories cat ON c.category_id = cat.category_id
                    LEFT JOIN Zones z ON c.zone_id = z.zone_id
                    WHERE c.department_id = @p0
                ";

                if (!string.IsNullOrEmpty(status))
                {
                    int statusValue = GetStatusValue(status);
                    sql += " AND c.CurrentStatus = @p1";
                    sql += " ORDER BY c.created_at DESC OFFSET @p2 ROWS FETCH NEXT @p3 ROWS ONLY";

                    var complaints = db.Database.SqlQuery<ComplaintDto>(sql, departmentId, statusValue, (page - 1) * pageSize, pageSize).ToList();
                    var totalCount = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM Complaints WHERE department_id = @p0 AND CurrentStatus = @p1", departmentId, statusValue).FirstOrDefault();

                    return Ok(new
                    {
                        TotalCount = totalCount,
                        Page = page,
                        PageSize = pageSize,
                        TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                        Complaints = complaints
                    });
                }
                else
                {
                    sql += " ORDER BY c.created_at DESC OFFSET @p1 ROWS FETCH NEXT @p2 ROWS ONLY";

                    var complaints = db.Database.SqlQuery<ComplaintDto>(sql, departmentId, (page - 1) * pageSize, pageSize).ToList();
                    var totalCount = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM Complaints WHERE department_id = @p0", departmentId).FirstOrDefault();

                    return Ok(new
                    {
                        TotalCount = totalCount,
                        Page = page,
                        PageSize = pageSize,
                        TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                        Complaints = complaints
                    });
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET DEPARTMENT PERFORMANCE DASHBOARD
        // =====================================================
        [HttpGet]
        [Route("{departmentId:guid}/performance")]
        public IHttpActionResult GetDepartmentPerformance(Guid departmentId)
        {
            try
            {
                var department = db.Departments.Find(departmentId);
                if (department == null)
                    return NotFound("Department not found");

                var staffPerformance = db.StaffProfiles
                    .Where(s => s.DepartmentId == departmentId)
                    .Select(s => new
                    {
                        s.StaffId,
                        FullName = s.User.FullName,
                        s.Role,
                        s.TotalAssignments,
                        s.CompletedAssignments,
                        s.PendingAssignments,
                        s.AverageResolutionTime,
                        s.PerformanceScore
                    })
                    .OrderByDescending(s => s.PerformanceScore)
                    .ToList();

                var resolvedComplaints = db.Complaints
                    .Where(c => c.DepartmentId == departmentId && c.ResolvedAt != null && c.Category != null)
                    .ToList();

                var slaMetCount = resolvedComplaints.Count(c =>
                    (c.ResolvedAt.Value - c.CreatedAt).TotalHours <= c.Category.ExpectedResolutionTimeHours);

                var slaRate = resolvedComplaints.Count > 0 ? (double)slaMetCount / resolvedComplaints.Count * 100 : 0;

                var categoryBreakdown = db.Complaints
                    .Where(c => c.DepartmentId == departmentId && c.Category != null)
                    .GroupBy(c => c.Category.CategoryName)
                    .Select(g => new
                    {
                        Category = g.Key ?? "Uncategorized",
                        Count = g.Count(),
                        Resolved = g.Count(c => c.CurrentStatus == ComplaintStatus.Resolved)
                    })
                    .ToList();

                var sixMonthsAgo = DateTime.Now.AddMonths(-6);
                var monthlyTrend = db.Complaints
                    .Where(c => c.DepartmentId == departmentId && c.CreatedAt >= sixMonthsAgo)
                    .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month })
                    .Select(g => new
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        Total = g.Count(),
                        Resolved = g.Count(c => c.CurrentStatus == ComplaintStatus.Resolved)
                    })
                    .OrderBy(g => g.Year)
                    .ThenBy(g => g.Month)
                    .ToList();

                return Ok(new
                {
                    Department = new
                    {
                        department.DepartmentId,
                        department.DepartmentName,
                        department.DepartmentCode,
                        department.PerformanceScore,
                        department.PerformanceRating,
                        department.ActiveComplaintsCount,
                        department.ResolvedComplaintsCount,
                        department.TotalComplaintsCount,
                        department.AverageResolutionTimeDays
                    },
                    StaffPerformance = staffPerformance,
                    SLACompliance = new
                    {
                        TotalResolved = resolvedComplaints.Count,
                        MetSLA = slaMetCount,
                        ComplianceRate = Math.Round(slaRate, 2)
                    },
                    CategoryBreakdown = categoryBreakdown,
                    MonthlyTrend = monthlyTrend,
                    LastUpdated = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET DEPARTMENT SUMMARY FOR ADMIN DASHBOARD
        // =====================================================
        [HttpGet]
        [Route("summary")]
        public IHttpActionResult GetDepartmentSummary()
        {
            try
            {
                var summary = db.Departments
                    .Where(d => d.IsActive)
                    .Select(d => new
                    {
                        d.DepartmentId,
                        d.DepartmentName,
                        d.DepartmentCode,
                        d.PerformanceScore,
                        d.PerformanceRating,
                        d.ActiveComplaintsCount,
                        d.ResolvedComplaintsCount,
                        d.TotalComplaintsCount,
                        StaffCount = d.Staffs.Count(),
                        d.AverageResolutionTimeDays,
                        d.PrivatizationStatus
                    })
                    .OrderByDescending(d => d.PerformanceScore)
                    .ToList();

                int totalActive = 0;
                int totalResolved = 0;
                double totalPerformance = 0;
                int deptCount = 0;

                foreach (var dept in summary)
                {
                    totalActive += dept.ActiveComplaintsCount;
                    totalResolved += dept.ResolvedComplaintsCount;
                    if (dept.PerformanceScore != null)
                    {
                        totalPerformance += (double)dept.PerformanceScore;
                        deptCount++;
                    }
                }

                double avgPerformance = deptCount > 0 ? totalPerformance / deptCount : 0;

                return Ok(new
                {
                    Departments = summary,
                    Totals = new
                    {
                        TotalDepartments = summary.Count,
                        TotalActiveComplaints = totalActive,
                        TotalResolvedComplaints = totalResolved,
                        AveragePerformance = Math.Round(avgPerformance, 2)
                    }
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        private int GetStatusValue(string status)
        {
            switch (status.ToLower())
            {
                case "submitted": return 0;
                case "underreview": return 1;
                case "approved": return 2;
                case "assigned": return 3;
                case "inprogress": return 4;
                case "resolved": return 5;
                case "verified": return 6;
                case "rejected": return 7;
                case "closed": return 8;
                default: return 0;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }

    // DTOs
    public class StaffAssignmentRequest
    {
        public Guid AssignedById { get; set; }
        public string Notes { get; set; }
    }

    //public class DepartmentStaffDto
    //{
    //    public Guid StaffId { get; set; }
    //    public Guid UserId { get; set; }
    //    public string FullName { get; set; }
    //    public string Email { get; set; }
    //    public string PhoneNumber { get; set; }
    //    public Guid? DepartmentId { get; set; }
    //    public string Role { get; set; }
    //    public string EmployeeId { get; set; }
    //    public int TotalAssignments { get; set; }
    //    public int CompletedAssignments { get; set; }
    //    public int PendingAssignments { get; set; }
    //    public decimal PerformanceScore { get; set; }
    //    public bool IsAvailable { get; set; }
    //}

    public class ComplaintDto
    {
        public Guid ComplaintId { get; set; }
        public string ComplaintNumber { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Priority { get; set; }
        public int CurrentStatus { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CategoryName { get; set; }
        public string ZoneName { get; set; }
        public string LocationAddress { get; set; }
        public int UpvoteCount { get; set; }
        public bool HasPhotos { get; set; }
        public string AssignedToEmployeeId { get; set; }
        public string AssignedToName { get; set; }
    }
}