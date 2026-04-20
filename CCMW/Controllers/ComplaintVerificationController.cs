using CCMW.Models;
using System;
using System.Linq;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/complaint-verification")]
    public class ComplaintVerificationController : ApiController
    {
        private readonly CCMWDbContext db = new CCMWDbContext();

        // =====================================================
        // 1️⃣ CITIZEN ACCEPTS RESOLUTION (CLOSE COMPLAINT)
        // =====================================================
        [HttpPost]
        [Route("{complaintId:guid}/accept")]
        public IHttpActionResult AcceptResolution(
            Guid complaintId,
            [FromUri] Guid citizenId)
        {
            var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == complaintId);
            if (complaint == null)
                return NotFound();

            if (complaint.CitizenId != citizenId)
                return Unauthorized();

            if (complaint.CurrentStatus != ComplaintStatus.Resolved)
                return BadRequest("Complaint is not resolved yet.");

            var oldStatus = complaint.CurrentStatus.ToString();

            complaint.CurrentStatus = ComplaintStatus.Closed;
            complaint.ClosedAt = DateTime.Now;
            complaint.UpdatedAt = DateTime.Now;

            db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
            {
                HistoryId = Guid.NewGuid(),
                ComplaintId = complaintId,
                PreviousStatus = oldStatus,
                NewStatus = ComplaintStatus.Closed.ToString(),
                ChangedById = citizenId,
                ChangedAt = DateTime.Now,
                Notes = "Citizen accepted resolution"
            });

            db.SaveChanges();
            return Ok("Complaint closed successfully");
        }

        // =====================================================
        // 2️⃣ CITIZEN REJECTS RESOLUTION (REOPEN)
        // =====================================================
        [HttpPost]
        [Route("{complaintId:guid}/reject")]
        public IHttpActionResult RejectResolution(
            Guid complaintId,
            [FromUri] Guid citizenId,
            [FromBody] string rejectionReason)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
                return BadRequest("Rejection reason is required.");

            var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == complaintId);
            if (complaint == null)
                return NotFound();

            if (complaint.CitizenId != citizenId)
                return Unauthorized();

            if (complaint.CurrentStatus != ComplaintStatus.Resolved)
                return BadRequest("Complaint is not resolved yet.");

            var oldStatus = complaint.CurrentStatus.ToString();

            complaint.CurrentStatus = ComplaintStatus.Reopened;
            complaint.ReopenedAt = DateTime.Now;
            complaint.UpdatedAt = DateTime.Now;

            // Deactivate previous assignments
            var activeAssignments = db.ComplaintAssignments
                .Where(a => a.ComplaintId == complaintId && a.IsActive)
                .ToList();

            foreach (var assignment in activeAssignments)
            {
                assignment.IsActive = false;
            }

            db.ComplaintStatusHistories.Add(new ComplaintStatusHistories
            {
                HistoryId = Guid.NewGuid(),
                ComplaintId = complaintId,
                PreviousStatus = oldStatus,
               // NewStatus = ComplaintStatus.Reopened.ToString(),
                ChangedById = citizenId,
                ChangeReason = rejectionReason,
                ChangedAt = DateTime.Now,
                Notes = "Citizen rejected resolution"
            });

            db.SaveChanges();
            return Ok("Complaint reopened successfully");
        }

        // =====================================================
        // 3️⃣ CHECK IF COMPLAINT IS PENDING VERIFICATION
        // =====================================================
        [HttpGet]
        [Route("{complaintId:guid}/pending")]
        public IHttpActionResult IsPendingVerification(Guid complaintId)
        {
            var complaint = db.Complaints
                .Where(c => c.ComplaintId == complaintId)
                .Select(c => new
                {
                    c.ComplaintId,
                    c.CurrentStatus,
                    IsPendingVerification = c.CurrentStatus == ComplaintStatus.Resolved
                })
                .FirstOrDefault();

            if (complaint == null)
                return NotFound();

            return Ok(complaint);
        }

        // =====================================================
        // 4️⃣ GET VERIFICATION SUMMARY (FOR UI)
        // =====================================================
        [HttpGet]
        [Route("{complaintId:guid}/summary")]
        public IHttpActionResult GetVerificationSummary(Guid complaintId)
        {
            var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == complaintId);
            if (complaint == null)
                return NotFound();

            var lastResolution = db.ComplaintStatusHistories
                .Where(h =>
                    h.ComplaintId == complaintId &&
                    h.NewStatus == ComplaintStatus.Resolved.ToString())
                .OrderByDescending(h => h.ChangedAt)
                .FirstOrDefault();

            return Ok(new
            {
                complaint.ComplaintId,
                complaint.ComplaintNumber,
                complaint.CurrentStatus,
                ResolvedAt = lastResolution?.ChangedAt,
                complaint.ClosedAt,
                //complaint.ReopenedAt
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}
