using CCMW.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/staff-performance")]
    public class StaffPerformanceController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // Calculate and update staff performance scores
        [HttpPost]
        [Route("calculate")]
        public IHttpActionResult CalculatePerformanceScores()
        {
            try
            {
                var staffList = db.StaffProfiles
                    .Include(s => s.Assignments)
                    .ToList();

                int updatedCount = 0;

                foreach (var staff in staffList)
                {
                    // Total assignments
                    var totalAssignments = staff.Assignments.Count;
                    staff.TotalAssignments = totalAssignments;

                    // Completed assignments
                    var completedAssignments = staff.Assignments.Count(a => a.CompletedAt != null);
                    staff.CompletedAssignments = completedAssignments;

                    // Pending assignments
                    var pendingAssignments = staff.Assignments.Count(a => a.CompletedAt == null && a.IsActive == true);
                    staff.PendingAssignments = pendingAssignments;

                    // Average resolution time (in hours)
                    var completed = staff.Assignments
                        .Where(a => a.CompletedAt.HasValue)
                        .ToList();

                    if (completed.Any())
                    {
                        var totalHours = completed.Sum(a => (a.CompletedAt.Value - a.AssignedAt).TotalHours);
                        var avgHours = (decimal)Math.Round(totalHours / completed.Count, 2);
                        staff.AverageResolutionTime = avgHours;
                    }
                    else
                    {
                        staff.AverageResolutionTime = 0;
                    }

                    // Performance score (completed / total * 100)
                    if (totalAssignments > 0)
                    {
                        var performanceScore = (decimal)completedAssignments / totalAssignments * 100;
                        staff.PerformanceScore = Math.Round(performanceScore, 2);
                    }
                    else
                    {
                        staff.PerformanceScore = 0;
                    }

                    // Availability: available if pending assignments less than 5
                    bool wasAvailable = staff.IsAvailable;
                    staff.IsAvailable = pendingAssignments < 5;

                    if (wasAvailable != staff.IsAvailable)
                    {
                        updatedCount++;
                    }
                }

                db.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = "Staff performance scores calculated and updated successfully",
                    staffCount = staffList.Count,
                    availabilityChangedCount = updatedCount,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // Get top performing staff
        [HttpGet]
        [Route("top/{count?}")]
        public IHttpActionResult GetTopStaff(int count = 10)
        {
            try
            {
                var topStaff = db.StaffProfiles
                    .Include(s => s.User)
                    .Include(s => s.Department)
                    .Where(s => s.TotalAssignments > 0)
                    .OrderByDescending(s => s.PerformanceScore)
                    .Take(count)
                    .Select(s => new
                    {
                        s.StaffId,
                        StaffName = s.User != null ? s.User.FullName : s.EmployeeId,
                        DepartmentName = s.Department != null ? s.Department.DepartmentName : "N/A",
                        s.Role,
                        s.TotalAssignments,
                        s.CompletedAssignments,
                        s.PendingAssignments,
                        PerformanceScore = Math.Round((double)s.PerformanceScore, 2),
                        AverageResolutionTime = Math.Round((double)s.AverageResolutionTime, 2),
                        s.IsAvailable,
                        CompletionRate = s.TotalAssignments > 0 ?
                            Math.Round((double)s.CompletedAssignments / s.TotalAssignments * 100, 2) : 0
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    count = topStaff.Count,
                    staff = topStaff
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // Get staff performance by ID
        [HttpGet]
        [Route("{staffId:guid}")]
        public IHttpActionResult GetStaffPerformanceById(Guid staffId)
        {
            try
            {
                var staff = db.StaffProfiles
                    .Include(s => s.User)
                    .Include(s => s.Department)
                    .FirstOrDefault(s => s.StaffId == staffId);

                if (staff == null)
                    return NotFound();

                // Get monthly breakdown
                var monthlyBreakdown = db.ComplaintAssignments
                    .Where(a => a.AssignedToId == staffId && a.CompletedAt != null)
                    .GroupBy(a => new { a.CompletedAt.Value.Year, a.CompletedAt.Value.Month })
                    .Select(g => new
                    {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        CompletedCount = g.Count(),
                        AverageHours = g.Average(a => (double)(a.CompletedAt.Value - a.AssignedAt).TotalHours)
                    })
                    .OrderByDescending(x => x.Year)
                    .ThenByDescending(x => x.Month)
                    .Take(6)
                    .ToList();

                // Get recent assignments (last 5)
                var recentAssignments = db.ComplaintAssignments
                    .Where(a => a.AssignedToId == staffId)
                    .OrderByDescending(a => a.AssignedAt)
                    .Take(5)
                    .Select(a => new
                    {
                        a.AssignmentId,
                        a.ComplaintId,
                        a.AssignedAt,
                        a.CompletedAt,
                        a.ExpectedCompletionDate,
                        IsOverdue = a.ExpectedCompletionDate != null &&
                                   a.ExpectedCompletionDate < DateTime.Now &&
                                   a.CompletedAt == null,
                        CompletionTime = a.CompletedAt != null ?
                            (double?)(a.CompletedAt.Value - a.AssignedAt).TotalHours : null
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    staff = new
                    {
                        staff.StaffId,
                        StaffName = staff.User != null ? staff.User.FullName : staff.EmployeeId,
                        DepartmentName = staff.Department != null ? staff.Department.DepartmentName : "N/A",
                        staff.Role,
                        staff.TotalAssignments,
                        staff.CompletedAssignments,
                        staff.PendingAssignments,
                        PerformanceScore = Math.Round((double)staff.PerformanceScore, 2),
                        AverageResolutionTime = Math.Round((double)staff.AverageResolutionTime, 2),
                        staff.IsAvailable,
                        CompletionRate = staff.TotalAssignments > 0 ?
                            Math.Round((double)staff.CompletedAssignments / staff.TotalAssignments * 100, 2) : 0
                    },
                    monthlyBreakdown,
                    recentAssignments
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // Update single staff performance
        [HttpPost]
        [Route("{staffId:guid}/update")]
        public IHttpActionResult UpdateStaffPerformance(Guid staffId)
        {
            try
            {
                var staff = db.StaffProfiles
                    .Include(s => s.Assignments)
                    .FirstOrDefault(s => s.StaffId == staffId);

                if (staff == null)
                    return NotFound();

                // Calculate performance for this staff only
                var totalAssignments = staff.Assignments.Count;
                var completedAssignments = staff.Assignments.Count(a => a.CompletedAt != null);
                var pendingAssignments = staff.Assignments.Count(a => a.CompletedAt == null && a.IsActive == true);

                staff.TotalAssignments = totalAssignments;
                staff.CompletedAssignments = completedAssignments;
                staff.PendingAssignments = pendingAssignments;

                // Average resolution time
                var completed = staff.Assignments
                    .Where(a => a.CompletedAt.HasValue)
                    .ToList();

                if (completed.Any())
                {
                    var totalHours = completed.Sum(a => (a.CompletedAt.Value - a.AssignedAt).TotalHours);
                    var avgHours = (decimal)Math.Round(totalHours / completed.Count, 2);
                    staff.AverageResolutionTime = avgHours;
                }
                else
                {
                    staff.AverageResolutionTime = 0;
                }

                // Performance score
                staff.PerformanceScore = totalAssignments > 0
                    ? Math.Round((decimal)completedAssignments / totalAssignments * 100, 2)
                    : 0;

                // Availability
                staff.IsAvailable = pendingAssignments < 5;

                db.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = "Staff performance updated successfully",
                    staffId = staffId,
                    totalAssignments = staff.TotalAssignments,
                    completedAssignments = staff.CompletedAssignments,
                    pendingAssignments = staff.PendingAssignments,
                    performanceScore = staff.PerformanceScore,
                    isAvailable = staff.IsAvailable
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // Get staff performance summary for dashboard
        [HttpGet]
        [Route("summary")]
        public IHttpActionResult GetPerformanceSummary()
        {
            try
            {
                var totalStaff = db.StaffProfiles.Count();
                var activeStaff = db.StaffProfiles.Count(s => s.IsAvailable == true);
                var avgPerformance = db.StaffProfiles.Any() ? db.StaffProfiles.Average(s => s.PerformanceScore) : 0;
                var totalAssignments = db.ComplaintAssignments.Count();
                var completedAssignments = db.ComplaintAssignments.Count(a => a.CompletedAt != null);
                var pendingAssignments = db.ComplaintAssignments.Count(a => a.CompletedAt == null && a.IsActive == true);

                // Get top performer
                var topPerformer = db.StaffProfiles
                    .Include(s => s.User)
                    .Where(s => s.TotalAssignments > 0)
                    .OrderByDescending(s => s.PerformanceScore)
                    .Select(s => new
                    {
                        StaffName = s.User != null ? s.User.FullName : s.EmployeeId,
                        PerformanceScore = Math.Round((double)s.PerformanceScore, 2)
                    })
                    .FirstOrDefault();

                var summary = new
                {
                    TotalStaff = totalStaff,
                    ActiveStaff = activeStaff,
                    AveragePerformanceScore = Math.Round((double)avgPerformance, 2),
                    TotalAssignmentsAll = totalAssignments,
                    CompletedAssignmentsAll = completedAssignments,
                    PendingAssignmentsAll = pendingAssignments,
                    OverallCompletionRate = totalAssignments > 0 ?
                        Math.Round((double)completedAssignments / totalAssignments * 100, 2) : 0,
                    TopPerformer = topPerformer != null ? topPerformer.StaffName : "N/A",
                    TopPerformerScore = topPerformer?.PerformanceScore ?? 0
                };

                return Ok(new { success = true, summary });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // Reset all staff performance scores (for admin use)
        [HttpPost]
        [Route("reset")]
        public IHttpActionResult ResetPerformanceScores()
        {
            try
            {
                var allStaff = db.StaffProfiles.ToList();

                foreach (var staff in allStaff)
                {
                    staff.TotalAssignments = 0;
                    staff.CompletedAssignments = 0;
                    staff.PendingAssignments = 0;
                    staff.AverageResolutionTime = 0;
                    staff.PerformanceScore = 0;
                    staff.IsAvailable = true;
                }

                db.SaveChanges();

                return Ok(new
                {
                    success = true,
                    message = "All staff performance scores have been reset",
                    resetCount = allStaff.Count,
                    timestamp = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}