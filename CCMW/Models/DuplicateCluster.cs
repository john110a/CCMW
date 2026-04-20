using CCMW.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("DuplicateClusters")] // Specify the table name
public class DuplicateCluster
{
    [Key]
    [Column("cluster_id")] // Map to database column
    public Guid ClusterId { get; set; }

    [Column("primary_complaint_id")]
    public Guid PrimaryComplaintId { get; set; }

    [Column("category_id")]
    public Guid CategoryId { get; set; }

    [Column("location_latitude")]
    public decimal LocationLatitude { get; set; }

    [Column("location_longitude")]
    public decimal LocationLongitude { get; set; }

    [Column("cluster_radius_meters")]
    public int ClusterRadiusMeters { get; set; }

    [Column("total_complaints_merged")]
    public int TotalComplaintsMerged { get; set; }

    [Column("total_combined_upvotes")]
    public int TotalCombinedUpvotes { get; set; }

    [Column("ai_similarity_score")]
    public decimal AiSimilarityScore { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    // Navigation properties
    [ForeignKey("PrimaryComplaintId")]
    public virtual Complaint PrimaryComplaint { get; set; }

    [ForeignKey("CategoryId")]
    public virtual ComplaintCategory Category { get; set; }

    public virtual ICollection<DuplicateEntry> DuplicateEntries { get; set; }
}