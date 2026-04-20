using CCMW.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/auto-assignment")]
    public class AutoAssignmentController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // Auto-assign complaints to available staff
        [HttpPost]
        [Route("run")]
        public IHttpActionResult RunAutoAssignment()
        {
            // Get complaints ready for assignment (Approved but not assigned)
            var pendingComplaints = db.Complaints
                .Where(c => c.CurrentStatus == ComplaintStatus.Approved &&
                           c.AssignedToId == null)
                .OrderBy(c => c.Priority)
                .ThenBy(c => c.CreatedAt)
                .ToList();

            int assigned = 0;

            foreach (var complaint in pendingComplaints)
            {
                // Find best staff for this complaint
                var bestStaff = FindBestStaff(complaint);

                if (bestStaff != null)
                {
                    // Create assignment
                    var assignment = new ComplaintAssignment
                    {
                        AssignmentId = Guid.NewGuid(),
                        ComplaintId = complaint.ComplaintId,
                        AssignedToId = bestStaff.StaffId,
                        AssignedAt = DateTime.Now,
                        AssignmentType = "Auto",
                        AssignmentNotes = "Auto-assigned by system",
                        IsActive = true
                    };

                    db.ComplaintAssignments.Add(assignment);

                    // Update complaint
                    complaint.AssignedToId = bestStaff.StaffId;
                    complaint.AssignedAt = DateTime.Now;
                    complaint.CurrentStatus = ComplaintStatus.Assigned;
                    complaint.StatusUpdatedAt = DateTime.Now;

                    // Update staff stats
                    bestStaff.TotalAssignments += 1;
                    bestStaff.PendingAssignments += 1;

                    // Add status history
                    db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
                    {
                        HistoryId = Guid.NewGuid(),
                        ComplaintId = complaint.ComplaintId,
                        PreviousStatus = ComplaintStatus.Approved.ToString(),
                        NewStatus = ComplaintStatus.Assigned.ToString(),
                        ChangedById = null, // System
                        ChangedAt = DateTime.Now,
                        Notes = "Auto-assigned to " + bestStaff.User?.FullName
                    });

                    assigned++;
                }
            }

            db.SaveChanges();

            return Ok(new
            {
                Message = "Auto-assignment completed",
                ComplaintsProcessed = pendingComplaints.Count,
                Assigned = assigned
            });
        }

        private StaffProfile FindBestStaff(Complaint complaint)
        {
            // Get staff in same department and zone
            var candidates = db.StaffProfiles
                .Include(s => s.User)
                .Where(s => s.DepartmentId == complaint.DepartmentId &&
                           s.ZoneId == complaint.ZoneId &&
                           s.IsAvailable)
                .ToList();

            if (!candidates.Any())
                return null;

            // Simple algorithm: staff with least pending assignments
            return candidates.OrderBy(s => s.PendingAssignments).FirstOrDefault();
        }

        // Get auto-assignment rules
        [HttpGet]
        [Route("candidates/{complaintId}")]
        public IHttpActionResult GetAssignmentCandidates(Guid complaintId)
        {
            var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == complaintId);
            if (complaint == null)
                return NotFound();

            var candidates = db.StaffProfiles
                .Include(s => s.User)
                .Where(s => s.DepartmentId == complaint.DepartmentId &&
                           s.ZoneId == complaint.ZoneId &&
                           s.IsAvailable)
                .Select(s => new
                {
                    s.StaffId,
                    StaffName = s.User.FullName,
                    s.PendingAssignments,
                    s.CompletedAssignments,
                    s.PerformanceScore
                })
                .OrderBy(s => s.PendingAssignments)
                .ToList();

            return Ok(candidates);
        }
    }
}