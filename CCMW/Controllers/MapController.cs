using CCMW.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/map")]
    public class MapController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // =====================================================
        // GET ALL COMPLAINTS ON MAP
        // =====================================================
        [HttpGet]
        [Route("complaints")]
        public IHttpActionResult GetMapComplaints(
            [FromUri] Guid? zoneId = null,
            [FromUri] Guid? categoryId = null,
            [FromUri] string status = null,
            [FromUri] double? lat = null,
            [FromUri] double? lng = null,
            [FromUri] double radiusKm = 5.0)
        {
            try
            {
                IQueryable<Complaint> query = db.Complaints
                    .Include("Category")
                    .Include("Zone");

                // Filter by zone
                if (zoneId.HasValue)
                    query = query.Where(c => c.ZoneId == zoneId);

                // Filter by category
                if (categoryId.HasValue)
                    query = query.Where(c => c.CategoryId == categoryId);

                // Filter by status
                if (!string.IsNullOrEmpty(status))
                {
                    if (status == "active")
                        query = query.Where(c => c.CurrentStatus != ComplaintStatus.Resolved &&
                                               c.CurrentStatus != ComplaintStatus.Closed);
                    else if (status == "resolved")
                        query = query.Where(c => c.CurrentStatus == ComplaintStatus.Resolved ||
                                               c.CurrentStatus == ComplaintStatus.Closed);
                }

                // Filter by location radius (if coordinates provided)
                if (lat.HasValue && lng.HasValue)
                {
                    // Simple bounding box filter (1 degree ≈ 111 km)
                    var minLat = lat.Value - (radiusKm / 111.0);
                    var maxLat = lat.Value + (radiusKm / 111.0);
                    var minLng = lng.Value - (radiusKm / (111.0 * Math.Cos(lat.Value * Math.PI / 180)));
                    var maxLng = lng.Value + (radiusKm / (111.0 * Math.Cos(lat.Value * Math.PI / 180)));

                    query = query.Where(c => c.LocationLatitude >= (decimal)minLat &&
                                           c.LocationLatitude <= (decimal)maxLat &&
                                           c.LocationLongitude >= (decimal)minLng &&
                                           c.LocationLongitude <= (decimal)maxLng);
                }

                var complaints = query
                    .OrderByDescending(c => c.CreatedAt)
                    .Take(100) // Limit for performance
                    .Select(c => new
                    {
                        c.ComplaintId,
                        c.ComplaintNumber,
                        c.Title,
                        c.Description,
                        CurrentStatus = (int)c.CurrentStatus,
                        StatusText = GetStatusText(c.CurrentStatus),
                        c.Priority,
                        c.UpvoteCount,
                        Latitude = c.LocationLatitude,
                        Longitude = c.LocationLongitude,
                        c.LocationAddress,
                        Category = new
                        {
                            c.Category.CategoryName,
                            c.Category.IconName,
                            c.Category.ColorCode
                        },
                        Zone = new { c.Zone.ZoneName },
                        HasPhotos = c.ComplaintPhotos.Any(),
                        CreatedDate = c.CreatedAt,
                        TimeAgo = GetTimeAgo(c.CreatedAt)
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    data = complaints,
                    count = complaints.Count,
                    searchParams = new { lat, lng, radiusKm }
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error = ex.Message,
                    data = new List<object>(),
                    count = 0
                });
            }
        }

        // =====================================================
        // GET NEARBY COMPLAINTS - FIXED
        // =====================================================
        [HttpGet]
        [Route("nearby")]
        public IHttpActionResult GetNearbyComplaints(
            [FromUri] double lat,
            [FromUri] double lng,
            [FromUri] double radiusKm = 2.0,
            [FromUri] int limit = 20)
        {
            try
            {
                // First get complaints from database (no distance calculation in DB)
                var complaints = db.Complaints
                    .Include("Category")
                    .Where(c => c.CurrentStatus != ComplaintStatus.Resolved &&
                              c.CurrentStatus != ComplaintStatus.Closed)
                    .ToList(); // Execute query first

                // Then calculate distance in memory
                var result = complaints
                    .Select(c => new
                    {
                        Complaint = c,
                        Distance = CalculateDistance(lat, lng, (double)c.LocationLatitude, (double)c.LocationLongitude)
                    })
                    .Where(x => x.Distance <= radiusKm)
                    .OrderBy(x => x.Distance)
                    .Take(limit)
                    .Select(x => new
                    {
                        x.Complaint.ComplaintId,
                        x.Complaint.ComplaintNumber,
                        x.Complaint.Title,
                        x.Complaint.Description,
                        Status = (int)x.Complaint.CurrentStatus,
                        StatusText = GetStatusText(x.Complaint.CurrentStatus),
                        x.Complaint.Priority,
                        x.Complaint.UpvoteCount,
                        Latitude = x.Complaint.LocationLatitude,
                        Longitude = x.Complaint.LocationLongitude,
                        x.Complaint.LocationAddress,
                        CategoryName = x.Complaint.Category.CategoryName,
                        DistanceKm = Math.Round(x.Distance, 2),
                        HasPhotos = x.Complaint.ComplaintPhotos.Any(),
                        TimeAgo = GetTimeAgo(x.Complaint.CreatedAt)
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    data = result,
                    count = result.Count,
                    center = new { lat, lng },
                    radiusKm = radiusKm
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error = ex.Message,
                    data = new List<object>(),
                    count = 0
                });
            }
        }

        // =====================================================
        // GET ZONE BOUNDARIES - FIXED
        // =====================================================
        [HttpGet]
        [Route("zones")]
        public IHttpActionResult GetZones()
        {
            try
            {
                // First get zones from database
                var zonesFromDb = db.Zones.ToList(); // Execute query first

                // Then parse boundaries in memory
                var zones = zonesFromDb.Select(z => new
                {
                    z.ZoneId,
                    z.ZoneName,
                    z.ZoneNumber,
                    z.ZoneCode,
                    z.City,
                    z.Province,
                    z.TotalAreaSqKm,
                    z.Population,
                    ActiveComplaints = z.ActiveComplaintsCount,
                    TotalComplaints = z.TotalComplaintsCount,
                    z.PerformanceRating,
                    z.ColorCode,
                    CenterLatitude = z.CenterLatitude,
                    CenterLongitude = z.CenterLongitude,
                    Boundaries = ParseBoundaryCoordinates(z.BoundaryCoordinates),
                    HasBoundary = !string.IsNullOrEmpty(z.BoundaryCoordinates)
                }).ToList();

                return Ok(new
                {
                    success = true,
                    data = zones,
                    count = zones.Count
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error = ex.Message,
                    data = new List<object>(),
                    count = 0
                });
            }
        }

        // =====================================================
        // GET COMPLAINT DENSITY BY ZONE
        // =====================================================
        [HttpGet]
        [Route("density")]
        public IHttpActionResult GetComplaintDensity()
        {
            try
            {
                var density = db.Zones
                    .Select(z => new
                    {
                        z.ZoneId,
                        z.ZoneName,
                        z.ZoneNumber,
                        TotalComplaints = z.TotalComplaintsCount,
                        ActiveComplaints = z.ActiveComplaintsCount,
                        ResolvedComplaints = z.TotalComplaintsCount - z.ActiveComplaintsCount,
                        Density = z.TotalComplaintsCount > 0 ?
                                 (double)z.ActiveComplaintsCount / z.TotalComplaintsCount * 100 : 0,
                        Performance = z.PerformanceRating
                    })
                    .OrderByDescending(d => d.Density)
                    .ToList();

                return Ok(new
                {
                    success = true,
                    data = density,
                    totalZones = density.Count,
                    zonesWithComplaints = density.Count(z => z.TotalComplaints > 0)
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error = ex.Message,
                    data = new List<object>()
                });
            }
        }

        // =====================================================
        // GET ZONE STATISTICS
        // =====================================================
        [HttpGet]
        [Route("zone-stats")]
        public IHttpActionResult GetZoneStats()
        {
            try
            {
                var stats = db.Zones
                    .Select(z => new
                    {
                        z.ZoneId,
                        z.ZoneName,
                        z.ZoneNumber,
                        TotalComplaints = z.TotalComplaintsCount,
                        ActiveComplaints = z.ActiveComplaintsCount,
                        ResolvedComplaints = z.TotalComplaintsCount - z.ActiveComplaintsCount,
                        ResolutionRate = z.TotalComplaintsCount > 0 ?
                            ((double)(z.TotalComplaintsCount - z.ActiveComplaintsCount) / z.TotalComplaintsCount * 100) : 0,
                        z.PerformanceRating,
                        z.ColorCode
                    })
                    .OrderByDescending(s => s.ActiveComplaints)
                    .ToList();

                return Ok(new
                {
                    success = true,
                    data = stats,
                    summary = new
                    {
                        totalComplaints = stats.Sum(s => s.TotalComplaints),
                        totalActive = stats.Sum(s => s.ActiveComplaints),
                        totalResolved = stats.Sum(s => s.ResolvedComplaints),
                        averageResolutionRate = stats.Average(s => s.ResolutionRate)
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error = ex.Message,
                    data = new List<object>()
                });
            }
        }

        // =====================================================
        // GET COMPLAINT HEATMAP DATA
        // =====================================================
        [HttpGet]
        [Route("heatmap")]
        public IHttpActionResult GetHeatmapData(
            [FromUri] double? minLat = null,
            [FromUri] double? maxLat = null,
            [FromUri] double? minLng = null,
            [FromUri] double? maxLng = null)
        {
            try
            {
                var query = db.Complaints
                    .Where(c => c.CurrentStatus != ComplaintStatus.Resolved &&
                              c.CurrentStatus != ComplaintStatus.Closed);

                // Apply bounding box filter if provided
                if (minLat.HasValue && maxLat.HasValue && minLng.HasValue && maxLng.HasValue)
                {
                    query = query.Where(c => c.LocationLatitude >= (decimal)minLat &&
                                           c.LocationLatitude <= (decimal)maxLat &&
                                           c.LocationLongitude >= (decimal)minLng &&
                                           c.LocationLongitude <= (decimal)maxLng);
                }

                var heatmapData = query
                    .Select(c => new
                    {
                        Latitude = c.LocationLatitude,
                        Longitude = c.LocationLongitude,
                        Weight = c.UpvoteCount > 0 ? c.UpvoteCount : 1,
                        c.Priority
                    })
                    .ToList();

                return Ok(new
                {
                    success = true,
                    data = heatmapData,
                    count = heatmapData.Count
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error = ex.Message,
                    data = new List<object>()
                });
            }
        }

        // =====================================================
        // HELPER METHODS
        // =====================================================

        private string GetStatusText(ComplaintStatus status)
        {
            switch (status)
            {
                case ComplaintStatus.Submitted: return "Submitted";
                case ComplaintStatus.UnderReview: return "Under Review";
                case ComplaintStatus.Approved: return "Approved";
                case ComplaintStatus.Assigned: return "Assigned";
                case ComplaintStatus.InProgress: return "In Progress";
                case ComplaintStatus.Resolved: return "Resolved";
                case ComplaintStatus.Verified: return "Verified";
                case ComplaintStatus.Rejected: return "Rejected";
                case ComplaintStatus.Closed: return "Closed";
                default: return "Unknown";
            }
        }

        private string GetTimeAgo(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;

            if (timeSpan.TotalMinutes < 1)
                return "Just now";
            if (timeSpan.TotalMinutes < 60)
                return $"{(int)timeSpan.TotalMinutes} min ago";
            if (timeSpan.TotalHours < 24)
                return $"{(int)timeSpan.TotalHours} hour{(timeSpan.TotalHours >= 2 ? "s" : "")} ago";
            if (timeSpan.TotalDays < 7)
                return $"{(int)timeSpan.TotalDays} day{(timeSpan.TotalDays >= 2 ? "s" : "")} ago";
            if (timeSpan.TotalDays < 30)
                return $"{(int)(timeSpan.TotalDays / 7)} week{(timeSpan.TotalDays / 7 >= 2 ? "s" : "")} ago";

            return dateTime.ToString("MMM dd, yyyy");
        }

        // Calculate distance between two coordinates (Haversine formula)
        private double CalculateDistance(double lat1, double lon1, double lat2, double lon2)
        {
            var R = 6371; // Earth's radius in km
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        private double ToRadians(double angle)
        {
            return Math.PI * angle / 180.0;
        }

        // Parse boundary coordinates string
        private object ParseBoundaryCoordinates(string boundaryString)
        {
            if (string.IsNullOrEmpty(boundaryString))
                return null;

            try
            {
                // Try to parse as GeoJSON
                if (boundaryString.Contains("type") && boundaryString.Contains("Polygon"))
                {
                    // Return as string for client to parse
                    return boundaryString;
                }

                // Parse as simple format: "lat1,lng1;lat2,lng2;lat3,lng3"
                var points = boundaryString.Split(';')
                    .Select(p => p.Split(','))
                    .Where(p => p.Length == 2)
                    .Select(p => new
                    {
                        Lat = double.Parse(p[0]),
                        Lng = double.Parse(p[1])
                    })
                    .ToList();

                return points;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing boundary: {ex.Message}");
                return null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}