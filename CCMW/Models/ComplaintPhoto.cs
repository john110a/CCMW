using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CCMW.Models
{
    [Table("Complaint_Photos", Schema = "dbo")]
    public class ComplaintPhoto
    {
        [Key]
        [Column("photo_id")]
        public Guid PhotoId { get; set; }

        [Column("complaint_id")]
        public Guid ComplaintId { get; set; }

        [Column("photo_url")]
        [Required]
        [MaxLength(255)]
        public string PhotoUrl { get; set; }

        [Column("uploaded_at")]
        public DateTime UploadedAt { get; set; } = DateTime.Now;

        [Column("UploadedById")]
        public Guid? UploadedById { get; set; }

        [Column("PhotoType")]
        [MaxLength(30)]
        public string PhotoType { get; set; }

        [Column("PhotoThumbnailUrl")]
        [MaxLength(255)]
        public string PhotoThumbnailUrl { get; set; }

        [Column("Caption")]
        [MaxLength(255)]
        public string Caption { get; set; }

        [Column("GpsLatitude")]
        public decimal? GpsLatitude { get; set; }   // FIXED: nullable to match DB schema

        [Column("GpsLongitude")]
        public decimal? GpsLongitude { get; set; }  // FIXED: nullable to match DB schema

        [Column("Metadata")]
        public string Metadata { get; set; }

        [Column("UploadOrder")]
        public int UploadOrder { get; set; }

        // Navigation properties
        [ForeignKey("ComplaintId")]
        public virtual Complaint Complaint { get; set; }

        [ForeignKey("UploadedById")]
        public virtual User UploadedBy { get; set; }
    }
}