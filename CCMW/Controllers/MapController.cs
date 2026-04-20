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

        // GET ALL COMPLAINTS ON MAP
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
                // Simple bounding box filter (for demo - use proper spatial queries in production)
                var minLat = lat.Value - (radiusKm / 111.0); // 1 degree ≈ 111 km
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
                    c.CurrentStatus,
                    c.Priority,
                    c.UpvoteCount,
                    Latitude = c.LocationLatitude,
                    Longitude = c.LocationLongitude,
                    c.LocationAddress,
                    Category = new { c.Category.CategoryName, c.Category.IconName, c.Category.ColorCode },
                    Zone = new { c.Zone.ZoneName },
                    HasPhotos = c.ComplaintPhotos.Any(),
                    CreatedDate = c.CreatedAt
                })
                .ToList();

            return Ok(new
            {
                success = true,
                data = complaints,
                count = complaints.Count
            });
        }

        // GET NEARBY COMPLAINTS
        [HttpGet]
        [Route("nearby")]
        public IHttpActionResult GetNearbyComplaints(
            [FromUri] double lat,
            [FromUri] double lng,
            [FromUri] double radiusKm = 2.0,
            [FromUri] int limit = 20)
        {
            // Simple distance calculation (for demo)
            var complaints = db.Complaints
                .Include("Category")
                .Where(c => c.CurrentStatus != ComplaintStatus.Resolved &&
                          c.CurrentStatus != ComplaintStatus.Closed)
                .AsEnumerable() // Switch to LINQ to Objects for distance calculation
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
                    x.Complaint.Title,
                    x.Complaint.CurrentStatus,
                    x.Complaint.UpvoteCount,
                    Latitude = x.Complaint.LocationLatitude,
                    Longitude = x.Complaint.LocationLongitude,
                    x.Complaint.LocationAddress,
                    Category = x.Complaint.Category.CategoryName,
                    DistanceKm = Math.Round(x.Distance, 2)
                })
                .ToList();

            return Ok(complaints);
        }

        // GET ZONE BOUNDARIES
        [HttpGet]
        [Route("zones")]
        public IHttpActionResult GetZones()
        {
            var zones = db.Zones
                .Select(z => new
                {
                    z.ZoneId,
                    z.ZoneName,
                    z.ZoneCode,
                    z.City,
                    z.Province,
                    z.TotalAreaSqKm,
                    z.Population,
                    z.ActiveComplaintsCount,
                    z.PerformanceRating,
                    // Parse boundary coordinates if stored as string
                    Boundaries = ParseBoundaryCoordinates(z.BoundaryCoordinates)
                })
                .ToList();

            return Ok(zones);
        }

        // GET COMPLAINT DENSITY BY ZONE
        [HttpGet]
        [Route("density")]
        public IHttpActionResult GetComplaintDensity()
        {
            var density = db.Zones
                .Select(z => new
                {
                    z.ZoneId,
                    z.ZoneName,
                    TotalComplaints = z.TotalComplaintsCount,
                    ActiveComplaints = z.ActiveComplaintsCount,
                    Density = z.TotalComplaintsCount > 0 ?
                             (double)z.ActiveComplaintsCount / z.TotalComplaintsCount * 100 : 0
                })
                .OrderByDescending(d => d.Density)
                .ToList();

            return Ok(density);
        }

        // HELPER: Calculate distance between two coordinates (Haversine formula)
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

        // HELPER: Parse boundary coordinates string
        private object ParseBoundaryCoordinates(string boundaryString)
        {
            if (string.IsNullOrEmpty(boundaryString))
                return null;

            try
            {
                // Assuming format: "lat1,lng1;lat2,lng2;lat3,lng3"
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
            catch
            {
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