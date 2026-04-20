using CCMW.Models;
using System;
using System.Linq;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/complaint-status-history")]
    public class ComplaintStatusHistoryController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // =====================================================
        // 0️⃣ ADD STATUS HISTORY (SYSTEM / CONTROLLER INTERNAL)
        // =====================================================
        [HttpPost]
        [Route("add")]
        public IHttpActionResult AddStatusHistory(ComplaintStatusHistories model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            model.HistoryId = Guid.NewGuid();
            model.ChangedAt = DateTime.Now;

            db.ComplaintStatusHistories.Add(model);
            db.SaveChanges();

            return Ok(new
            {
                Message = "Complaint status history recorded successfully",
                model.HistoryId
            });
        }

        // =====================================================
        // 1️⃣ GET FULL STATUS TIMELINE FOR A COMPLAINT
        // =====================================================
        [HttpGet]
        [Route("complaint/{complaintId:guid}")]
        public IHttpActionResult GetHistoryByComplaint(Guid complaintId)
        {
            var history = db.ComplaintStatusHistories
                .Where(h => h.ComplaintId == complaintId)
                .OrderBy(h => h.ChangedAt)
                .Select(h => new
                {
                    h.HistoryId,
                    h.PreviousStatus,
                    h.NewStatus,
                    h.ChangeReason,
                    h.Notes,
                    h.ChangedById,
                    h.ChangedAt
                })
                .ToList();

            return Ok(history);
        }

        // =====================================================
        // 2️⃣ GET LATEST STATUS ENTRY
        // =====================================================
        [HttpGet]
        [Route("complaint/{complaintId:guid}/latest")]
        public IHttpActionResult GetLatestStatus(Guid complaintId)
        {
            var latest = db.ComplaintStatusHistories
                .Where(h => h.ComplaintId == complaintId)
                .OrderByDescending(h => h.ChangedAt)
                .Select(h => new
                {
                    h.NewStatus,
                    h.ChangedAt,
                    h.ChangedById
                })
                .FirstOrDefault();

            if (latest == null)
                return NotFound();

            return Ok(latest);
        }

        // =====================================================
        // 3️⃣ GET STATUS HISTORY BY USER (ADMIN / STAFF AUDIT)
        // =====================================================
        [HttpGet]
        [Route("user/{userId:guid}")]
        public IHttpActionResult GetHistoryByUser(Guid userId)
        {
            var history = db.ComplaintStatusHistories
                .Where(h => h.ChangedById == userId)
                .OrderByDescending(h => h.ChangedAt)
                .Select(h => new
                {
                    h.ComplaintId,
                    h.PreviousStatus,
                    h.NewStatus,
                    h.ChangeReason,
                    h.ChangedAt
                })
                .ToList();

            return Ok(history);
        }

        // =====================================================
        // 4️⃣ ADMIN AUDIT – ALL STATUS CHANGES
        // =====================================================
        [HttpGet]
        [Route("audit/all")]
        public IHttpActionResult GetAllStatusHistory()
        {
            var history = db.ComplaintStatusHistories
                .OrderByDescending(h => h.ChangedAt)
                .Take(500) // safety limit
                .Select(h => new
                {
                    h.ComplaintId,
                    h.PreviousStatus,
                    h.NewStatus,
                    h.ChangedById,
                    h.ChangeReason,
                    h.ChangedAt
                })
                .ToList();

            return Ok(history);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();

            base.Dispose(disposing);
        }
    }
}
