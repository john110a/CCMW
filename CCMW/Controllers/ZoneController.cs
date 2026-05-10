// Controllers/ZoneController.cs - COMPLETE WITH SUB-ZONE SUPPORT
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

        // =====================================================
        // GET ALL ZONES (Original)
        // =====================================================
        [HttpGet]
        [Route("")]
        public IHttpActionResult GetAllZones()
        {
            try
            {
                var zones = db.Zones
                    .Where(z => z.IsActive)
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
                        z.ParentZoneId,
                        z.Level,
                        z.DisplayOrder,
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

        // =====================================================
        // GET ZONE HIERARCHY (Main Zones with Sub-Zones)
        // =====================================================
        // Controllers/ZoneController.cs - Add this method or update existing one

        // =====================================================
        // GET ZONE HIERARCHY (Main Zones with Sub-Zones) - FIXED
        // =====================================================
        [HttpGet]
        [Route("hierarchy")]
        public IHttpActionResult GetZoneHierarchy()
        {
            try
            {
                // Get all active zones
                var allZones = db.Zones
                    .Where(z => z.IsActive)
                    .ToList();

                if (!allZones.Any())
                {
                    return Ok(new { TotalMainZones = 0, TotalSubZones = 0, Zones = new List<object>() });
                }

                // Get main zones (Level 1 OR no parent zone)
                var mainZones = allZones
                    .Where(z => z.Level == 1 || z.ParentZoneId == null)
                    .OrderBy(z => z.DisplayOrder)
                    .ThenBy(z => z.ZoneName)
                    .ToList();

                var result = new List<object>();

                foreach (var zone in mainZones)
                {
                    // Get sub-zones for this main zone
                    var subZones = allZones
                        .Where(sz => sz.ParentZoneId == zone.ZoneId && sz.IsActive)
                        .OrderBy(sz => sz.DisplayOrder)
                        .ThenBy(sz => sz.ZoneName)
                        .Select(sz => new
                        {
                            sz.ZoneId,
                            sz.ZoneName,
                            sz.ZoneNumber,
                            sz.ZoneCode,
                            sz.City,
                            sz.Province,
                            sz.TotalAreaSqKm,
                            sz.Population,
                            sz.ActiveComplaintsCount,
                            sz.TotalComplaintsCount,
                            sz.PerformanceRating,
                            sz.BoundaryPolygon,
                            sz.CenterLatitude,
                            sz.CenterLongitude,
                            sz.ColorCode,
                            sz.Level,
                            sz.DisplayOrder,
                            sz.ParentZoneId,
                            sz.IsActive,
                            DepartmentAssignments = db.ZoneDepartments
                                .Where(zd => zd.ZoneId == sz.ZoneId && zd.IsActive)
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
                        .ToList();

                    result.Add(new
                    {
                        zone.ZoneId,
                        zone.ZoneName,
                        zone.ZoneNumber,
                        zone.ZoneCode,
                        zone.City,
                        zone.Province,
                        zone.TotalAreaSqKm,
                        zone.Population,
                        zone.ActiveComplaintsCount,
                        zone.TotalComplaintsCount,
                        zone.PerformanceRating,
                        zone.CreatedAt,
                        zone.UpdatedAt,
                        zone.BoundaryPolygon,
                        zone.CenterLatitude,
                        zone.CenterLongitude,
                        zone.ColorCode,
                        zone.IsActive,
                        zone.Level,
                        zone.DisplayOrder,
                        SubZones = subZones
                    });
                }

                int totalSubZones = allZones.Count(z => z.ParentZoneId != null && z.IsActive);

                return Ok(new
                {
                    TotalMainZones = mainZones.Count,
                    TotalSubZones = totalSubZones,
                    Zones = result
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error in GetZoneHierarchy: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"📚 StackTrace: {ex.StackTrace}");

                // Return empty result instead of 500 error
                return Ok(new { TotalMainZones = 0, TotalSubZones = 0, Zones = new List<object>(), Error = ex.Message });
            }
        }

        // =====================================================
        // GET SUB-ZONES BY PARENT ZONE
        // =====================================================
        [HttpGet]
        [Route("{zoneId:guid}/subzones")]
        public IHttpActionResult GetSubZones(Guid zoneId)
        {
            try
            {
                var parentZone = db.Zones.FirstOrDefault(z => z.ZoneId == zoneId);
                if (parentZone == null)
                    return Content(HttpStatusCode.NotFound, new { error = "Zone not found" });

                var subZones = db.Zones
                    .Where(z => z.ParentZoneId == zoneId && z.IsActive)
                    .OrderBy(z => z.DisplayOrder)
                    .ThenBy(z => z.ZoneName)
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
                        z.ActiveComplaintsCount,
                        z.TotalComplaintsCount,
                        z.PerformanceRating,
                        z.BoundaryPolygon,
                        z.CenterLatitude,
                        z.CenterLongitude,
                        z.ColorCode,
                        z.Level,
                        z.DisplayOrder,
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
                    .ToList();

                return Ok(new
                {
                    ParentZoneId = zoneId,
                    ParentZoneName = parentZone.ZoneName,
                    SubZonesCount = subZones.Count,
                    SubZones = subZones
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // CREATE SUB-ZONE
        // =====================================================
        [HttpPost]
        [Route("{parentZoneId:guid}/subzones")]
        public IHttpActionResult CreateSubZone(Guid parentZoneId, [FromBody] CreateSubZoneRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest("Sub-zone data is required.");

                var parentZone = db.Zones.FirstOrDefault(z => z.ZoneId == parentZoneId);
                if (parentZone == null)
                    return Content(HttpStatusCode.NotFound, new { error = "Parent zone not found" });

                if (parentZone.Level != 1)
                    return BadRequest("Sub-zones can only be created under Main Zones (Level 1)");

                // Check if zone number already exists
                if (db.Zones.Any(z => z.ZoneNumber == request.ZoneNumber))
                    return BadRequest($"Zone number {request.ZoneNumber} already exists");

                var subZone = new Zone
                {
                    ZoneId = Guid.NewGuid(),
                    ZoneNumber = request.ZoneNumber,
                    ZoneName = request.ZoneName,
                    ZoneCode = request.ZoneCode ?? $"SUB-{request.ZoneNumber:D3}",
                    City = request.City ?? parentZone.City,
                    Province = request.Province ?? parentZone.Province,
                    Population = request.Population ?? 0,
                    TotalAreaSqKm = request.TotalAreaSqKm.HasValue ? (decimal?)request.TotalAreaSqKm : null,
                    ColorCode = request.ColorCode ?? "#4CAF50",
                    CenterLatitude = request.CenterLatitude.HasValue ? (decimal?)request.CenterLatitude : null,
                    CenterLongitude = request.CenterLongitude.HasValue ? (decimal?)request.CenterLongitude : null,
                    BoundaryPolygon = request.BoundaryPolygon != null ? JsonConvert.SerializeObject(request.BoundaryPolygon) : null,
                    ParentZoneId = parentZoneId,
                    Level = 2, // Sub-zone
                    DisplayOrder = request.DisplayOrder ?? 0,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                db.Zones.Add(subZone);
                db.SaveChanges();

                // Add department assignments if provided
                if (request.DepartmentAssignments != null && request.DepartmentAssignments.Any())
                {
                    foreach (var deptAssignment in request.DepartmentAssignments)
                    {
                        var department = db.Departments.Find(deptAssignment.DepartmentId);
                        if (department != null)
                        {
                            var zoneDepartment = new ZoneDepartment
                            {
                                ZoneDeptId = Guid.NewGuid(),
                                ZoneId = subZone.ZoneId,
                                DepartmentId = deptAssignment.DepartmentId,
                                StaffCount = deptAssignment.StaffCount ?? 0,
                                ActiveComplaintsCount = 0,
                                IsActive = true,
                                CreatedAt = DateTime.Now,
                                BoundaryPolygon = deptAssignment.BoundaryPolygon != null ? JsonConvert.SerializeObject(deptAssignment.BoundaryPolygon) : null,
                                ColorCode = deptAssignment.ColorCode ?? subZone.ColorCode,
                                CenterLatitude = deptAssignment.CenterLatitude.HasValue ? (decimal?)deptAssignment.CenterLatitude : subZone.CenterLatitude,
                                CenterLongitude = deptAssignment.CenterLongitude.HasValue ? (decimal?)deptAssignment.CenterLongitude : subZone.CenterLongitude,
                                ServiceAreaSqKm = deptAssignment.ServiceAreaSqKm.HasValue ? (decimal?)deptAssignment.ServiceAreaSqKm : null
                            };
                            db.ZoneDepartments.Add(zoneDepartment);
                        }
                    }
                    db.SaveChanges();
                }

                return Ok(new
                {
                    Message = "Sub-zone created successfully",
                    SubZoneId = subZone.ZoneId,
                    SubZoneName = subZone.ZoneName,
                    ParentZoneId = parentZoneId,
                    ParentZoneName = parentZone.ZoneName
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error creating sub-zone: {ex.Message}");
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // UPDATE SUB-ZONE
        // =====================================================
        [HttpPut]
        [Route("subzones/{subZoneId:guid}")]
        public IHttpActionResult UpdateSubZone(Guid subZoneId, [FromBody] UpdateSubZoneRequest request)
        {
            try
            {
                var subZone = db.Zones.FirstOrDefault(z => z.ZoneId == subZoneId);
                if (subZone == null)
                    return Content(HttpStatusCode.NotFound, new { error = "Sub-zone not found" });

                if (subZone.Level != 2)
                    return BadRequest("This operation is only for sub-zones");

                if (!string.IsNullOrEmpty(request.ZoneName))
                    subZone.ZoneName = request.ZoneName;

                if (!string.IsNullOrEmpty(request.ZoneCode))
                    subZone.ZoneCode = request.ZoneCode;

                if (request.Population.HasValue)
                    subZone.Population = request.Population.Value;

                if (request.TotalAreaSqKm.HasValue)
                    subZone.TotalAreaSqKm = (decimal?)request.TotalAreaSqKm;

                if (!string.IsNullOrEmpty(request.ColorCode))
                    subZone.ColorCode = request.ColorCode;

                if (request.BoundaryPolygon != null)
                    subZone.BoundaryPolygon = JsonConvert.SerializeObject(request.BoundaryPolygon);

                if (request.CenterLatitude.HasValue)
                    subZone.CenterLatitude = (decimal?)request.CenterLatitude;

                if (request.CenterLongitude.HasValue)
                    subZone.CenterLongitude = (decimal?)request.CenterLongitude;

                if (request.DisplayOrder.HasValue)
                    subZone.DisplayOrder = request.DisplayOrder.Value;

                subZone.UpdatedAt = DateTime.Now;

                db.SaveChanges();

                return Ok(new
                {
                    Message = "Sub-zone updated successfully",
                    SubZoneId = subZoneId,
                    SubZoneName = subZone.ZoneName
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // DELETE SUB-ZONE (Soft delete)
        // =====================================================
        [HttpDelete]
        [Route("subzones/{subZoneId:guid}")]
        public IHttpActionResult DeleteSubZone(Guid subZoneId)
        {
            try
            {
                var subZone = db.Zones.FirstOrDefault(z => z.ZoneId == subZoneId);
                if (subZone == null)
                    return Content(HttpStatusCode.NotFound, new { error = "Sub-zone not found" });

                if (subZone.Level != 2)
                    return BadRequest("This operation is only for sub-zones");

                subZone.IsActive = false;
                subZone.UpdatedAt = DateTime.Now;

                var zoneDepartments = db.ZoneDepartments.Where(zd => zd.ZoneId == subZoneId);
                foreach (var zd in zoneDepartments)
                {
                    zd.IsActive = false;
                }

                db.SaveChanges();

                return Ok(new
                {
                    Message = "Sub-zone deleted successfully",
                    SubZoneId = subZoneId,
                    SubZoneName = subZone.ZoneName
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // REASSIGN SUB-ZONE TO DIFFERENT PARENT ZONE
        // =====================================================
        [HttpPut]
        [Route("subzones/{subZoneId:guid}/reassign")]
        public IHttpActionResult ReassignSubZone(Guid subZoneId, [FromBody] ReassignSubZoneRequest request)
        {
            try
            {
                var subZone = db.Zones.FirstOrDefault(z => z.ZoneId == subZoneId);
                if (subZone == null)
                    return Content(HttpStatusCode.NotFound, new { error = "Sub-zone not found" });

                if (subZone.Level != 2)
                    return BadRequest("This operation is only for sub-zones");

                var newParentZone = db.Zones.FirstOrDefault(z => z.ZoneId == request.NewParentZoneId);
                if (newParentZone == null)
                    return Content(HttpStatusCode.NotFound, new { error = "New parent zone not found" });

                if (newParentZone.Level != 1)
                    return BadRequest("Sub-zones can only be assigned to Main Zones (Level 1)");

                subZone.ParentZoneId = request.NewParentZoneId;
                subZone.UpdatedAt = DateTime.Now;

                db.SaveChanges();

                return Ok(new
                {
                    Message = "Sub-zone reassigned successfully",
                    SubZoneId = subZoneId,
                    SubZoneName = subZone.ZoneName,
                    NewParentZoneId = request.NewParentZoneId,
                    NewParentZoneName = newParentZone.ZoneName
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET ZONE BY ID (Original - Updated with parent info)
        // =====================================================
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
                        z.ParentZoneId,
                        z.Level,
                        z.DisplayOrder,
                        ParentZoneName = z.ParentZone != null ? z.ParentZone.ZoneName : null,
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

        // =====================================================
        // CREATE ZONE (Original - Updated with parent support)
        // =====================================================
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

                if (db.Zones.Any(z => z.ZoneNumber == request.ZoneNumber))
                    return BadRequest($"Zone number {request.ZoneNumber} already exists");

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
                    Level = request.Level == 0 ? 1 : request.Level,
                    DisplayOrder = request.DisplayOrder,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                db.Zones.Add(zone);
                db.SaveChanges();

                if (request.DepartmentAssignments != null && request.DepartmentAssignments.Any())
                {
                    foreach (var deptAssignment in request.DepartmentAssignments)
                    {
                        var department = db.Departments.Find(deptAssignment.DepartmentId);
                        if (department != null)
                        {
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
                    }
                    db.SaveChanges();
                }

                return Ok(new
                {
                    Message = "Zone created successfully",
                    ZoneId = zone.ZoneId,
                    Level = zone.Level,
                    DepartmentCount = request.DepartmentAssignments?.Count ?? 0
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ ERROR creating zone: {ex.Message}");
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // UPDATE ZONE (Original)
        // =====================================================
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
                if (updatedZone.DisplayOrder.HasValue)
                    zone.DisplayOrder = updatedZone.DisplayOrder.Value;

                zone.UpdatedAt = DateTime.Now;
                db.SaveChanges();

                return Ok(new { Message = "Zone updated successfully" });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // DELETE ZONE (Original - Updated to handle sub-zones)
        // =====================================================
        [HttpDelete]
        [Route("{id:guid}")]
        public IHttpActionResult DeleteZone(Guid id)
        {
            try
            {
                var zone = db.Zones.FirstOrDefault(z => z.ZoneId == id);
                if (zone == null)
                    return Content(HttpStatusCode.NotFound, new { error = "Zone not found" });

                // Check if zone has sub-zones
                var hasSubZones = db.Zones.Any(z => z.ParentZoneId == id && z.IsActive);
                if (hasSubZones)
                    return BadRequest("Cannot delete zone with active sub-zones. Delete or reassign sub-zones first.");

                if (db.Users.Any(u => u.ZoneId == id) ||
                    db.StaffProfiles.Any(s => s.ZoneId == id) ||
                    db.Complaints.Any(c => c.ZoneId == id))
                {
                    return BadRequest("Cannot delete zone with existing users, staff, or complaints");
                }

                zone.IsActive = false;
                zone.UpdatedAt = DateTime.Now;

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

        // =====================================================
        // GET ZONE STATISTICS (Original)
        // =====================================================
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
                    TotalDepartments = db.ZoneDepartments.Count(zd => zd.ZoneId == id && zd.IsActive),
                    SubZonesCount = db.Zones.Count(z => z.ParentZoneId == id && z.IsActive)
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

    // =====================================================
    // SUB-ZONE REQUEST DTOs
    // =====================================================

    public class CreateSubZoneRequest
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
        public int? DisplayOrder { get; set; }
        public List<ZoneDepartmentAssignmentRequest> DepartmentAssignments { get; set; }
    }

    public class UpdateSubZoneRequest
    {
        public string ZoneName { get; set; }
        public string ZoneCode { get; set; }
        public int? Population { get; set; }
        public double? TotalAreaSqKm { get; set; }
        public string ColorCode { get; set; }
        public object BoundaryPolygon { get; set; }
        public double? CenterLatitude { get; set; }
        public double? CenterLongitude { get; set; }
        public int? DisplayOrder { get; set; }
    }

    public class ReassignSubZoneRequest
    {
        public Guid NewParentZoneId { get; set; }
    }

    // =====================================================
    // ORIGINAL REQUEST DTOs (UPDATED)
    // =====================================================

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
        public int Level { get; set; } = 1;
        public int DisplayOrder { get; set; } = 0;
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
        public int? DisplayOrder { get; set; }
    }
}