using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CCMW.Models;
using System.Collections.Generic;

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
    public string PhotoUrl { get; set; }

    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; } = DateTime.Now;

    
    public virtual Complaint Complaint { get; set; }

    [Column("UploadedById")]
    public Guid? UploadedById { get; set; }

   
    public virtual User UploadedBy { get; set; }

    public string PhotoType { get; set; }
    public string PhotoThumbnailUrl { get; set; }
    public string Caption { get; set; }
    public decimal GpsLatitude { get; set; }
    public decimal GpsLongitude { get; set; }
    public string Metadata { get; set; }
    public int UploadOrder { get; set; }

   // public virtual ICollection<User> UploadedByUsers { get; set; } = new HashSet<User>();
}
