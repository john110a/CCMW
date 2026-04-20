using System;
using System.IO;
using System.Web;
using System.Linq;

namespace CCMW.Services
{
    public class FileUploadService
    {
        private readonly string _uploadRootPath;
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif" };
        private readonly long _maxFileSize = 10 * 1024 * 1024; // 10MB

        public FileUploadService()
        {
            _uploadRootPath = HttpContext.Current.Server.MapPath("~/Uploads/");

            // Create upload directory if it doesn't exist
            if (!Directory.Exists(_uploadRootPath))
            {
                Directory.CreateDirectory(_uploadRootPath);
            }
        }

        // UPLOAD FILE
        public FileUploadResult UploadFile(HttpPostedFile file, string subfolder = "")
        {
            var result = new FileUploadResult();

            try
            {
                // Validate file
                var validation = ValidateFile(file);
                if (!validation.IsValid)
                {
                    result.Error = validation.ErrorMessage;
                    return result;
                }

                // Create subfolder if specified
                var uploadPath = _uploadRootPath;
                if (!string.IsNullOrEmpty(subfolder))
                {
                    uploadPath = Path.Combine(_uploadRootPath, subfolder);
                    if (!Directory.Exists(uploadPath))
                    {
                        Directory.CreateDirectory(uploadPath);
                    }
                }

                // Generate unique filename
                var extension = Path.GetExtension(file.FileName).ToLower();
                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(uploadPath, fileName);

                // Save file
                file.SaveAs(filePath);

                // Generate URL
                var relativePath = filePath.Replace(HttpContext.Current.Server.MapPath("~/"), "").Replace("\\", "/");
                var fileUrl = $"/{relativePath}";

                // Generate thumbnail for images
                string thumbnailUrl = null;
                if (IsImageFile(extension))
                {
                    thumbnailUrl = GenerateThumbnail(filePath, uploadPath);
                }

                result.IsSuccess = true;
                result.FileName = fileName;
                result.FileUrl = fileUrl;
                result.ThumbnailUrl = thumbnailUrl;
                result.FileSize = file.ContentLength;
                result.FileType = GetFileType(extension);
            }
            catch (Exception ex)
            {
                result.Error = $"Upload failed: {ex.Message}";
            }

            return result;
        }

        // DELETE FILE
        public bool DeleteFile(string fileUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(fileUrl))
                    return false;

                var filePath = HttpContext.Current.Server.MapPath(fileUrl);
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);

                    // Also delete thumbnail if exists
                    var thumbnailPath = filePath.Replace(Path.GetExtension(filePath), "_thumb" + Path.GetExtension(filePath));
                    if (File.Exists(thumbnailPath))
                    {
                        File.Delete(thumbnailPath);
                    }

                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        // VALIDATE FILE
        public FileValidationResult ValidateFile(HttpPostedFile file)
        {
            var result = new FileValidationResult { IsValid = true };

            if (file == null || file.ContentLength == 0)
            {
                result.IsValid = false;
                result.ErrorMessage = "No file provided.";
                return result;
            }

            // Check file size
            if (file.ContentLength > _maxFileSize)
            {
                result.IsValid = false;
                result.ErrorMessage = $"File size exceeds maximum limit of {_maxFileSize / (1024 * 1024)}MB.";
                return result;
            }

            // Check file extension
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (string.IsNullOrEmpty(extension) || !_allowedExtensions.Contains(extension))
            {
                result.IsValid = false;
                result.ErrorMessage = $"Invalid file type. Allowed types: {string.Join(", ", _allowedExtensions)}";
                return result;
            }

            // Additional validation for images
            if (IsImageFile(extension))
            {
                try
                {
                    // Simple image validation by checking if we can get dimensions
                    using (var img = System.Drawing.Image.FromStream(file.InputStream))
                    {
                        result.Width = img.Width;
                        result.Height = img.Height;
                    }
                    file.InputStream.Position = 0; // Reset stream position
                }
                catch
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Invalid image file.";
                }
            }

            return result;
        }

        // GENERATE THUMBNAIL
        private string GenerateThumbnail(string originalPath, string uploadPath)
        {
            try
            {
                var extension = Path.GetExtension(originalPath);
                var fileName = Path.GetFileNameWithoutExtension(originalPath);
                var thumbnailPath = Path.Combine(uploadPath, $"{fileName}_thumb{extension}");

                using (var original = System.Drawing.Image.FromFile(originalPath))
                {
                    // Calculate thumbnail dimensions (max 200px)
                    int thumbWidth, thumbHeight;
                    if (original.Width > original.Height)
                    {
                        thumbWidth = 200;
                        thumbHeight = (int)((double)original.Height / original.Width * 200);
                    }
                    else
                    {
                        thumbHeight = 200;
                        thumbWidth = (int)((double)original.Width / original.Height * 200);
                    }

                    // Create thumbnail
                    using (var thumb = original.GetThumbnailImage(thumbWidth, thumbHeight, null, IntPtr.Zero))
                    {
                        thumb.Save(thumbnailPath);
                    }
                }

                var relativePath = thumbnailPath.Replace(HttpContext.Current.Server.MapPath("~/"), "").Replace("\\", "/");
                return $"/{relativePath}";
            }
            catch
            {
                return null; // Thumbnail generation failed
            }
        }

        // CHECK IF FILE IS IMAGE
        private bool IsImageFile(string extension)
        {
            return extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".gif";
        }

        // GET FILE TYPE
        private string GetFileType(string extension)
        {
            if (IsImageFile(extension)) return "image";
            if (extension == ".pdf") return "pdf";
            if (extension == ".doc" || extension == ".docx") return "document";
            return "other";
        }
    }

    // RESULT CLASSES
    public class FileUploadResult
    {
        public bool IsSuccess { get; set; }
        public string FileName { get; set; }
        public string FileUrl { get; set; }
        public string ThumbnailUrl { get; set; }
        public long FileSize { get; set; }
        public string FileType { get; set; }
        public string Error { get; set; }
    }

    public class FileValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}