using CCMW.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/duplicates")]
    public class DuplicateManagementController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // =====================================================
        // DEBUG CLUSTERS
        // =====================================================
        [HttpGet]
        [Route("debug-clusters")]
        public IHttpActionResult DebugClusters()
        {
            try
            {
                var result = new Dictionary<string, object>();

                var tableExists = db.Database.SqlQuery<int>(@"
                    SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES 
                    WHERE TABLE_NAME = 'DuplicateClusters'
                ").FirstOrDefault() > 0;

                result["Table_Exists"] = tableExists;

                if (tableExists)
                {
                    var count = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM DuplicateClusters").FirstOrDefault();
                    result["Record_Count"] = count;

                    if (count > 0)
                    {
                        var firstRowData = db.Database.SqlQuery<RawClusterData>(@"
                            SELECT TOP 1 
                                cluster_id,
                                primary_complaint_id,
                                category_id,
                                location_latitude,
                                location_longitude,
                                cluster_radius_meters,
                                total_complaints_merged,
                                total_combined_upvotes,
                                ai_similarity_score,
                                created_at,
                                updated_at
                            FROM DuplicateClusters
                        ").FirstOrDefault();

                        result["First_Row"] = firstRowData;
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return Ok(new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }

        // =====================================================
        // GET DUPLICATE CLUSTERS - FULLY FIXED
        // =====================================================
        [HttpGet]
        [Route("clusters")]
        public IHttpActionResult GetDuplicateClusters([FromUri] int page = 1, [FromUri] int pageSize = 20)
        {
            try
            {
                // Check if any clusters exist
                var clusterCount = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM DuplicateClusters").FirstOrDefault();
                if (clusterCount == 0) return Ok(new List<object>());

                // Get clusters using raw SQL with proper casting
                var clusters = db.Database.SqlQuery<ClusterDto>($@"
                    SELECT 
                        CAST(cluster_id AS uniqueidentifier) as ClusterId,
                        CAST(primary_complaint_id AS uniqueidentifier) as PrimaryComplaintId,
                        CAST(category_id AS uniqueidentifier) as CategoryId,
                        CAST(location_latitude AS decimal(10,6)) as LocationLatitude,
                        CAST(location_longitude AS decimal(10,6)) as LocationLongitude,
                        cluster_radius_meters as ClusterRadiusMeters,
                        total_complaints_merged as TotalComplaintsMerged,
                        total_combined_upvotes as TotalCombinedUpvotes,
                        created_at as CreatedAt,
                        updated_at as UpdatedAt
                    FROM DuplicateClusters
                    ORDER BY created_at DESC
                    OFFSET {(page - 1) * pageSize} ROWS FETCH NEXT {pageSize} ROWS ONLY
                ").ToList();

                var result = new List<object>();

                foreach (var cluster in clusters)
                {
                    // Get primary complaint details
                    var primaryComplaint = db.Database.SqlQuery<PrimaryComplaintDto>($@"
                        SELECT 
                            CAST(c.complaint_id AS uniqueidentifier) as ComplaintId,
                            c.title as Title,
                            ISNULL(c.ComplaintNumber, 'N/A') as ComplaintNumber,
                            ISNULL(c.description, '') as Description,
                            ISNULL(c.location_address, '') as LocationAddress,
                            c.created_at as CreatedAt,
                            ISNULL(cat.category_name, 'General') as CategoryName,
                            ISNULL(z.zone_name, 'Unknown') as ZoneName,
                            ISNULL(u.full_name, 'Unknown') as CitizenName,
                            ISNULL(c.UpvoteCount, 0) as UpvoteCount
                        FROM Complaints c
                        LEFT JOIN Complaint_Categories cat ON c.category_id = cat.category_id
                        LEFT JOIN Zones z ON c.zone_id = z.zone_id
                        LEFT JOIN Users u ON c.citizen_id = u.user_id
                        WHERE c.complaint_id = '{cluster.PrimaryComplaintId}'
                    ").FirstOrDefault();

                    // Get duplicate entries with proper decimal casting
                    var entries = db.Database.SqlQuery<DuplicateEntryDto>($@"
                        SELECT 
                            CAST(e.entry_id AS uniqueidentifier) as EntryId,
                            CAST(e.complaint_id AS uniqueidentifier) as ComplaintId,
                            CAST(e.similarity_score AS decimal(10,2)) as SimilarityScore,
                            e.merged_at as MergedAt,
                            c.title as ComplaintTitle,
                            ISNULL(c.ComplaintNumber, 'N/A') as ComplaintNumber,
                            c.created_at as ComplaintCreatedAt,
                            ISNULL(u.full_name, 'Unknown') as CitizenName
                        FROM DuplicateEntries e
                        INNER JOIN Complaints c ON e.complaint_id = c.complaint_id
                        LEFT JOIN Users u ON c.citizen_id = u.user_id
                        WHERE e.cluster_id = '{cluster.ClusterId}'
                        ORDER BY e.merged_at
                    ").ToList();

                    result.Add(new
                    {
                        cluster.ClusterId,
                        PrimaryComplaint = primaryComplaint != null ? new
                        {
                            primaryComplaint.ComplaintId,
                            primaryComplaint.Title,
                            primaryComplaint.ComplaintNumber,
                            primaryComplaint.Description,
                            primaryComplaint.LocationAddress,
                            primaryComplaint.CreatedAt,
                            CategoryName = primaryComplaint.CategoryName ?? "General",
                            ZoneName = primaryComplaint.ZoneName ?? "Unknown",
                            CitizenName = primaryComplaint.CitizenName ?? "Unknown",
                            UpvoteCount = primaryComplaint.UpvoteCount
                        } : null,
                        TotalComplaintsMerged = cluster.TotalComplaintsMerged,
                        TotalCombinedUpvotes = cluster.TotalCombinedUpvotes,
                        CreatedAt = cluster.CreatedAt,
                        ClusterRadiusMeters = cluster.ClusterRadiusMeters,
                        LocationLatitude = cluster.LocationLatitude,
                        LocationLongitude = cluster.LocationLongitude,
                        DuplicateCount = entries.Count,
                        DuplicateEntries = entries.Select(e => new
                        {
                            e.EntryId,
                            e.ComplaintId,
                            SimilarityScore = e.SimilarityScore,
                            e.MergedAt,
                            Complaint = new
                            {
                                Title = e.ComplaintTitle,
                                ComplaintNumber = e.ComplaintNumber,
                                CreatedAt = e.ComplaintCreatedAt,
                                CitizenName = e.CitizenName
                            }
                        }).ToList()
                    });
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in GetDuplicateClusters: {ex}");
                return Ok(new List<object>());
            }
        }

        // =====================================================
        // GET DUPLICATE STATS
        // =====================================================
        [HttpGet]
        [Route("stats")]
        public IHttpActionResult GetDuplicateStats()
        {
            try
            {
                var totalComplaints = db.Complaints.Count();
                var totalClusters = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM DuplicateClusters").FirstOrDefault();
                var totalDuplicates = db.Database.SqlQuery<int>("SELECT COUNT(*) FROM DuplicateEntries").FirstOrDefault();

                var pendingReview = db.Database.SqlQuery<int>(@"
                    SELECT COUNT(*) FROM DuplicateClusters 
                    WHERE created_at > DATEADD(day, -7, GETUTCDATE())
                ").FirstOrDefault();

                var autoDetectedToday = db.Database.SqlQuery<int>(@"
                    SELECT COUNT(*) FROM DuplicateEntries 
                    WHERE CAST(merged_at AS DATE) = CAST(GETUTCDATE() AS DATE)
                ").FirstOrDefault();

                return Ok(new
                {
                    TotalComplaints = totalComplaints,
                    TotalClusters = totalClusters,
                    TotalDuplicates = totalDuplicates,
                    PendingReview = pendingReview,
                    AutoDetectedToday = autoDetectedToday
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in GetDuplicateStats: {ex}");
                return Ok(new
                {
                    TotalComplaints = db.Complaints.Count(),
                    TotalClusters = 0,
                    TotalDuplicates = 0,
                    PendingReview = 0,
                    AutoDetectedToday = 0
                });
            }
        }

        // =====================================================
        // DETECT POTENTIAL DUPLICATES
        // =====================================================
        [HttpGet]
        [Route("detect")]
        public IHttpActionResult DetectPotentialDuplicates(
            [FromUri] Guid? categoryId = null,
            [FromUri] double? lat = null,
            [FromUri] double? lng = null,
            [FromUri] double radiusMeters = 100,
            [FromUri] int hoursThreshold = 24)
        {
            try
            {
                if (!lat.HasValue || !lng.HasValue)
                {
                    return Ok(new
                    {
                        Message = "Please provide latitude and longitude for duplicate detection",
                        PotentialDuplicates = new List<object>()
                    });
                }

                var baseQuery = db.Complaints
                    .Include(c => c.Category)
                    .Include(c => c.Zone)
                    .Where(c => !c.IsDuplicate && c.MergedIntoComplaintId == null);

                if (categoryId.HasValue)
                {
                    baseQuery = baseQuery.Where(c => c.CategoryId == categoryId.Value);
                }

                var complaints = baseQuery.ToList();
                var potentialDuplicates = new List<object>();

                foreach (var complaint in complaints)
                {
                    var distance = CalculateDistance(
                        lat.Value, lng.Value,
                        (double)complaint.LocationLatitude, (double)complaint.LocationLongitude);

                    if (distance <= (radiusMeters / 1000.0))
                    {
                        var timeDiff = Math.Abs((DateTime.UtcNow - complaint.CreatedAt.ToUniversalTime()).TotalHours);
                        if (timeDiff <= hoursThreshold)
                        {
                            potentialDuplicates.Add(new
                            {
                                Complaint = new
                                {
                                    complaint.ComplaintId,
                                    complaint.Title,
                                    complaint.Description,
                                    CurrentStatus = complaint.CurrentStatus.ToString(),
                                    complaint.CreatedAt,
                                    CategoryName = complaint.Category?.CategoryName ?? "General",
                                    ZoneName = complaint.Zone?.ZoneName ?? "Unknown"
                                },
                                DistanceMeters = Math.Round(distance * 1000, 2),
                                TimeDiffHours = Math.Round(timeDiff, 1),
                                SimilarityScore = Math.Round(CalculateSimilarityScore(complaint, lat.Value, lng.Value), 2)
                            });
                        }
                    }
                }

                var sortedDuplicates = potentialDuplicates
                    .OrderByDescending(d =>
                    {
                        dynamic obj = d;
                        return (double)obj.SimilarityScore;
                    })
                    .ToList();

                return Ok(new
                {
                    PotentialDuplicates = sortedDuplicates,
                    SearchCenter = new { Lat = lat, Lng = lng },
                    SearchRadiusMeters = radiusMeters,
                    TimeThresholdHours = hoursThreshold,
                    TotalFound = sortedDuplicates.Count
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in DetectPotentialDuplicates: {ex}");
                return Ok(new
                {
                    Message = "Error detecting duplicates",
                    PotentialDuplicates = new List<object>()
                });
            }
        }

        // =====================================================
        // COMPARE TWO COMPLAINTS
        // =====================================================
        [HttpGet]
        [Route("compare")]
        public IHttpActionResult CompareComplaints(
            [FromUri] Guid complaintId1,
            [FromUri] Guid complaintId2)
        {
            try
            {
                var complaint1 = db.Complaints
                    .Include(c => c.Category)
                    .Include(c => c.ComplaintPhotos)
                    .FirstOrDefault(c => c.ComplaintId == complaintId1);

                var complaint2 = db.Complaints
                    .Include(c => c.Category)
                    .Include(c => c.ComplaintPhotos)
                    .FirstOrDefault(c => c.ComplaintId == complaintId2);

                if (complaint1 == null || complaint2 == null)
                {
                    return Ok(new { Message = "One or both complaints not found" });
                }

                var distance = CalculateDistance(
                    (double)complaint1.LocationLatitude, (double)complaint1.LocationLongitude,
                    (double)complaint2.LocationLatitude, (double)complaint2.LocationLongitude);

                var timeDiff = Math.Abs((complaint1.CreatedAt - complaint2.CreatedAt).TotalHours);

                var locationScore = distance <= 0.1 ? 100 : Math.Max(0, 100 - (distance * 1000));
                var timeScore = timeDiff <= 24 ? 100 : Math.Max(0, 100 - (timeDiff / 24 * 100));
                var categoryScore = complaint1.CategoryId == complaint2.CategoryId ? 100 : 0;
                var descriptionScore = CalculateTextSimilarity(complaint1.Description, complaint2.Description);

                var totalScore = (locationScore * 0.4) + (timeScore * 0.2) + (categoryScore * 0.2) + (descriptionScore * 0.2);

                return Ok(new
                {
                    Complaint1 = new
                    {
                        complaint1.ComplaintId,
                        complaint1.Title,
                        complaint1.Description,
                        complaint1.CreatedAt,
                        Category = complaint1.Category?.CategoryName ?? "General",
                        Photos = complaint1.ComplaintPhotos?.Select(p => p.PhotoUrl).ToList() ?? new List<string>()
                    },
                    Complaint2 = new
                    {
                        complaint2.ComplaintId,
                        complaint2.Title,
                        complaint2.Description,
                        complaint2.CreatedAt,
                        Category = complaint2.Category?.CategoryName ?? "General",
                        Photos = complaint2.ComplaintPhotos?.Select(p => p.PhotoUrl).ToList() ?? new List<string>()
                    },
                    Comparison = new
                    {
                        DistanceMeters = Math.Round(distance * 1000, 2),
                        TimeDifferenceHours = Math.Round(timeDiff, 1),
                        SameCategory = complaint1.CategoryId == complaint2.CategoryId,
                        SimilarityScores = new
                        {
                            Location = Math.Round(locationScore, 2),
                            Time = Math.Round(timeScore, 2),
                            Category = categoryScore,
                            Description = Math.Round(descriptionScore, 2),
                            Total = Math.Round(totalScore, 2)
                        },
                        IsLikelyDuplicate = totalScore >= 70
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in CompareComplaints: {ex}");
                return Ok(new { Message = "Error comparing complaints" });
            }
        }

        // =====================================================
        // MERGE DUPLICATES
        // =====================================================
        [HttpPost]
        [Route("merge")]
        public IHttpActionResult MergeDuplicates([FromBody] MergeRequest request)
        {
            try
            {
                if (request == null || request.PrimaryComplaintId == Guid.Empty)
                    return BadRequest("Primary complaint ID is required.");

                var primaryComplaint = db.Complaints
                    .FirstOrDefault(c => c.ComplaintId == request.PrimaryComplaintId);

                if (primaryComplaint == null)
                    return Content(HttpStatusCode.NotFound, new { Message = "Primary complaint not found." });

                // Check if cluster already exists
                var existingCluster = db.DuplicateClusters
                    .FirstOrDefault(c => c.PrimaryComplaintId == request.PrimaryComplaintId);

                if (existingCluster != null)
                {
                    return BadRequest("A cluster already exists for this complaint.");
                }

                int primaryUpvotes = primaryComplaint.UpvoteCount;

                var duplicateCluster = new DuplicateCluster
                {
                    ClusterId = Guid.NewGuid(),
                    PrimaryComplaintId = request.PrimaryComplaintId,
                    CategoryId = primaryComplaint.CategoryId,
                    LocationLatitude = primaryComplaint.LocationLatitude,
                    LocationLongitude = primaryComplaint.LocationLongitude,
                    ClusterRadiusMeters = (int)request.RadiusMeters,
                    TotalComplaintsMerged = 1,
                    TotalCombinedUpvotes = primaryUpvotes,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                db.DuplicateClusters.Add(duplicateCluster);

                var primaryEntry = new DuplicateEntry
                {
                    EntryId = Guid.NewGuid(),
                    ClusterId = duplicateCluster.ClusterId,
                    ComplaintId = request.PrimaryComplaintId,
                    SimilarityScore = 100,
                    SimilarityFactors = "{\"type\":\"primary\"}",
                    MergedAt = DateTime.UtcNow,
                    MergedById = request.MergedByUserId
                };
                db.DuplicateEntries.Add(primaryEntry);

                int mergedCount = 1;
                int totalUpvotes = primaryUpvotes;

                if (request.DuplicateComplaintIds != null)
                {
                    foreach (var duplicateId in request.DuplicateComplaintIds)
                    {
                        if (duplicateId == request.PrimaryComplaintId)
                            continue;

                        var duplicateComplaint = db.Complaints
                            .FirstOrDefault(c => c.ComplaintId == duplicateId);

                        if (duplicateComplaint != null)
                        {
                            duplicateComplaint.IsDuplicate = true;
                            duplicateComplaint.MergedIntoComplaintId = request.PrimaryComplaintId;
                            duplicateComplaint.UpdatedAt = DateTime.UtcNow;

                            totalUpvotes += duplicateComplaint.UpvoteCount;

                            decimal similarityScore = 0;
                            if (request.SimilarityScores != null && request.SimilarityScores.ContainsKey(duplicateId))
                            {
                                similarityScore = request.SimilarityScores[duplicateId];
                            }
                            else
                            {
                                similarityScore = CalculateSimilarityScore(primaryComplaint, duplicateComplaint);
                            }

                            var duplicateEntry = new DuplicateEntry
                            {
                                EntryId = Guid.NewGuid(),
                                ClusterId = duplicateCluster.ClusterId,
                                ComplaintId = duplicateId,
                                SimilarityScore = similarityScore,
                                SimilarityFactors = "{\"merged\":true}",
                                MergedAt = DateTime.UtcNow,
                                MergedById = request.MergedByUserId
                            };
                            db.DuplicateEntries.Add(duplicateEntry);

                            mergedCount++;
                        }
                    }
                }

                // Update primary complaint's upvote count
                primaryComplaint.UpvoteCount = totalUpvotes;

                duplicateCluster.TotalComplaintsMerged = mergedCount;
                duplicateCluster.TotalCombinedUpvotes = totalUpvotes;

                db.SaveChanges();

                return Ok(new
                {
                    Message = $"Successfully merged {mergedCount - 1} duplicates into primary complaint",
                    ClusterId = duplicateCluster.ClusterId,
                    PrimaryComplaintId = request.PrimaryComplaintId,
                    MergedCount = mergedCount - 1,
                    TotalUpvotes = totalUpvotes
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in MergeDuplicates: {ex}");
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // PROCESS ALL EXISTING COMPLAINTS
        // =====================================================
        [HttpPost]
        [Route("process-all-existing")]
        public IHttpActionResult ProcessAllExistingComplaints()
        {
            try
            {
                var allComplaints = db.Complaints
                    .Include(c => c.Category)
                    .Where(c => !c.IsDuplicate && c.MergedIntoComplaintId == null)
                    .OrderBy(c => c.CreatedAt)
                    .ToList();

                int clustersCreated = 0;
                int complaintsProcessed = 0;
                int skipped = 0;

                foreach (var complaint in allComplaints)
                {
                    if (complaint.IsDuplicate || complaint.MergedIntoComplaintId != null)
                        continue;

                    var similar = FindSimilarExistingComplaints(complaint, allComplaints);

                    if (similar.Any())
                    {
                        int complaintUpvotes = complaint.UpvoteCount;
                        int similarUpvotes = similar.Sum(s => s.UpvoteCount);

                        var cluster = new DuplicateCluster
                        {
                            ClusterId = Guid.NewGuid(),
                            PrimaryComplaintId = complaint.ComplaintId,
                            CategoryId = complaint.CategoryId,
                            LocationLatitude = complaint.LocationLatitude,
                            LocationLongitude = complaint.LocationLongitude,
                            ClusterRadiusMeters = 200,
                            TotalComplaintsMerged = similar.Count + 1,
                            TotalCombinedUpvotes = complaintUpvotes + similarUpvotes,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        db.DuplicateClusters.Add(cluster);

                        db.DuplicateEntries.Add(new DuplicateEntry
                        {
                            EntryId = Guid.NewGuid(),
                            ClusterId = cluster.ClusterId,
                            ComplaintId = complaint.ComplaintId,
                            SimilarityScore = 100,
                            SimilarityFactors = "{\"type\":\"primary\"}",
                            MergedAt = DateTime.UtcNow
                        });

                        foreach (var dup in similar)
                        {
                            dup.IsDuplicate = true;
                            dup.MergedIntoComplaintId = complaint.ComplaintId;
                            dup.UpdatedAt = DateTime.UtcNow;

                            db.DuplicateEntries.Add(new DuplicateEntry
                            {
                                EntryId = Guid.NewGuid(),
                                ClusterId = cluster.ClusterId,
                                ComplaintId = dup.ComplaintId,
                                SimilarityScore = CalculateSimilarityScore(complaint, dup),
                                SimilarityFactors = "{\"merged\":true}",
                                MergedAt = DateTime.UtcNow
                            });
                        }

                        complaint.UpvoteCount = complaintUpvotes + similarUpvotes;

                        clustersCreated++;
                        complaintsProcessed += similar.Count + 1;
                    }
                    else
                    {
                        skipped++;
                    }
                }

                db.SaveChanges();

                return Ok(new
                {
                    Message = "Existing complaints processed successfully",
                    TotalComplaints = allComplaints.Count,
                    ClustersCreated = clustersCreated,
                    ComplaintsMerged = complaintsProcessed,
                    ComplaintsSkipped = skipped,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in ProcessAllExistingComplaints: {ex}");
                return Ok(new
                {
                    Message = "Error processing complaints",
                    Error = ex.Message
                });
            }
        }

        // =====================================================
        // PRIVATE HELPER METHODS
        // =====================================================

        private List<Complaint> FindSimilarExistingComplaints(Complaint target, List<Complaint> allComplaints)
        {
            return allComplaints
                .Where(c => c.ComplaintId != target.ComplaintId)
                .Where(c => c.CategoryId == target.CategoryId)
                .Where(c => !c.IsDuplicate && c.MergedIntoComplaintId == null)
                //.Where(c => Math.Abs((c.CreatedAt - target.CreatedAt).TotalDays) <= 30)
                .Where(c => CalculateDistance(
                    (double)c.LocationLatitude, (double)c.LocationLongitude,
                    (double)target.LocationLatitude, (double)target.LocationLongitude) < 0.2)
                .ToList();
        }

        private decimal CalculateSimilarityScore(Complaint c1, Complaint c2)
        {
            decimal score = 0;

            double distance = CalculateDistance(
                (double)c1.LocationLatitude, (double)c1.LocationLongitude,
                (double)c2.LocationLatitude, (double)c2.LocationLongitude);

            if (distance <= 0.1)
                score += 40;
            else if (distance <= 0.2)
                score += 30;
            else if (distance <= 0.5)
                score += 20;

            double daysDiff = Math.Abs((c1.CreatedAt - c2.CreatedAt).TotalDays);
            if (daysDiff <= 1)
                score += 30;
            else if (daysDiff <= 3)
                score += 20;
            else if (daysDiff <= 7)
                score += 10;

            score += 20;

            if (!string.IsNullOrEmpty(c1.Title) && !string.IsNullOrEmpty(c2.Title))
            {
                var words1 = c1.Title.ToLower().Split(' ');
                var words2 = c2.Title.ToLower().Split(' ');
                var common = words1.Intersect(words2).Count();
                var total = words1.Union(words2).Count();

                if (total > 0)
                    score += (decimal)((double)common / total * 10);
            }

            return Math.Min(score, 100);
        }

        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371;
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double angle) => Math.PI * angle / 180.0;

        private double CalculateTextSimilarity(string text1, string text2)
        {
            if (string.IsNullOrEmpty(text1) || string.IsNullOrEmpty(text2))
                return 0;

            var words1 = text1.ToLower().Split(new[] { ' ', '.', ',', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
            var words2 = text2.ToLower().Split(new[] { ' ', '.', ',', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);

            var commonWords = words1.Intersect(words2).Count();
            var totalWords = words1.Union(words2).Count();

            return totalWords > 0 ? (double)commonWords / totalWords * 100 : 0;
        }

        private double CalculateSimilarityScore(Complaint complaint, double lat, double lng)
        {
            var distance = CalculateDistance(lat, lng,
                (double)complaint.LocationLatitude, (double)complaint.LocationLongitude);
            var distanceScore = Math.Max(0, 100 - (distance * 1000));

            var timeDiff = Math.Abs((DateTime.UtcNow - complaint.CreatedAt.ToUniversalTime()).TotalHours);
            var timeScore = Math.Max(0, 100 - (timeDiff / 24 * 100));

            return (distanceScore * 0.6) + (timeScore * 0.4);
        }

        // =====================================================
        // DTO CLASSES
        // =====================================================

        private class RawClusterData
        {
            public Guid cluster_id { get; set; }
            public Guid primary_complaint_id { get; set; }
            public Guid? category_id { get; set; }
            public decimal location_latitude { get; set; }
            public decimal location_longitude { get; set; }
            public int cluster_radius_meters { get; set; }
            public int total_complaints_merged { get; set; }
            public int total_combined_upvotes { get; set; }
            public decimal? ai_similarity_score { get; set; }
            public DateTime created_at { get; set; }
            public DateTime updated_at { get; set; }
        }

        private class ClusterDto
        {
            public Guid ClusterId { get; set; }
            public Guid PrimaryComplaintId { get; set; }
            public Guid? CategoryId { get; set; }
            public decimal LocationLatitude { get; set; }
            public decimal LocationLongitude { get; set; }
            public int ClusterRadiusMeters { get; set; }
            public int TotalComplaintsMerged { get; set; }
            public int TotalCombinedUpvotes { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
        }

        private class PrimaryComplaintDto
        {
            public Guid ComplaintId { get; set; }
            public string Title { get; set; }
            public string ComplaintNumber { get; set; }
            public string Description { get; set; }
            public string LocationAddress { get; set; }
            public DateTime CreatedAt { get; set; }
            public string CategoryName { get; set; }
            public string ZoneName { get; set; }
            public string CitizenName { get; set; }
            public int UpvoteCount { get; set; }
        }

        private class DuplicateEntryDto
        {
            public Guid EntryId { get; set; }
            public Guid ComplaintId { get; set; }
            public decimal SimilarityScore { get; set; }
            public DateTime? MergedAt { get; set; }
            public string ComplaintTitle { get; set; }
            public string ComplaintNumber { get; set; }
            public DateTime ComplaintCreatedAt { get; set; }
            public string CitizenName { get; set; }
        }

        public class MergeRequest
        {
            public Guid PrimaryComplaintId { get; set; }
            public List<Guid> DuplicateComplaintIds { get; set; }
            public Dictionary<Guid, decimal> SimilarityScores { get; set; }
            public Guid MergedByUserId { get; set; }
            public decimal RadiusMeters { get; set; } = 100;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                db.Dispose();
            base.Dispose(disposing);
        }
    }
}