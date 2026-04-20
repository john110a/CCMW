// Create this file: Controllers/ReportController.cs
using CCMW.Models;
using System;
using System.Linq;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/reports")]
    public class ReportController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // GET api/reports/dashboard-summary
        [HttpGet]
        [Route("dashboard-summary")]
        public IHttpActionResult GetDashboardSummary()
        {
            var summary = new
            {
                TotalComplaints = db.Complaints.Count(),
                PendingComplaints = db.Complaints.Count(c =>
                    c.CurrentStatus == ComplaintStatus.Submitted ||
                    c.CurrentStatus == ComplaintStatus.UnderReview),
                InProgressComplaints = db.Complaints.Count(c =>
                    c.CurrentStatus == ComplaintStatus.InProgress ||
                    c.CurrentStatus == ComplaintStatus.Assigned),
                ResolvedComplaints = db.Complaints.Count(c =>
                    c.CurrentStatus == ComplaintStatus.Resolved),
                TotalUsers = db.Users.Count(),
                TotalStaff = db.StaffProfiles.Count()
            };

            return Ok(summary);
        }

        // GET api/reports/monthly-trends?year=2026&month=2
        [HttpGet]
        [Route("monthly-trends")]
        public IHttpActionResult GetMonthlyTrends(int? year = null, int? month = null)
        {
            var query = db.Complaints.AsQueryable();

            if (year.HasValue)
            {
                query = query.Where(c => c.CreatedAt.Year == year.Value);
            }

            if (month.HasValue)
            {
                query = query.Where(c => c.CreatedAt.Month == month.Value);
            }

            var trends = query
                .GroupBy(c => c.CreatedAt.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Count = g.Count(),
                    Resolved = g.Count(c => c.CurrentStatus == ComplaintStatus.Resolved)
                })
                .OrderBy(g => g.Date)
                .ToList();

            return Ok(trends);
        }

        // GET api/reports/department-performance
        [HttpGet]
        [Route("department-performance")]
        public IHttpActionResult GetDepartmentPerformance()
        {
            var performance = db.Departments
                .Select(d => new
                {
                    d.DepartmentId,
                    d.DepartmentName,
                    TotalComplaints = db.Complaints.Count(c => c.DepartmentId == d.DepartmentId),
                    ResolvedComplaints = db.Complaints.Count(c =>
                        c.DepartmentId == d.DepartmentId &&
                        c.CurrentStatus == ComplaintStatus.Resolved),
                    ResolutionRate = db.Complaints.Count(c => c.DepartmentId == d.DepartmentId) > 0 ?
                        (double)db.Complaints.Count(c => c.DepartmentId == d.DepartmentId &&
                            c.CurrentStatus == ComplaintStatus.Resolved) /
                        db.Complaints.Count(c => c.DepartmentId == d.DepartmentId) * 100 : 0,
                    d.AverageResolutionTimeDays,
                    d.PerformanceScore
                })
                .OrderByDescending(d => d.PerformanceScore)
                .ToList();

            return Ok(performance);
        }

        // GET api/reports/staff-performance
        [HttpGet]
        [Route("staff-performance")]
        public IHttpActionResult GetStaffPerformance(Guid? departmentId = null)
        {
            var query = db.StaffProfiles.AsQueryable();

            if (departmentId.HasValue)
            {
                query = query.Where(s => s.DepartmentId == departmentId.Value);
            }

            var performance = query
                .Select(s => new
                {
                    s.StaffId,
                    s.User.FullName,
                    s.Department.DepartmentName,
                    s.TotalAssignments,
                    s.CompletedAssignments,
                    CompletionRate = s.TotalAssignments > 0 ?
                        (double)s.CompletedAssignments / s.TotalAssignments * 100 : 0,
                    s.AverageResolutionTime,
                    s.PerformanceScore
                })
                .OrderByDescending(s => s.PerformanceScore)
                .ToList();

            return Ok(performance);
        }

        // GET api/reports/category-breakdown
        [HttpGet]
        [Route("category-breakdown")]
        public IHttpActionResult GetCategoryBreakdown()
        {
            var breakdown = db.ComplaintCategories
                .Select(c => new
                {
                    c.CategoryId,
                    c.CategoryName,
                    TotalComplaints = db.Complaints.Count(cp => cp.CategoryId == c.CategoryId),
                    ResolvedComplaints = db.Complaints.Count(cp =>
                        cp.CategoryId == c.CategoryId &&
                        cp.CurrentStatus == ComplaintStatus.Resolved)
                })
                .OrderByDescending(c => c.TotalComplaints)
                .ToList();

            return Ok(breakdown);
        }
    }
}