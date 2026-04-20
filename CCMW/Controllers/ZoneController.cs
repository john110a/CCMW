// Controllers/ZoneController.cs - COMPLETE FIXED VERSION
using CCMW.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/zones")]
    public class ZoneController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // GET api/zones
        [HttpGet]
        [Route("")]
        public IHttpActionResult GetAllZones()
        {
            try
            {
                var zones = db.Zones
                    .Select(z => new
                    {
                        z.ZoneId,
                        z.ZoneName,
                        z.ZoneNumber,
                        z.ZoneCode,
                        z.City,
                        z.Province,
                        z.TotalAreaSqKm,
                        z.Population,
                        z.ColorCode,
                        z.CenterLatitude,
                        z.CenterLongitude,
                        z.BoundaryPolygon,
                        z.IsActive,
                        z.CreatedAt,
                        DepartmentAssignments = db.ZoneDepartments
                            .Where(zd => zd.ZoneId == z.ZoneId && zd.IsActive)
                            .Select(zd => new
                            {
                                zd.ZoneDeptId,
                                zd.DepartmentId,
                                DepartmentName = zd.Department.DepartmentName,
                                zd.StaffCount,
                                zd.ActiveComplaintsCount,
                                zd.ColorCode
                            })
                            .ToList()
                    })
                    .OrderBy(z => z.ZoneNumber)
                    .ToList();

                return Ok(zones);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/zones/{id}
        [HttpGet]
        [Route("{id:guid}")]
        public IHttpActionResult GetZone(Guid id)
        {
            try
            {
                var zone = db.Zones
                    .Where(z => z.ZoneId == id)
                    .Select(z => new
                    {
                        z.ZoneId,
                        z.ZoneName,
                        z.ZoneNumber,
                        z.ZoneCode,
                        z.City,
                        z.Province,
                        z.TotalAreaSqKm,
                        z.Population,
                        z.ColorCode,
                        z.CenterLatitude,
                        z.CenterLongitude,
                        z.BoundaryPolygon,
                        z.IsActive,
                        z.CreatedAt,
                        DepartmentAssignments = db.ZoneDepartments
                            .Where(zd => zd.ZoneId == z.ZoneId && zd.IsActive)
                            .Select(zd => new
                            {
                                zd.ZoneDeptId,
                                zd.DepartmentId,
                                DepartmentName = zd.Department.DepartmentName,
                                zd.StaffCount,
                                zd.ActiveComplaintsCount,
                                zd.ColorCode
                            })
                            .ToList()
                    })
                    .FirstOrDefault();

                if (zone == null)
                    return Content(HttpStatusCode.NotFound, new { error = "Zone not found" });

                return Ok(zone);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/zones - Creates zone with department assignments
        // POST api/zones - Creates zone with department assignments
        [HttpPost]
        [Route("")]
        public IHttpActionResult CreateZone([FromBody] ZoneCreateRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest("Zone data required");

                if (string.IsNullOrEmpty(request.ZoneName))
                    return BadRequest("Zone name is required");

                // Check if zone number already exists
                if (db.Zones.Any(z => z.ZoneNumber == request.ZoneNumber))
                    return BadRequest($"Zone number {request.ZoneNumber} already exists");

                // Create the zone
                var zone = new Zone
                {
                    ZoneId = Guid.NewGuid(),
                    ZoneName = request.ZoneName,
                    ZoneNumber = request.ZoneNumber,
                    ZoneCode = request.ZoneCode ?? $"Z{request.ZoneNumber:D3}",
                    City = request.City ?? "Islamabad",
                    Province = request.Province ?? "ICT",
                    Population = request.Population ?? 0,
                    TotalAreaSqKm = request.TotalAreaSqKm.HasValue ? (decimal?)request.TotalAreaSqKm : null,
                    ColorCode = request.ColorCode ?? "#2196F3",
                    CenterLatitude = request.CenterLatitude.HasValue ? (decimal?)request.CenterLatitude : null,
                    CenterLongitude = request.CenterLongitude.HasValue ? (decimal?)request.CenterLongitude : null,
                    BoundaryPolygon = request.BoundaryPolygon != null ? JsonConvert.SerializeObject(request.BoundaryPolygon) : null,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                db.Zones.Add(zone);
                db.SaveChanges(); // Save zone first to get ZoneId

                // Add department assignments to ZoneDepartments table
                if (request.DepartmentAssignments != null && request.DepartmentAssignments.Any())
                {
                    foreach (var deptAssignment in request.DepartmentAssignments)
                    {
                        // Verify department exists
                        var department = db.Departments.Find(deptAssignment.DepartmentId);
                        if (department == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"⚠️ Department not found: {deptAssignment.DepartmentId}");
                            continue; // Skip invalid departments
                        }

                        var zoneDepartment = new ZoneDepartment
                        {
                            ZoneDeptId = Guid.NewGuid(),
                            ZoneId = zone.ZoneId,
                            DepartmentId = deptAssignment.DepartmentId,
                            StaffCount = deptAssignment.StaffCount ?? 0,
                            ActiveComplaintsCount = 0,
                            IsActive = true,
                            CreatedAt = DateTime.Now,
                            BoundaryPolygon = deptAssignment.BoundaryPolygon != null ? JsonConvert.SerializeObject(deptAssignment.BoundaryPolygon) : null,
                            ColorCode = deptAssignment.ColorCode ?? zone.ColorCode,
                            CenterLatitude = deptAssignment.CenterLatitude.HasValue ? (decimal?)deptAssignment.CenterLatitude : zone.CenterLatitude,
                            CenterLongitude = deptAssignment.CenterLongitude.HasValue ? (decimal?)deptAssignment.CenterLongitude : zone.CenterLongitude,
                            ServiceAreaSqKm = deptAssignment.ServiceAreaSqKm.HasValue ? (decimal?)deptAssignment.ServiceAreaSqKm : null
                        };
                        db.ZoneDepartments.Add(zoneDepartment);
                    }

                    db.SaveChanges(); // Save department assignments
                }

                return Ok(new
                {
                    Message = "Zone created successfully",
                    ZoneId = zone.ZoneId,
                    DepartmentCount = request.DepartmentAssignments?.Count ?? 0
                });
            }
            catch (Exception ex)
            {
                // Log the error to Output window
                System.Diagnostics.Debug.WriteLine($"❌ ERROR creating zone: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"📚 StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine($"🔍 Inner Exception: {ex.InnerException.Message}");
                }

                // Return the actual exception (this will fix the error)
                return InternalServerError(ex);
            }
        }

        // PUT api/zones/{id}
        [HttpPut]
        [Route("{id:guid}")]
        public IHttpActionResult UpdateZone(Guid id, [FromBody] ZoneUpdateRequest updatedZone)
        {
            try
            {
                var zone = db.Zones.FirstOrDefault(z => z.ZoneId == id);
                if (zone == null)
                    return Content(HttpStatusCode.NotFound, new { error = "Zone not found" });

                if (!string.IsNullOrEmpty(updatedZone.ZoneName))
                    zone.ZoneName = updatedZone.ZoneName;
                if (!string.IsNullOrEmpty(updatedZone.ZoneCode))
                    zone.ZoneCode = updatedZone.ZoneCode;
                if (!string.IsNullOrEmpty(updatedZone.City))
                    zone.City = updatedZone.City;
                if (!string.IsNullOrEmpty(updatedZone.Province))
                    zone.Province = updatedZone.Province;
                if (updatedZone.TotalAreaSqKm.HasValue)
                    zone.TotalAreaSqKm = updatedZone.TotalAreaSqKm;
                if (updatedZone.Population.HasValue)
                    zone.Population = updatedZone.Population.Value;

                zone.UpdatedAt = DateTime.Now;

                db.SaveChanges();

                return Ok(new { Message = "Zone updated successfully" });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // DELETE api/zones/{id}
        [HttpDelete]
        [Route("{id:guid}")]
        public IHttpActionResult DeleteZone(Guid id)
        {
            try
            {
                var zone = db.Zones.FirstOrDefault(z => z.ZoneId == id);
                if (zone == null)
                    return Content(HttpStatusCode.NotFound, new { error = "Zone not found" });

                // Check if zone has dependencies
                if (db.Users.Any(u => u.ZoneId == id) ||
                    db.StaffProfiles.Any(s => s.ZoneId == id) ||
                    db.Complaints.Any(c => c.ZoneId == id))
                {
                    return BadRequest("Cannot delete zone with existing users, staff, or complaints");
                }

                // Soft delete - set IsActive to false instead of hard delete
                zone.IsActive = false;
                zone.UpdatedAt = DateTime.Now;

                // Also soft delete department assignments
                var zoneDepartments = db.ZoneDepartments.Where(zd => zd.ZoneId == id);
                foreach (var zd in zoneDepartments)
                {
                    zd.IsActive = false;
                }

                db.SaveChanges();

                return Ok(new { Message = "Zone deactivated successfully" });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/zones/{id}/statistics
        [HttpGet]
        [Route("{id:guid}/statistics")]
        public IHttpActionResult GetZoneStatistics(Guid id)
        {
            try
            {
                var zone = db.Zones.FirstOrDefault(z => z.ZoneId == id);
                if (zone == null)
                    return Content(HttpStatusCode.NotFound, new { error = "Zone not found" });

                var stats = new
                {
                    TotalComplaints = db.Complaints.Count(c => c.ZoneId == id),
                    ActiveComplaints = db.Complaints.Count(c => c.ZoneId == id &&
                        c.CurrentStatus != ComplaintStatus.Resolved &&
                        c.CurrentStatus != ComplaintStatus.Closed),
                    ResolvedComplaints = db.Complaints.Count(c => c.ZoneId == id &&
                        c.CurrentStatus == ComplaintStatus.Resolved),
                    TotalUsers = db.Users.Count(u => u.ZoneId == id),
                    TotalStaff = db.StaffProfiles.Count(s => s.ZoneId == id),
                    TotalDepartments = db.ZoneDepartments.Count(zd => zd.ZoneId == id && zd.IsActive)
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }

    // Request DTOs
    public class ZoneCreateRequest
    {
        public string ZoneName { get; set; }
        public int ZoneNumber { get; set; }
        public string ZoneCode { get; set; }
        public string City { get; set; }
        public string Province { get; set; }
        public int? Population { get; set; }
        public double? TotalAreaSqKm { get; set; }
        public string ColorCode { get; set; }
        public double? CenterLatitude { get; set; }
        public double? CenterLongitude { get; set; }
        public object BoundaryPolygon { get; set; }
        public List<ZoneDepartmentAssignmentRequest> DepartmentAssignments { get; set; }
    }

    public class ZoneDepartmentAssignmentRequest
    {
        public Guid DepartmentId { get; set; }
        public string DepartmentName { get; set; }
        public int? StaffCount { get; set; }
        public object BoundaryPolygon { get; set; }
        public string ColorCode { get; set; }
        public double? CenterLatitude { get; set; }
        public double? CenterLongitude { get; set; }
        public double? ServiceAreaSqKm { get; set; }
    }

    public class ZoneUpdateRequest
    {
        public string ZoneName { get; set; }
        public string ZoneCode { get; set; }
        public string City { get; set; }
        public string Province { get; set; }
        public decimal? TotalAreaSqKm { get; set; }
        public int? Population { get; set; }
    }
}