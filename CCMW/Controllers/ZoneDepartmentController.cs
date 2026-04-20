using CCMW.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/zone-departments")]
    public class ZoneDepartmentController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        private IHttpActionResult NotFound(string message)
        {
            return Content(HttpStatusCode.NotFound, new { error = message });
        }

        // =====================================================
        // GET ALL ACTIVE ZONE-DEPARTMENT MAPPINGS
        // =====================================================
        [HttpGet]
        [Route("")]
        public IHttpActionResult GetAllMappings([FromUri] bool includeInactive = false)
        {
            try
            {
                var query = db.ZoneDepartments
                    .Include(zd => zd.Zone)
                    .Include(zd => zd.Department)
                    .AsQueryable();

                if (!includeInactive)
                {
                    query = query.Where(zd => zd.IsActive);
                }

                var mappings = query
                    .OrderBy(zd => zd.Zone.ZoneName)
                    .ThenBy(zd => zd.Department.DepartmentName)
                    .Select(zd => new
                    {
                        zd.ZoneDeptId,
                        zd.ZoneId,
                        ZoneName = zd.Zone.ZoneName,
                        ZoneNumber = zd.Zone.ZoneNumber,
                        ZoneCode = zd.Zone.ZoneCode,
                        zd.DepartmentId,
                        DepartmentName = zd.Department.DepartmentName,
                        DepartmentCode = zd.Department.DepartmentCode,
                        zd.StaffCount,
                        zd.ActiveComplaintsCount,
                        zd.IsActive,
                        zd.CreatedAt,
                        zd.BoundaryPolygon,
                        zd.ColorCode,
                        zd.CenterLatitude,
                        zd.CenterLongitude,
                        zd.ServiceAreaSqKm
                    })
                    .ToList();

                return Ok(new
                {
                    TotalMappings = mappings.Count,
                    ActiveMappings = mappings.Count(m => m.IsActive),
                    Mappings = mappings
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET DEPARTMENTS BY ZONE
        // =====================================================
        [HttpGet]
        [Route("zone/{zoneId:guid}")]
        public IHttpActionResult GetDepartmentsByZone(Guid zoneId, [FromUri] bool includeInactive = false)
        {
            try
            {
                var zone = db.Zones.Find(zoneId);
                if (zone == null)
                    return NotFound("Zone not found");

                var query = db.ZoneDepartments
                    .Include(zd => zd.Department)
                    .Where(zd => zd.ZoneId == zoneId);

                if (!includeInactive)
                {
                    query = query.Where(zd => zd.IsActive);
                }

                var mappings = query
                    .OrderBy(zd => zd.Department.DepartmentName)
                    .Select(zd => new
                    {
                        zd.ZoneDeptId,
                        zd.ZoneId,
                        zd.DepartmentId,
                        DepartmentName = zd.Department.DepartmentName,
                        DepartmentCode = zd.Department.DepartmentCode,
                        zd.StaffCount,
                        zd.ActiveComplaintsCount,
                        zd.IsActive,
                        zd.CreatedAt,
                        PerformanceScore = zd.Department.PerformanceScore,
                        zd.BoundaryPolygon,
                        zd.ColorCode,
                        zd.ServiceAreaSqKm
                    })
                    .ToList();

                return Ok(new
                {
                    ZoneId = zoneId,
                    ZoneName = zone.ZoneName,
                    TotalDepartments = mappings.Count,
                    Departments = mappings
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET ZONES BY DEPARTMENT
        // =====================================================
        [HttpGet]
        [Route("department/{departmentId:guid}")]
        public IHttpActionResult GetZonesByDepartment(Guid departmentId, [FromUri] bool includeInactive = false)
        {
            try
            {
                var department = db.Departments.Find(departmentId);
                if (department == null)
                    return NotFound("Department not found");

                var query = db.ZoneDepartments
                    .Include(zd => zd.Zone)
                    .Where(zd => zd.DepartmentId == departmentId);

                if (!includeInactive)
                {
                    query = query.Where(zd => zd.IsActive);
                }

                var mappings = query
                    .OrderBy(zd => zd.Zone.ZoneName)
                    .Select(zd => new
                    {
                        zd.ZoneDeptId,
                        zd.ZoneId,
                        ZoneName = zd.Zone.ZoneName,
                        ZoneNumber = zd.Zone.ZoneNumber,
                        ZoneCode = zd.Zone.ZoneCode,
                        City = zd.Zone.City,
                        Province = zd.Zone.Province,
                        zd.StaffCount,
                        zd.ActiveComplaintsCount,
                        zd.IsActive,
                        zd.CreatedAt,
                        TotalComplaints = zd.Zone.TotalComplaintsCount,
                        PerformanceRating = zd.Zone.PerformanceRating,
                        zd.BoundaryPolygon,
                        zd.ColorCode,
                        zd.ServiceAreaSqKm,
                        CenterLatitude = (double?)zd.CenterLatitude,
                        CenterLongitude = (double?)zd.CenterLongitude
                    })
                    .ToList();

                return Ok(new
                {
                    DepartmentId = departmentId,
                    DepartmentName = department.DepartmentName,
                    TotalZones = mappings.Count,
                    Zones = mappings
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // ASSIGN DEPARTMENT TO ZONE
        // =====================================================
        [HttpPost]
        [Route("assign")]
        public IHttpActionResult AssignDepartmentToZone([FromBody] AssignDepartmentRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest("Request data is required");

                var zone = db.Zones.Find(request.ZoneId);
                if (zone == null)
                    return NotFound("Zone not found");

                var department = db.Departments.Find(request.DepartmentId);
                if (department == null)
                    return NotFound("Department not found");

                var existingMapping = db.ZoneDepartments
                    .FirstOrDefault(zd => zd.ZoneId == request.ZoneId &&
                                         zd.DepartmentId == request.DepartmentId);

                if (existingMapping != null)
                {
                    if (existingMapping.IsActive)
                    {
                        return BadRequest("Department is already assigned to this zone");
                    }
                    else
                    {
                        existingMapping.IsActive = true;
                        existingMapping.StaffCount = request.StaffCount;
                        existingMapping.ActiveComplaintsCount = request.ActiveComplaintsCount ?? 0;
                        existingMapping.BoundaryPolygon = request.BoundaryPolygon;
                        existingMapping.ColorCode = request.ColorCode;
                        existingMapping.CenterLatitude = request.CenterLatitude;
                        existingMapping.CenterLongitude = request.CenterLongitude;
                        existingMapping.ServiceAreaSqKm = request.ServiceAreaSqKm;

                        db.SaveChanges();

                        return Ok(new
                        {
                            Message = "Department reassigned to zone successfully",
                            ZoneDeptId = existingMapping.ZoneDeptId,
                            IsNew = false
                        });
                    }
                }

                var mapping = new ZoneDepartment
                {
                    ZoneDeptId = Guid.NewGuid(),
                    ZoneId = request.ZoneId,
                    DepartmentId = request.DepartmentId,
                    StaffCount = request.StaffCount,
                    ActiveComplaintsCount = request.ActiveComplaintsCount ?? 0,
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    BoundaryPolygon = request.BoundaryPolygon,
                    ColorCode = request.ColorCode,
                    CenterLatitude = request.CenterLatitude,
                    CenterLongitude = request.CenterLongitude,
                    ServiceAreaSqKm = request.ServiceAreaSqKm
                };

                db.ZoneDepartments.Add(mapping);
                db.SaveChanges();

                return Ok(new
                {
                    Message = "Department assigned to zone successfully",
                    ZoneDeptId = mapping.ZoneDeptId,
                    IsNew = true
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // UPDATE MAPPING STATISTICS
        // =====================================================
        [HttpPut]
        [Route("{zoneDeptId:guid}/update-stats")]
        public IHttpActionResult UpdateMappingStats(Guid zoneDeptId, [FromBody] UpdateStatsRequest request)
        {
            try
            {
                var mapping = db.ZoneDepartments.Find(zoneDeptId);
                if (mapping == null)
                    return NotFound("Mapping not found");

                if (request.StaffCount.HasValue)
                    mapping.StaffCount = request.StaffCount.Value;

                if (request.ActiveComplaintsCount.HasValue)
                    mapping.ActiveComplaintsCount = request.ActiveComplaintsCount.Value;

                db.SaveChanges();

                return Ok(new
                {
                    Message = "Statistics updated successfully",
                    ZoneDeptId = zoneDeptId,
                    mapping.StaffCount,
                    mapping.ActiveComplaintsCount
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // UPDATE BOUNDARY POLYGON
        // =====================================================
        [HttpPut]
        [Route("{zoneDeptId:guid}/boundary")]
        public IHttpActionResult UpdateBoundary(Guid zoneDeptId, [FromBody] UpdateBoundaryRequest request)
        {
            try
            {
                var mapping = db.ZoneDepartments.Find(zoneDeptId);
                if (mapping == null)
                    return NotFound("Mapping not found");

                if (!string.IsNullOrEmpty(request.BoundaryPolygon))
                    mapping.BoundaryPolygon = request.BoundaryPolygon;

                if (!string.IsNullOrEmpty(request.ColorCode))
                    mapping.ColorCode = request.ColorCode;

                if (request.CenterLatitude.HasValue)
                    mapping.CenterLatitude = request.CenterLatitude;

                if (request.CenterLongitude.HasValue)
                    mapping.CenterLongitude = request.CenterLongitude;

                if (request.ServiceAreaSqKm.HasValue)
                    mapping.ServiceAreaSqKm = request.ServiceAreaSqKm;

                mapping.PolygonUpdatedAt = DateTime.Now;

                db.SaveChanges();

                return Ok(new
                {
                    Message = "Boundary updated successfully",
                    ZoneDeptId = zoneDeptId
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // TOGGLE ACTIVE STATUS
        // =====================================================
        [HttpPut]
        [Route("{zoneDeptId:guid}/toggle-status")]
        public IHttpActionResult ToggleStatus(Guid zoneDeptId)
        {
            try
            {
                var mapping = db.ZoneDepartments.Find(zoneDeptId);
                if (mapping == null)
                    return NotFound("Mapping not found");

                mapping.IsActive = !mapping.IsActive;
                db.SaveChanges();

                return Ok(new
                {
                    Message = mapping.IsActive ? "Mapping activated" : "Mapping deactivated",
                    ZoneDeptId = zoneDeptId,
                    IsActive = mapping.IsActive
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET ZONE-DEPARTMENT SUMMARY
        // =====================================================
        [HttpGet]
        [Route("summary")]
        public IHttpActionResult GetSummary()
        {
            try
            {
                var totalMappings = db.ZoneDepartments.Count();
                var activeMappings = db.ZoneDepartments.Count(zd => zd.IsActive);
                var totalStaff = db.ZoneDepartments.Sum(zd => (int?)zd.StaffCount) ?? 0;
                var totalActiveComplaints = db.ZoneDepartments.Sum(zd => (int?)zd.ActiveComplaintsCount) ?? 0;

                var topZones = db.ZoneDepartments
                    .Include(zd => zd.Zone)
                    .Where(zd => zd.IsActive)
                    .GroupBy(zd => new { zd.ZoneId, zd.Zone.ZoneName })
                    .Select(g => new
                    {
                        ZoneId = g.Key.ZoneId,
                        ZoneName = g.Key.ZoneName,
                        TotalDepartments = g.Count(),
                        TotalStaff = g.Sum(zd => zd.StaffCount),
                        TotalActiveComplaints = g.Sum(zd => zd.ActiveComplaintsCount)
                    })
                    .OrderByDescending(x => x.TotalActiveComplaints)
                    .Take(5)
                    .ToList();

                var topDepartments = db.ZoneDepartments
                    .Include(zd => zd.Department)
                    .Where(zd => zd.IsActive)
                    .GroupBy(zd => new { zd.DepartmentId, zd.Department.DepartmentName })
                    .Select(g => new
                    {
                        DepartmentId = g.Key.DepartmentId,
                        DepartmentName = g.Key.DepartmentName,
                        TotalZones = g.Count(),
                        TotalStaff = g.Sum(zd => zd.StaffCount),
                        TotalActiveComplaints = g.Sum(zd => zd.ActiveComplaintsCount)
                    })
                    .OrderByDescending(x => x.TotalActiveComplaints)
                    .Take(5)
                    .ToList();

                return Ok(new
                {
                    TotalMappings = totalMappings,
                    ActiveMappings = activeMappings,
                    InactiveMappings = totalMappings - activeMappings,
                    TotalStaffAcrossAllZones = totalStaff,
                    TotalActiveComplaintsAcrossAllZones = totalActiveComplaints,
                    TopZones = topZones,
                    TopDepartments = topDepartments
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

    // DTOs
    public class AssignDepartmentRequest
    {
        public Guid ZoneId { get; set; }
        public Guid DepartmentId { get; set; }
        public int StaffCount { get; set; }
        public int? ActiveComplaintsCount { get; set; }
        public string BoundaryPolygon { get; set; }
        public string ColorCode { get; set; }
        public decimal? CenterLatitude { get; set; }
        public decimal? CenterLongitude { get; set; }
        public decimal? ServiceAreaSqKm { get; set; }
    }

    public class UpdateStatsRequest
    {
        public int? StaffCount { get; set; }
        public int? ActiveComplaintsCount { get; set; }
    }

    public class UpdateBoundaryRequest
    {
        public string BoundaryPolygon { get; set; }
        public string ColorCode { get; set; }
        public decimal? CenterLatitude { get; set; }
        public decimal? CenterLongitude { get; set; }
        public decimal? ServiceAreaSqKm { get; set; }
    }
}