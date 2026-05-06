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

        private string UploadRoot =>
            HttpContext.Current.Server.MapPath("~/Uploads/Complaints/");

        private string EnsureUploadDirectoryExists()
        {
            try
            {
                var path = UploadRoot;
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                    System.Diagnostics.Debug.WriteLine($"Created upload directory: {path}");
                }
                return path;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating upload directory: {ex.Message}");
                throw;
            }
        }

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
                if (!Request.Content.IsMimeMultipartContent())
                    return BadRequest("Multipart data required");

                var complaint = db.Complaints.FirstOrDefault(c => c.ComplaintId == complaintId);
                if (complaint == null)
                    return Content(HttpStatusCode.NotFound, new { error = "Complaint not found" });

                var user = db.Users.FirstOrDefault(u => u.UserId == uploadedById);
                if (user == null)
                    return Content(HttpStatusCode.NotFound, new { error = "User not found" });

                var uploadPath = EnsureUploadDirectoryExists();
                var provider = new MultipartFormDataStreamProvider(uploadPath);
                await Request.Content.ReadAsMultipartAsync(provider);

                if (provider.FileData.Count == 0)
                    return BadRequest("No files uploaded");

                var currentPhotoCount = db.ComplaintPhotos.Count(p => p.ComplaintId == complaintId);
                if (currentPhotoCount + provider.FileData.Count > 5)
                    return BadRequest("Maximum 5 photos allowed per complaint");

                int successfulUploads = 0;
                var uploadedPhotos = new System.Collections.Generic.List<object>();

                foreach (var file in provider.FileData)
                {
                    try
                    {
                        var originalFileName = file.Headers.ContentDisposition.FileName.Trim('"');
                        var extension = Path.GetExtension(originalFileName).ToLower();
                        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };

                        if (!allowedExtensions.Contains(extension))
                        {
                            if (File.Exists(file.LocalFileName)) File.Delete(file.LocalFileName);
                            continue;
                        }

                        var fileInfo = new FileInfo(file.LocalFileName);
                        if (fileInfo.Length > 5 * 1024 * 1024)
                        {
                            if (File.Exists(file.LocalFileName)) File.Delete(file.LocalFileName);
                            continue;
                        }

                        var fileName = Guid.NewGuid() + extension;
                        var finalPath = Path.Combine(uploadPath, fileName);
                        File.Move(file.LocalFileName, finalPath);

                        var photo = new ComplaintPhoto
                        {
                            PhotoId = Guid.NewGuid(),
                            ComplaintId = complaintId,
                            UploadedById = uploadedById, // Already a UserId for citizen uploads
                            PhotoUrl = "/Uploads/Complaints/" + fileName,
                            PhotoThumbnailUrl = "/Uploads/Complaints/" + fileName,
                            PhotoType = "Complaint",
                            UploadedAt = DateTime.Now,
                            GpsLatitude = null,
                            GpsLongitude = null,
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
                            try { File.Delete(file.LocalFileName); } catch { }
                    }
                }

                if (successfulUploads > 0)
                    db.SaveChanges();

                if (successfulUploads == 0)
                    return BadRequest("No valid files were uploaded. Please check file types and sizes.");

                return Ok(new
                {
                    Message = $"{successfulUploads} photo(s) uploaded successfully",
                    TotalUploaded = successfulUploads,
                    Photos = uploadedPhotos
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UploadComplaintPhoto ERROR: {ex.Message}");
                return Content(HttpStatusCode.InternalServerError, new
                {
                    Message = "An error occurred while uploading.",
                    Error = ex.Message,
                    Inner = ex.InnerException?.Message,
                    Inner2 = ex.InnerException?.InnerException?.Message
                });
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
                System.Diagnostics.Debug.WriteLine($"=== UploadResolutionPhoto START ===");
                System.Diagnostics.Debug.WriteLine($"AssignmentId: {assignmentId}, StaffId: {staffId}");

                // AssignedToId in ComplaintAssignment stores StaffProfile.StaffId (not UserId)
                var assignment = db.ComplaintAssignments
                    .FirstOrDefault(a => a.AssignmentId == assignmentId && a.AssignedToId == staffId);

                if (assignment == null)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: Assignment not found");
                    return Content(HttpStatusCode.NotFound, new
                    {
                        success = false,
                        error = "Assignment not found or does not belong to this staff member"
                    });
                }

                System.Diagnostics.Debug.WriteLine($"Assignment found - ComplaintId: {assignment.ComplaintId}");

                // -------------------------------------------------------
                // ROOT CAUSE FIX:
                // staffId = StaffProfile.StaffId (from ComplaintAssignment.staff_id column)
                // ComplaintPhoto.UploadedById = FK → Users.user_id
                // These are DIFFERENT GUIDs. We must resolve the UserId via StaffProfile.
                // -------------------------------------------------------
                var staffProfile = db.StaffProfiles.FirstOrDefault(s => s.StaffId == staffId);
                if (staffProfile == null)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: StaffProfile not found");
                    return Content(HttpStatusCode.NotFound, new
                    {
                        success = false,
                        error = "Staff profile not found"
                    });
                }

                Guid uploaderUserId = staffProfile.UserId;
                System.Diagnostics.Debug.WriteLine($"Resolved UserId for photo FK: {uploaderUserId}");

                if (!Request.Content.IsMimeMultipartContent())
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: Not multipart content");
                    return BadRequest("Multipart data required");
                }

                var uploadPath = EnsureUploadDirectoryExists();
                var provider = new MultipartFormDataStreamProvider(uploadPath);

                try
                {
                    await Request.Content.ReadAsMultipartAsync(provider);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ERROR reading multipart: {ex.Message}");
                    return BadRequest($"Error reading upload: {ex.Message}");
                }

                if (provider.FileData.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("ERROR: No files uploaded");
                    return BadRequest("No files uploaded");
                }

                System.Diagnostics.Debug.WriteLine($"Files received: {provider.FileData.Count}");

                var currentPhotoCount = db.ComplaintPhotos.Count(p =>
                    p.ComplaintId == assignment.ComplaintId && p.PhotoType == "Resolution");

                if (currentPhotoCount + provider.FileData.Count > 10)
                    return BadRequest("Maximum 10 resolution photos allowed per complaint");

                int successfulUploads = 0;
                var uploadedPhotos = new System.Collections.Generic.List<object>();

                foreach (var file in provider.FileData)
                {
                    try
                    {
                        var originalFileName = file.Headers.ContentDisposition.FileName.Trim('"');
                        System.Diagnostics.Debug.WriteLine($"Processing file: {originalFileName}");

                        var extension = Path.GetExtension(originalFileName).ToLower();
                        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };

                        if (!allowedExtensions.Contains(extension))
                        {
                            System.Diagnostics.Debug.WriteLine($"Invalid extension: {extension}");
                            if (File.Exists(file.LocalFileName)) File.Delete(file.LocalFileName);
                            continue;
                        }

                        var fileInfo = new FileInfo(file.LocalFileName);
                        if (fileInfo.Length > 5 * 1024 * 1024)
                        {
                            System.Diagnostics.Debug.WriteLine($"File too large: {fileInfo.Length} bytes");
                            if (File.Exists(file.LocalFileName)) File.Delete(file.LocalFileName);
                            continue;
                        }

                        System.Diagnostics.Debug.WriteLine($"File size: {fileInfo.Length} bytes");

                        var fileName = $"{Guid.NewGuid()}{extension}";
                        var finalPath = Path.Combine(uploadPath, fileName);

                        if (!File.Exists(file.LocalFileName))
                        {
                            System.Diagnostics.Debug.WriteLine($"Temp file not found: {file.LocalFileName}");
                            continue;
                        }

                        File.Move(file.LocalFileName, finalPath);
                        System.Diagnostics.Debug.WriteLine($"File saved to: {finalPath}");

                        var photo = new ComplaintPhoto
                        {
                            PhotoId = Guid.NewGuid(),
                            ComplaintId = assignment.ComplaintId,
                            UploadedById = uploaderUserId, // FIXED: UserId from StaffProfile, satisfies FK_Photos_UploadedBy
                            PhotoUrl = "/Uploads/Complaints/" + fileName,
                            PhotoThumbnailUrl = "/Uploads/Complaints/" + fileName,
                            PhotoType = "Resolution",
                            UploadedAt = DateTime.Now,
                            UploadOrder = currentPhotoCount + successfulUploads + 1,
                            Caption = $"Resolution photo for assignment {assignmentId}",
                            GpsLatitude = null,
                            GpsLongitude = null
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

                        System.Diagnostics.Debug.WriteLine($"Photo record created: {photo.PhotoId}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error processing file: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"Inner: {ex.InnerException?.Message}");
                        if (File.Exists(file.LocalFileName))
                            try { File.Delete(file.LocalFileName); } catch { }
                    }
                }

                if (successfulUploads > 0)
                {
                    await db.SaveChangesAsync();
                    System.Diagnostics.Debug.WriteLine($"Saved {successfulUploads} photos to database");
                }

                if (successfulUploads == 0)
                    return BadRequest("No valid files were uploaded. Please check file types (JPG, PNG, GIF) and size (max 5MB).");

                return Ok(new
                {
                    success = true,
                    Message = $"{successfulUploads} resolution photo(s) uploaded successfully",
                    TotalUploaded = successfulUploads,
                    Photos = uploadedPhotos
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"UNHANDLED EXCEPTION: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {ex.StackTrace}");
                return Content(HttpStatusCode.InternalServerError, new
                {
                    success = false,
                    Message = "An error occurred while uploading resolution photo.",
                    Error = ex.Message,
                    Inner = ex.InnerException?.Message,
                    Inner2 = ex.InnerException?.InnerException?.Message,
                    Stack = ex.StackTrace
                });
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
        // 4️⃣ GET PHOTOS BY TYPE
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

                var fullPath = HttpContext.Current.Server.MapPath(photo.PhotoUrl);
                if (File.Exists(fullPath))
                    File.Delete(fullPath);

                if (!string.IsNullOrEmpty(photo.PhotoThumbnailUrl) && photo.PhotoThumbnailUrl != photo.PhotoUrl)
                {
                    var thumbPath = HttpContext.Current.Server.MapPath(photo.PhotoThumbnailUrl);
                    if (File.Exists(thumbPath))
                        File.Delete(thumbPath);
                }

                db.ComplaintPhotos.Remove(photo);
                db.SaveChanges();

                return Ok(new { Message = "Photo deleted successfully", PhotoId = photoId });
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

                return Ok(new { Message = "Caption updated successfully", PhotoId = photoId, Caption = caption });
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
                    .Select(g => new { PhotoType = g.Key, Count = g.Count() })
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