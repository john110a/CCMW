using CCMW.Models;
using System;
using System.Data.Entity; // <-- Add this using directive
using System.Linq;
using System.Net;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/appeals")]
    public class AppealsController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // CITIZEN: FILE APPEAL
        [HttpPost]
        [Route("file")]
        public IHttpActionResult FileAppeal([FromBody] Appeal appeal)
        {
            if (appeal == null)
                return BadRequest("Appeal data is required.");

            // Check if complaint exists and was rejected
            var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == appeal.ComplaintId);
            if (complaint == null)
                return NotFound("Complaint not found.");

            if (complaint.CurrentStatus != ComplaintStatus.Rejected &&
                complaint.SubmissionStatus != SubmissionStatus.Rejected)
                return BadRequest("Can only appeal rejected complaints.");

            // Check if appeal already exists
            if (db.Appeals.Any(a => a.ComplaintId == appeal.ComplaintId && a.CitizenId == appeal.CitizenId))
                return BadRequest("Appeal already filed for this complaint.");

            appeal.AppealId = Guid.NewGuid();
            appeal.AppealStatus = "Pending";
            appeal.SubmittedAt = DateTime.Now;

            db.Appeals.Add(appeal);
            db.SaveChanges();

            return Ok(new
            {
                Message = "Appeal filed successfully.",
                AppealId = appeal.AppealId,
                SubmittedAt = appeal.SubmittedAt
            });
        }

        private IHttpActionResult NotFound(string message)
        {
            return Content(HttpStatusCode.NotFound, new { error = message });
        }



        // CITIZEN: GET MY APPEALS
        [HttpGet]
        [Route("my/{citizenId:guid}")]
        public IHttpActionResult GetMyAppeals(Guid citizenId)
        {
            var appeals = db.Appeals
                .Include(a => a.Complaint)
                .Where(a => a.CitizenId == citizenId)
                .OrderByDescending(a => a.SubmittedAt)
                .Select(a => new
                {
                    a.AppealId,
                    a.ComplaintId,
                    Complaint = new { a.Complaint.ComplaintNumber, a.Complaint.Title },
                    a.AppealReason,
                    a.AppealStatus,
                    a.SubmittedAt,
                    a.ReviewedAt,
                    Reviewer = a.ReviewedBy != null ? new { a.ReviewedBy.FullName } : null
                })
                .ToList();

            return Ok(appeals);
        }

        // ADMIN: GET ALL PENDING APPEALS
        [HttpGet]
        [Route("pending")]
        public IHttpActionResult GetPendingAppeals()
        {
            var appeals = db.Appeals
                .Include(a => a.Complaint)
                .Include(a => a.Citizen)
                .Where(a => a.AppealStatus == "Pending")
                .OrderBy(a => a.SubmittedAt)
                .Select(a => new
                {
                    a.AppealId,
                    a.ComplaintId,
                    Complaint = new { a.Complaint.ComplaintNumber, a.Complaint.Title, a.Complaint.RejectionReason },
                    Citizen = new { a.Citizen.FullName, a.Citizen.Email, a.Citizen.PhoneNumber },
                    a.AppealReason,
                    a.SupportingDocuments,
                    a.SubmittedAt
                })
                .ToList();

            return Ok(appeals);
        }

        // ADMIN: REVIEW APPEAL
        [HttpPost]
        [Route("{appealId:guid}/review")]
        public IHttpActionResult ReviewAppeal(Guid appealId, [FromBody] ReviewAppealRequest request, Guid? adminId)
        {
            var appeal = db.Appeals
                .Include(a => a.Complaint)
                .FirstOrDefault(a => a.AppealId == appealId);

            if (appeal == null)
                return NotFound("Appeal not found.");

            if (appeal.AppealStatus != "Pending")
                return BadRequest("Appeal already reviewed.");

            appeal.AppealStatus = request.Status; // Approved or Rejected
            appeal.ReviewedById = adminId;
            appeal.ReviewNotes = request.ReviewNotes;
            appeal.ReviewedAt = DateTime.Now;

            // If appeal approved, update complaint status
            if (request.Status == "Approved")
            {
                appeal.Complaint.SubmissionStatus = SubmissionStatus.Approved;
                appeal.Complaint.CurrentStatus = ComplaintStatus.Approved;
                appeal.Complaint.RejectionReason = null; // Clear rejection
                appeal.Complaint.ApprovedById = adminId;
                appeal.Complaint.StatusUpdatedAt = DateTime.Now;
            }

            db.SaveChanges();

            return Ok(new
            {
                Message = $"Appeal {request.Status.ToLower()} successfully.",
                AppealId = appealId,
                Status = request.Status
            });
        }

        // GET APPEAL DETAILS
        [HttpGet]
        [Route("{appealId:guid}")]
        public IHttpActionResult GetAppealDetails(Guid appealId)
        {
            var appeal = db.Appeals
                .Include(a => a.Complaint)
                .Include(a => a.Citizen)
                .Include(a => a.ReviewedBy)
                .FirstOrDefault(a => a.AppealId == appealId);

            if (appeal == null)
                return NotFound();

            return Ok(new
            {
                appeal.AppealId,
                appeal.ComplaintId,
                Complaint = new
                {
                    appeal.Complaint.ComplaintNumber,
                    appeal.Complaint.Title,
                    appeal.Complaint.Description,
                    appeal.Complaint.RejectionReason
                },
                Citizen = new
                {
                    appeal.Citizen.UserId,
                    appeal.Citizen.FullName,
                    appeal.Citizen.Email
                },
                appeal.AppealReason,
                appeal.SupportingDocuments,
                appeal.AppealStatus,
                appeal.SubmittedAt,
                Reviewer = appeal.ReviewedBy != null ? new
                {
                    appeal.ReviewedBy.FullName,
                    appeal.ReviewedBy.Email
                } : null,
                appeal.ReviewNotes,
                appeal.ReviewedAt
            });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }

    // Helper DTO for review request
    public class ReviewAppealRequest
{
    public Guid AdminId { get; set; }
    public string Status { get; set; }
    public string ReviewNotes { get; set; }
}
}