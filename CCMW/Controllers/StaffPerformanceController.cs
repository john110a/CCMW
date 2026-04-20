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
            var staffList = db.StaffProfiles
                .Include(s => s.Assignments)
                .ToList();

            foreach (var staff in staffList)
            {
                // Total assignments
                staff.TotalAssignments = staff.Assignments.Count;

                // Completed assignments
                staff.CompletedAssignments = staff.Assignments.Count(a => a.CompletedAt != null);

                // Pending assignments
                staff.PendingAssignments = staff.Assignments.Count(a => a.CompletedAt == null && a.IsActive == true);

                // Average resolution time
                var completed = staff.Assignments
                    .Where(a => a.CompletedAt.HasValue)
                    .ToList();

                if (completed.Any())
                {
                    var totalHours = completed.Sum(a => (a.CompletedAt.Value - a.AssignedAt).TotalHours);
                    staff.AverageResolutionTime = (decimal)(totalHours / completed.Count);
                }

                // Performance score (simple formula)
                if (staff.TotalAssignments > 0)
                {
                    staff.PerformanceScore = (decimal)staff.CompletedAssignments / staff.TotalAssignments * 100;
                }

                // Availability
                staff.IsAvailable = staff.PendingAssignments < 5; // Max 5 pending tasks
            }

            db.SaveChanges();

            return Ok(new
            {
                Message = "Staff performance scores updated",
                StaffCount = staffList.Count
            });
        }

        // Get top performing staff
        [HttpGet]
        [Route("top/{count?}")]
        public IHttpActionResult GetTopStaff(int count = 10)
        {
            var topStaff = db.StaffProfiles
                .Include(s => s.User)
                .Include(s => s.Department)
                .OrderByDescending(s => s.PerformanceScore)
                .Take(count)
                .Select(s => new
                {
                    s.StaffId,
                    StaffName = s.User.FullName,
                    s.Department.DepartmentName,
                    s.Role,
                    s.TotalAssignments,
                    s.CompletedAssignments,
                    s.PerformanceScore,
                    s.IsAvailable
                })
                .ToList();

            return Ok(topStaff);
        }
    }
}