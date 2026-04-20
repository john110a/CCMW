using CCMW.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/sla")]
    public class SlaController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // Check complaints against SLA
        [HttpGet]
        [Route("check")]
        public IHttpActionResult CheckSLACompliance()
        {
            var complaints = db.Complaints
                .Include(c => c.Category)
                .Where(c => c.CurrentStatus != ComplaintStatus.Resolved &&
                           c.CurrentStatus != ComplaintStatus.Closed)
                .ToList();

            int breached = 0;
            int warning = 0;

            foreach (var complaint in complaints)
            {
                if (complaint.Category?.ExpectedResolutionTimeHours > 0)
                {
                    var hoursElapsed = (DateTime.Now - complaint.CreatedAt).TotalHours;
                    var threshold = complaint.Category.ExpectedResolutionTimeHours;

                    // Mark as overdue if exceeded SLA
                    if (hoursElapsed > threshold)
                    {
                        if (!complaint.IsOverdue)
                        {
                            complaint.IsOverdue = true;
                            breached++;
                        }
                    }
                    // Warning at 80% of SLA
                    else if (hoursElapsed > threshold * 0.8)
                    {
                        warning++;
                    }
                }
            }

            db.SaveChanges();

            return Ok(new
            {
                Message = "SLA check completed",
                BreachedComplaints = breached,
                NearBreachComplaints = warning,
                CheckedComplaints = complaints.Count
            });
        }

        // Get department SLA compliance rates
        [HttpGet]
        [Route("department/{departmentId}")]
        public IHttpActionResult GetDepartmentSLA(Guid departmentId)
        {
            var resolved = db.Complaints
                .Where(c => c.DepartmentId == departmentId &&
                           c.ResolvedAt != null &&
                           c.Category != null)
                .ToList();

            if (!resolved.Any())
                return Ok(new { ComplianceRate = 0 });

            int metCount = 0;
            foreach (var complaint in resolved)
            {
                var hoursElapsed = (complaint.ResolvedAt.Value - complaint.CreatedAt).TotalHours;
                if (complaint.Category.ExpectedResolutionTimeHours >= hoursElapsed)
                {
                    metCount++;
                }
            }

            var rate = (double)metCount / resolved.Count * 100;

            return Ok(new
            {
                DepartmentId = departmentId,
                ResolvedComplaints = resolved.Count,
                MetSLA = metCount,
                ComplianceRate = Math.Round(rate, 2)
            });
        }
    }
}