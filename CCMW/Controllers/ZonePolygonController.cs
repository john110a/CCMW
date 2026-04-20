// Controllers/ZonePolygonController.cs
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Http;
using CCMW.Models;
using Newtonsoft.Json;

namespace CCMW.Controllers
{
    [RoutePrefix("api/zone-polygons")]
    public class ZonePolygonController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // Save zone with polygon
        [HttpPost]
        [Route("save")]
        public IHttpActionResult SaveZoneWithPolygon([FromBody] ZonePolygonRequest request)
        {
            try
            {
                var zone = new Zone
                {
                    ZoneId = Guid.NewGuid(),
                    ZoneNumber = request.ZoneNumber,
                    ZoneName = request.ZoneName,
                    ZoneCode = request.ZoneCode,
                    BoundaryPolygon = JsonConvert.SerializeObject(request.Polygon),
                    CenterLatitude = request.CenterLatitude,
                    CenterLongitude = request.CenterLongitude,
                    ColorCode = request.ColorCode,
                    City = request.City,
                    Province = request.Province,
                    TotalAreaSqKm = request.Area,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                db.Zones.Add(zone);
                db.SaveChanges();

                // Save department assignments
                foreach (var dept in request.DepartmentAssignments)
                {
                    var zoneDept = new ZoneDepartment
                    {
                        ZoneDeptId = Guid.NewGuid(),
                        ZoneId = zone.ZoneId,
                        DepartmentId = dept.DepartmentId,
                        BoundaryPolygon = JsonConvert.SerializeObject(dept.Polygon),
                        ColorCode = dept.ColorCode,
                        StaffCount = dept.StaffCount,
                        ActiveComplaintsCount = 0,
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };
                    db.ZoneDepartments.Add(zoneDept);
                }

                db.SaveChanges();

                return Ok(new
                {
                    Message = "Zone created successfully",
                    ZoneId = zone.ZoneId
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // Get zone polygon
        [HttpGet]
        [Route("zone/{zoneId}")]
        public IHttpActionResult GetZonePolygon(Guid zoneId)
        {
            var zone = db.Zones.Find(zoneId);
            if (zone == null)
                return NotFound();

            var polygon = JsonConvert.DeserializeObject(zone.BoundaryPolygon ?? "{}");

            return Ok(new
            {
                zone.ZoneId,
                zone.ZoneName,
                zone.ZoneNumber,
                zone.CenterLatitude,
                zone.CenterLongitude,
                zone.ColorCode,
                zone.TotalAreaSqKm,
                Polygon = polygon
            });
        }

        // Get department zones
        [HttpGet]
        [Route("department/{departmentId}")]
        public IHttpActionResult GetDepartmentZones(Guid departmentId)
        {
            var zoneDepts = db.ZoneDepartments
                .Include(zd => zd.Zone)
                .Where(zd => zd.DepartmentId == departmentId && zd.IsActive)
                .ToList();

            var result = zoneDepts.Select(zd => new
            {
                zd.ZoneDeptId,
                Zone = new
                {
                    zd.Zone.ZoneId,
                    zd.Zone.ZoneName,
                    zd.Zone.ZoneNumber,
                    Polygon = JsonConvert.DeserializeObject(zd.Zone.BoundaryPolygon ?? "{}"),
                    zd.Zone.CenterLatitude,
                    zd.Zone.CenterLongitude
                },
                DepartmentPolygon = JsonConvert.DeserializeObject(zd.BoundaryPolygon ?? "{}"),
                zd.ColorCode,
                zd.StaffCount,
                zd.ActiveComplaintsCount
            });

            return Ok(result);
        }

        // Update department sub-polygon
        [HttpPut]
        [Route("zone-department/{zoneDeptId}")]
        public IHttpActionResult UpdateDepartmentPolygon(Guid zoneDeptId, [FromBody] UpdatePolygonRequest request)
        {
            var zoneDept = db.ZoneDepartments.Find(zoneDeptId);
            if (zoneDept == null)
                return NotFound();

            zoneDept.BoundaryPolygon = JsonConvert.SerializeObject(request.Polygon);
            zoneDept.ColorCode = request.ColorCode;

            db.SaveChanges();

            return Ok(new { Message = "Department polygon updated" });
        }
    }

    public class ZonePolygonRequest
    {
        public int ZoneNumber { get; set; }
        public string ZoneName { get; set; }
        public string ZoneCode { get; set; }
        public object Polygon { get; set; } // GeoJSON
        public decimal? CenterLatitude { get; set; }
        public decimal? CenterLongitude { get; set; }
        public string ColorCode { get; set; }
        public string City { get; set; }
        public string Province { get; set; }
        public decimal Area { get; set; }
        public List<DepartmentAssignmentRequest> DepartmentAssignments { get; set; }
    }

    public class DepartmentAssignmentRequest
    {
        public Guid DepartmentId { get; set; }
        public object Polygon { get; set; } // GeoJSON
        public string ColorCode { get; set; }
        public int StaffCount { get; set; }
    }

    public class UpdatePolygonRequest
    {
        public object Polygon { get; set; }
        public string ColorCode { get; set; }
    }
}