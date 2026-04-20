using CCMW.Models;
using System;
using System.Linq;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/complaint-photos")]
    public class ComplaintPhotoController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // =====================================================
        // 1️⃣ GET ALL PHOTOS FOR A COMPLAINT
        // =====================================================
        [HttpGet]
        [Route("complaint/{complaintId:guid}")]
        public IHttpActionResult GetPhotosByComplaint(Guid complaintId)
        {
            var photos = db.ComplaintPhotos
                           .Where(p => p.ComplaintId == complaintId)
                           .OrderBy(p => p.UploadOrder)
                           .ToList();

            return Ok(photos);
        }

        // =====================================================
        // 2️⃣ GET BEFORE PHOTOS
        // =====================================================
        [HttpGet]
        [Route("complaint/{complaintId:guid}/before")]
        public IHttpActionResult GetBeforePhotos(Guid complaintId)
        {
            var photos = db.ComplaintPhotos
                           .Where(p => p.ComplaintId == complaintId &&
                                       p.PhotoType == "Before")
                           .ToList();

            return Ok(photos);
        }

        // =====================================================
        // 3️⃣ GET AFTER PHOTOS
        // =====================================================
        [HttpGet]
        [Route("complaint/{complaintId:guid}/after")]
        public IHttpActionResult GetAfterPhotos(Guid complaintId)
        {
            var photos = db.ComplaintPhotos
                           .Where(p => p.ComplaintId == complaintId &&
                                       p.PhotoType == "After")
                           .ToList();

            return Ok(photos);
        }

        // =====================================================
        // 4️⃣ ADD PHOTO (Citizen / Staff)
        // =====================================================
        [HttpPost]
        [Route("add")]
        public IHttpActionResult AddPhoto([FromBody] ComplaintPhoto photo)
        {
            if (photo == null)
                return BadRequest("Photo data is required.");

            var complaintExists = db.Complaints.Any(c => c.ComplaintId == photo.ComplaintId);
            if (!complaintExists)
                return NotFound();

            photo.PhotoId = Guid.NewGuid();
            photo.UploadedAt = DateTime.Now;

            db.ComplaintPhotos.Add(photo);
            db.SaveChanges();

            return Ok(new
            {
                Message = "Photo uploaded successfully",
                PhotoId = photo.PhotoId
            });
        }

        // =====================================================
        // 5️⃣ DELETE PHOTO (Admin only)
        // =====================================================
        [HttpDelete]
        [Route("{photoId:guid}")]
        public IHttpActionResult DeletePhoto(Guid photoId)
        {
            var photo = db.ComplaintPhotos.FirstOrDefault(p => p.PhotoId == photoId);
            if (photo == null)
                return NotFound();

            db.ComplaintPhotos.Remove(photo);
            db.SaveChanges();

            return Ok("Photo deleted successfully");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}
