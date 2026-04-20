using CCMW.Models;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/complaint-media")]
    public class ComplaintMediaController : ApiController
    {
        private readonly CCMWDbContext db = new CCMWDbContext();

        private readonly string UploadRoot =
            HttpContext.Current.Server.MapPath("~/Uploads/Complaints/");

        // =====================================================
        // 1️⃣ UPLOAD COMPLAINT PHOTO (Citizen / Admin)
        // =====================================================
        [HttpPost]
        [Route("complaint/{complaintId:guid}/upload")]
        public async Task<IHttpActionResult> UploadComplaintPhoto(
            Guid complaintId,
            [FromUri] Guid uploadedById)
        {
            try
            {
                // Validate input
                if (!Request.Content.IsMimeMultipartContent())
                    return BadRequest("Multipart data required");

                // Check if complaint exists
                var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == complaintId);
                if (complaint == null)
                    return Content(HttpStatusCode.NotFound, new { error = "Complaint not found" });

                // Check if user exists
                var user = db.Users.FirstOrDefault(u => u.UserId == uploadedById);
                if (user == null)
                    return Content(HttpStatusCode.NotFound, new { error = "User not found" });

                // Create upload directory if it doesn't exist
                Directory.CreateDirectory(UploadRoot);

                var provider = new MultipartFormDataStreamProvider(UploadRoot);
                await Request.Content.ReadAsMultipartAsync(provider);

                // Check if any files were uploaded
                if (provider.FileData.Count == 0)
                    return BadRequest("No files uploaded");

                // Check total photos limit per complaint (max 5)
                var currentPhotoCount = db.ComplaintPhotos.Count(p => p.ComplaintId == complaintId);
                if (currentPhotoCount + provider.FileData.Count > 5)
                    return BadRequest("Maximum 5 photos allowed per complaint");

                int successfulUploads = 0;
                var uploadedPhotos = new System.Collections.Generic.List<object>();

                foreach (var file in provider.FileData)
                {
                    try
                    {
                        // Get original filename
                        var originalFileName = file.Headers.ContentDisposition.FileName.Trim('"');

                        // Validate file extension
                        var extension = Path.GetExtension(originalFileName).ToLower();
                        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
                        if (!allowedExtensions.Contains(extension))
                        {
                            File.Delete(file.LocalFileName);
                            continue; // Skip invalid file
                        }

                        // Validate file size (max 5MB)
                        var fileInfo = new FileInfo(file.LocalFileName);
                        if (fileInfo.Length > 5 * 1024 * 1024) // 5MB in bytes
                        {
                            File.Delete(file.LocalFileName);
                            continue; // Skip oversized file
                        }

                        // Generate unique filename
                        var fileName = Guid.NewGuid() + extension;
                        var finalPath = Path.Combine(UploadRoot, fileName);

                        // Move file to final location
                        File.Move(file.LocalFileName, finalPath);

                        // Get GPS coordinates if available (optional)
                        decimal? gpsLat = null;
                        decimal? gpsLng = null;

                        // You can add logic here to extract GPS from image metadata if needed

                        // Create photo record in database
                        var photo = new ComplaintPhoto
                        {
                            PhotoId = Guid.NewGuid(),
                            ComplaintId = complaintId,
                            UploadedById = uploadedById,
                            PhotoUrl = "/Uploads/Complaints/" + fileName,
                            PhotoThumbnailUrl = "/Uploads/Complaints/" + fileName, // You can generate thumbnail later
                            PhotoType = "Complaint",
                            UploadedAt = DateTime.Now,
                            GpsLatitude = gpsLat ?? 0,
                            GpsLongitude = gpsLng ?? 0,
                            UploadOrder = currentPhotoCount + successfulUploads + 1
                        };

                        db.ComplaintPhotos.Add(photo);
                        successfulUploads++;

                        uploadedPhotos.Add(new
                        {
                            photo.PhotoId,
                            photo.PhotoUrl,
                            photo.UploadOrder,
                            FileName = originalFileName
                        });
                    }
                    catch (Exception ex)
                    {
                        // Log error but continue with other files
                        System.Diagnostics.Debug.WriteLine($"Error processing file: {ex.Message}");
                        // Clean up temp file if it exists
                        if (File.Exists(file.LocalFileName))
                            File.Delete(file.LocalFileName);
                    }
                }

                // Save all successful uploads to database
                if (successfulUploads > 0)
                {
                    db.SaveChanges();
                }

                // Return response
                if (successfulUploads == 0)
                {
                    return BadRequest("No valid files were uploaded. Please check file types and sizes.");
                }

                return Ok(new
                {
                    Message = $"{successfulUploads} photo(s) uploaded successfully",
                    TotalUploaded = successfulUploads,
                    Photos = uploadedPhotos
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // 2️⃣ UPLOAD RESOLUTION PHOTO (Field Staff)
        // =====================================================
        [HttpPost]
        [Route("assignment/{assignmentId:guid}/resolution/upload")]
        public async Task<IHttpActionResult> UploadResolutionPhoto(
            Guid assignmentId,
            [FromUri] Guid staffId)
        {
            try
            {
                // Validate assignment exists and belongs to staff
                var assignment = db.ComplaintAssignments
                    .FirstOrDefault(a =>
                        a.AssignmentId == assignmentId &&
                        a.AssignedToId == staffId);

                if (assignment == null)
                    return Content(HttpStatusCode.NotFound, new { error = "Assignment not found or does not belong to this staff member" });

                // Check if staff exists
                var staff = db.StaffProfiles.FirstOrDefault(s => s.StaffId == staffId);
                if (staff == null)
                    return Content(HttpStatusCode.NotFound, new { error = "Staff not found" });

                if (!Request.Content.IsMimeMultipartContent())
                    return BadRequest("Multipart data required");

                Directory.CreateDirectory(UploadRoot);

                var provider = new MultipartFormDataStreamProvider(UploadRoot);
                await Request.Content.ReadAsMultipartAsync(provider);

                // Check if any files were uploaded
                if (provider.FileData.Count == 0)
                    return BadRequest("No files uploaded");

                // Check total photos limit per complaint (max 5)
                var currentPhotoCount = db.ComplaintPhotos.Count(p =>
                    p.ComplaintId == assignment.ComplaintId &&
                    p.PhotoType == "Resolution");

                if (currentPhotoCount + provider.FileData.Count > 5)
                    return BadRequest("Maximum 5 resolution photos allowed per complaint");

                int successfulUploads = 0;
                var uploadedPhotos = new System.Collections.Generic.List<object>();

                foreach (var file in provider.FileData)
                {
                    try
                    {
                        // Get original filename
                        var originalFileName = file.Headers.ContentDisposition.FileName.Trim('"');

                        // Validate file extension
                        var extension = Path.GetExtension(originalFileName).ToLower();
                        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
                        if (!allowedExtensions.Contains(extension))
                        {
                            File.Delete(file.LocalFileName);
                            continue;
                        }

                        // Validate file size (max 5MB)
                        var fileInfo = new FileInfo(file.LocalFileName);
                        if (fileInfo.Length > 5 * 1024 * 1024)
                        {
                            File.Delete(file.LocalFileName);
                            continue;
                        }

                        // Generate unique filename
                        var fileName = Guid.NewGuid() + extension;
                        var finalPath = Path.Combine(UploadRoot, fileName);

                        File.Move(file.LocalFileName, finalPath);

                        // Create photo record
                        var photo = new ComplaintPhoto
                        {
                            PhotoId = Guid.NewGuid(),
                            ComplaintId = assignment.ComplaintId,
                            UploadedById = staffId,
                            PhotoUrl = "/Uploads/Complaints/" + fileName,
                            PhotoThumbnailUrl = "/Uploads/Complaints/" + fileName,
                            PhotoType = "Resolution",
                            UploadedAt = DateTime.Now,
                            UploadOrder = currentPhotoCount + successfulUploads + 1
                        };

                        db.ComplaintPhotos.Add(photo);
                        successfulUploads++;

                        uploadedPhotos.Add(new
                        {
                            photo.PhotoId,
                            photo.PhotoUrl,
                            photo.UploadOrder,
                            FileName = originalFileName
                        });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error processing file: {ex.Message}");
                        if (File.Exists(file.LocalFileName))
                            File.Delete(file.LocalFileName);
                    }
                }

                if (successfulUploads > 0)
                {
                    db.SaveChanges();
                }

                if (successfulUploads == 0)
                {
                    return BadRequest("No valid files were uploaded. Please check file types and sizes.");
                }

                return Ok(new
                {
                    Message = $"{successfulUploads} resolution photo(s) uploaded successfully",
                    TotalUploaded = successfulUploads,
                    Photos = uploadedPhotos
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // 3️⃣ GET ALL MEDIA FOR A COMPLAINT
        // =====================================================
        [HttpGet]
        [Route("complaint/{complaintId:guid}")]
        public IHttpActionResult GetComplaintMedia(Guid complaintId)
        {
            try
            {
                // Check if complaint exists
                var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == complaintId);
                if (complaint == null)
                    return Content(HttpStatusCode.NotFound, new { error = "Complaint not found" });

                var media = db.ComplaintPhotos
                    .Where(p => p.ComplaintId == complaintId)
                    .OrderBy(p => p.UploadOrder)
                    .Select(p => new
                    {
                        p.PhotoId,
                        p.PhotoUrl,
                        p.PhotoThumbnailUrl,
                        p.PhotoType,
                        p.UploadedById,
                        p.UploadedAt,
                        p.UploadOrder,
                        p.Caption,
                        UploadedBy = db.Users
                            .Where(u => u.UserId == p.UploadedById)
                            .Select(u => new { u.FullName, u.UserType })
                            .FirstOrDefault()
                    })
                    .ToList();

                return Ok(new
                {
                    ComplaintId = complaintId,
                    TotalPhotos = media.Count,
                    Photos = media
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // 4️⃣ GET PHOTOS BY TYPE (Before/After/Complaint/Resolution)
        // =====================================================
        [HttpGet]
        [Route("complaint/{complaintId:guid}/type/{photoType}")]
        public IHttpActionResult GetPhotosByType(Guid complaintId, string photoType)
        {
            try
            {
                var validTypes = new[] { "Complaint", "Resolution", "Before", "After" };
                if (!validTypes.Contains(photoType))
                    return BadRequest("Invalid photo type. Must be: Complaint, Resolution, Before, or After");

                var photos = db.ComplaintPhotos
                    .Where(p => p.ComplaintId == complaintId && p.PhotoType == photoType)
                    .OrderBy(p => p.UploadOrder)
                    .Select(p => new
                    {
                        p.PhotoId,
                        p.PhotoUrl,
                        p.PhotoThumbnailUrl,
                        p.UploadedAt,
                        p.UploadOrder,
                        p.Caption
                    })
                    .ToList();

                return Ok(photos);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // 5️⃣ DELETE PHOTO (Admin Only)
        // =====================================================
        [HttpDelete]
        [Route("{photoId:guid}")]
        public IHttpActionResult DeletePhoto(Guid photoId)
        {
            try
            {
                var photo = db.ComplaintPhotos.FirstOrDefault(p => p.PhotoId == photoId);
                if (photo == null)
                    return Content(HttpStatusCode.NotFound, new { error = "Photo not found" });

                // Delete physical file
                var fullPath = HttpContext.Current.Server.MapPath(photo.PhotoUrl);
                if (File.Exists(fullPath))
                    File.Delete(fullPath);

                // Also delete thumbnail if it exists and is different
                if (!string.IsNullOrEmpty(photo.PhotoThumbnailUrl) && photo.PhotoThumbnailUrl != photo.PhotoUrl)
                {
                    var thumbPath = HttpContext.Current.Server.MapPath(photo.PhotoThumbnailUrl);
                    if (File.Exists(thumbPath))
                        File.Delete(thumbPath);
                }

                db.ComplaintPhotos.Remove(photo);
                db.SaveChanges();

                return Ok(new
                {
                    Message = "Photo deleted successfully",
                    PhotoId = photoId
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // 6️⃣ UPDATE PHOTO CAPTION
        // =====================================================
        [HttpPut]
        [Route("{photoId:guid}/caption")]
        public IHttpActionResult UpdateCaption(Guid photoId, [FromBody] string caption)
        {
            try
            {
                var photo = db.ComplaintPhotos.FirstOrDefault(p => p.PhotoId == photoId);
                if (photo == null)
                    return Content(HttpStatusCode.NotFound, new { error = "Photo not found" });

                photo.Caption = caption;
                db.SaveChanges();

                return Ok(new
                {
                    Message = "Caption updated successfully",
                    PhotoId = photoId,
                    Caption = caption
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // 7️⃣ GET PHOTO COUNT FOR COMPLAINT
        // =====================================================
        [HttpGet]
        [Route("complaint/{complaintId:guid}/count")]
        public IHttpActionResult GetPhotoCount(Guid complaintId)
        {
            try
            {
                var counts = db.ComplaintPhotos
                    .Where(p => p.ComplaintId == complaintId)
                    .GroupBy(p => p.PhotoType)
                    .Select(g => new
                    {
                        PhotoType = g.Key,
                        Count = g.Count()
                    })
                    .ToList();

                return Ok(new
                {
                    ComplaintId = complaintId,
                    TotalPhotos = counts.Sum(c => c.Count),
                    ByType = counts
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