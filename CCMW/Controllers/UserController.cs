using CCMW.Models;
using CCMW.DTOs;
using System;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/user")]
    public class UserController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        [HttpPost]
        [Route("register")]
        public IHttpActionResult Register([FromBody] RegisterRequest request)
        {
            try
            {
                // Validate request
                if (request == null)
                    return BadRequest("User data is required.");

                if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.PasswordHash))
                    return BadRequest("Email and Password are required.");

                if (string.IsNullOrEmpty(request.FullName))
                    return BadRequest("Full name is required.");

                // Force user type to be Citizen
                request.UserType = "Citizen";

                // Check if email already exists
                if (db.Users.Any(u => u.Email == request.Email))
                    return BadRequest("Email already registered.");

                // Hash the password
                string hashedPassword = request.PasswordHash;

                // Create new user
                var user = new User
                {
                    UserId = Guid.NewGuid(),
                    Email = request.Email,
                    PasswordHash = hashedPassword,
                    FullName = request.FullName,
                    PhoneNumber = request.PhoneNumber,
                    CNIC = request.CNIC,
                    Address = request.Address,
                    ZoneId = request.ZoneId,
                    UserType = "Citizen",
                    IsActive = true,
                    IsVerified = false,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };

                db.Users.Add(user);
                db.SaveChanges();

                // Create citizen profile with timestamps
                var citizenProfile = new CitizenProfile
                {
                    CitizenId = user.UserId,
                    UserId = user.UserId,
                    TotalComplaintsFiled = 0,
                    ApprovedComplaintsCount = 0,
                    ResolvedComplaintsCount = 0,
                    RejectedComplaintsCount = 0,
                    ContributionScore = 0,
                    LeaderboardRank = 0,
                    BadgeLevel = "Newcomer",
                    TotalUpvotesReceived = 0,
                    CreatedAt = DateTime.Now,  // ✅ Now works with database
                    UpdatedAt = DateTime.Now   // ✅ Now works with database
                };

                db.CitizenProfiles.Add(citizenProfile);
                db.SaveChanges();

                return Ok(new
                {
                    Message = "Citizen registered successfully.",
                    UserId = user.UserId,
                    UserType = user.UserType,
                    FullName = user.FullName
                });
            }
            catch (Exception ex)
            {
                // Log the full exception
                System.Diagnostics.Debug.WriteLine(ex.ToString());
                return InternalServerError(ex);
            }
        }
        // =====================================================
        // UNIVERSAL LOGIN - Works for all user types
        // =====================================================
        [HttpPost]
        [Route("login")]
        public IHttpActionResult Login([FromBody] LoginRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.PasswordHash))
                    return BadRequest("Email and Password are required.");

                // Find user by email only first (don't include password in query to avoid timing attacks)
                var user = db.Users
                    .AsNoTracking()
                    .FirstOrDefault(u => u.Email == request.Email);

                if (user == null)
                    return Unauthorized("Invalid email or password");

                // Verify password (in production, use proper hashing comparison)
                if (user.PasswordHash != request.PasswordHash)
                    return Unauthorized("Invalid email or password");

                // Check if user is active
                if (!user.IsActive)
                    return Unauthorized("Account is deactivated. Please contact support.");

                // Update last login
                user.LastLogin = DateTime.Now;
                db.SaveChanges();

                // Prepare base response
                var response = new
                {
                    Message = "Login successful.",
                    UserId = user.UserId,
                    UserType = user.UserType,
                    FullName = user.FullName,
                    Email = user.Email,
                    IsVerified = user.IsVerified
                };

                // Add role-specific information
                switch (user.UserType)
                {
                    case "Citizen":
                        var citizenProfile = db.CitizenProfiles
                            .Where(c => c.UserId == user.UserId)
                            .Select(c => new
                            {
                                c.CitizenId,
                                c.TotalComplaintsFiled,
                                c.ApprovedComplaintsCount,
                                c.ContributionScore,
                                c.LeaderboardRank,
                                c.BadgeLevel,
                                c.TotalUpvotesReceived
                            })
                            .FirstOrDefault();

                        return Ok(new
                        {
                            response.Message,
                            response.UserId,
                            response.UserType,
                            response.FullName,
                            response.Email,
                            response.IsVerified,
                            Profile = citizenProfile,
                            RedirectTo = "/home"
                        });

                    case "Field_Staff":
                        var fieldStaff = db.StaffProfiles
                            .Where(s => s.UserId == user.UserId)
                            .Select(s => new
                            {
                                s.StaffId,
                                s.Role,
                                s.DepartmentId,
                                DepartmentName = s.Department != null ? s.Department.DepartmentName : null,
                                s.ZoneId,
                                ZoneName = s.Zone != null ? s.Zone.ZoneName : null,
                                s.TotalAssignments,
                                s.CompletedAssignments,
                                s.PendingAssignments,
                                s.IsAvailable,
                                s.PerformanceScore
                            })
                            .FirstOrDefault();

                        return Ok(new
                        {
                            response.Message,
                            response.UserId,
                            response.UserType,
                            response.FullName,
                            response.Email,
                            response.IsVerified,
                            StaffInfo = fieldStaff,
                            RedirectTo = "/staff-dashboard"
                        });

                    case "Department_Admin":
                        var deptAdmin = db.StaffProfiles
                            .Where(s => s.UserId == user.UserId)
                            .Select(s => new
                            {
                                s.StaffId,
                                s.Role,
                                s.DepartmentId,
                                DepartmentName = s.Department != null ? s.Department.DepartmentName : null,
                                PendingApprovals = db.Complaints.Count(c =>
                                    c.DepartmentId == s.DepartmentId &&
                                    c.SubmissionStatus == SubmissionStatus.PendingApproval),
                                ActiveComplaints = db.Complaints.Count(c =>
                                    c.DepartmentId == s.DepartmentId &&
                                    c.CurrentStatus != ComplaintStatus.Resolved &&
                                    c.CurrentStatus != ComplaintStatus.Closed)
                            })
                            .FirstOrDefault();

                        return Ok(new
                        {
                            response.Message,
                            response.UserId,
                            response.UserType,
                            response.FullName,
                            response.Email,
                            response.IsVerified,
                            AdminInfo = deptAdmin,
                            RedirectTo = "/department-dashboard"
                        });

                    case "System_Admin":
                        return Ok(new
                        {
                            response.Message,
                            response.UserId,
                            response.UserType,
                            response.FullName,
                            response.Email,
                            response.IsVerified,
                            SystemStats = new
                            {
                                TotalUsers = db.Users.Count(),
                                TotalComplaints = db.Complaints.Count(),
                                TotalDepartments = db.Departments.Count(),
                                TotalZones = db.Zones.Count(),
                                PendingApprovals = db.Complaints.Count(c =>
                                    c.SubmissionStatus == SubmissionStatus.PendingApproval)
                            },
                            RedirectTo = "/admin-dashboard"
                        });

                    default:
                        return Ok(new
                        {
                            response.Message,
                            response.UserId,
                            response.UserType,
                            response.FullName,
                            response.Email,
                            response.IsVerified,
                            RedirectTo = "/dashboard"
                        });
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // STAFF LOGIN - For all staff types
        // =====================================================
        [HttpPost]
        [Route("staff/login")]
        public IHttpActionResult StaffLogin([FromBody] LoginRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.PasswordHash))
                    return BadRequest("Email and Password are required.");

                var user = db.Users
                    .AsNoTracking()
                    .FirstOrDefault(u => u.Email == request.Email);

                if (user == null)
                    return Unauthorized("Invalid email or password");

                if (user.PasswordHash != request.PasswordHash)
                    return Unauthorized("Invalid email or password");

                // Check if user is staff (not citizen)
                if (user.UserType == "Citizen")
                    return Unauthorized("This login is for staff only. Please use citizen login.");

                if (!user.IsActive)
                    return Unauthorized("Account is deactivated. Please contact support.");

                user.LastLogin = DateTime.Now;
                db.SaveChanges();

                // Get staff profile
                var staffProfile = db.StaffProfiles
                    .Where(s => s.UserId == user.UserId)
                    .Select(s => new
                    {
                        s.StaffId,
                        s.Role,
                        s.DepartmentId,
                        DepartmentName = s.Department != null ? s.Department.DepartmentName : null,
                        s.ZoneId,
                        ZoneName = s.Zone != null ? s.Zone.ZoneName : null,
                        s.IsAvailable
                    })
                    .FirstOrDefault();

                string redirectTo;
                if (user.UserType == "Field_Staff")
                {
                    redirectTo = "/staff-dashboard";
                }
                else if (user.UserType == "Department_Admin")
                {
                    redirectTo = "/department-dashboard";
                }
                else if (user.UserType == "System_Admin")
                {
                    redirectTo = "/admin-dashboard";
                }
                else
                {
                    redirectTo = "/staff-dashboard";
                }

                return Ok(new
                {
                    Message = "Staff login successful.",
                    UserId = user.UserId,
                    UserType = user.UserType,
                    FullName = user.FullName,
                    Email = user.Email,
                    StaffInfo = staffProfile,
                    RedirectTo = redirectTo
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // CITIZEN LOGIN - Specific for citizens
        // =====================================================
        [HttpPost]
        [Route("citizen/login")]
        public IHttpActionResult CitizenLogin([FromBody] LoginRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.PasswordHash))
                    return BadRequest("Email and Password are required.");

                var user = db.Users.AsNoTracking()
                    .FirstOrDefault(u => u.Email == request.Email && u.UserType == "Citizen");

                if (user == null)
                    return Unauthorized("Invalid email or password");

                if (user.PasswordHash != request.PasswordHash)
                    return Unauthorized("Invalid email or password");

                if (!user.IsActive)
                    return Unauthorized("Account is deactivated. Please contact support.");

                user.LastLogin = DateTime.Now;
                db.SaveChanges();

                var citizenProfile = db.CitizenProfiles
                    .Where(c => c.UserId == user.UserId)
                    .Select(c => new
                    {
                        c.CitizenId,
                        c.TotalComplaintsFiled,
                        c.ApprovedComplaintsCount,
                        c.ContributionScore,
                        c.LeaderboardRank,
                        c.BadgeLevel,
                        c.TotalUpvotesReceived
                    })
                    .FirstOrDefault();

                return Ok(new
                {
                    Message = "Citizen login successful.",
                    UserId = user.UserId,
                    UserType = user.UserType,
                    FullName = user.FullName,
                    Email = user.Email,
                    Profile = citizenProfile,
                    RedirectTo = "/home"
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET USER BY ID - Safe version without circular references
        // =====================================================
        [HttpGet]
        [Route("{id:guid}")]
        public IHttpActionResult GetUser(Guid id)
        {
            try
            {
                var user = db.Users.AsNoTracking()
                    .FirstOrDefault(u => u.UserId == id);

                if (user == null)
                    return NotFound();

                // Return without circular references
                var result = new
                {
                    user.UserId,
                    user.UserType,
                    user.FullName,
                    user.Email,
                    user.PhoneNumber,
                    user.ZoneId,
                    user.IsActive,
                    user.IsVerified,
                    user.CreatedAt,
                    user.CNIC,
                    user.Address,
                    user.ProfilePhotoUrl,
                    user.LastLogin
                };

                // Add citizen profile if exists
                if (user.UserType == "Citizen")
                {
                    var citizen = db.CitizenProfiles
                        .Where(c => c.UserId == id)
                        .Select(c => new
                        {
                            c.CitizenId,
                            c.TotalComplaintsFiled,
                            c.ApprovedComplaintsCount,
                            c.ContributionScore,
                            c.LeaderboardRank,
                            c.BadgeLevel,
                            c.TotalUpvotesReceived
                        })
                        .FirstOrDefault();

                    if (citizen != null)
                    {
                        return Ok(new
                        {
                            User = result,
                            CitizenProfile = citizen
                        });
                    }
                }

                // Add staff profile if exists
                if (user.UserType != "Citizen")
                {
                    var staff = db.StaffProfiles
                        .Where(s => s.UserId == id)
                        .Select(s => new
                        {
                            s.StaffId,
                            s.Role,
                            s.DepartmentId,
                            s.ZoneId,
                            s.TotalAssignments,
                            s.CompletedAssignments,
                            s.PendingAssignments,
                            s.IsAvailable,
                            s.PerformanceScore
                        })
                        .FirstOrDefault();

                    if (staff != null)
                    {
                        return Ok(new
                        {
                            User = result,
                            StaffProfile = staff
                        });
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // UPDATE USER - Citizens can update their own profile
        // =====================================================
        [HttpPut]
        [Route("{id:guid}")]
        public IHttpActionResult UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
        {
            try
            {
                if (request == null)
                    return BadRequest("User data is required.");

                var user = db.Users.AsNoTracking().FirstOrDefault(u => u.UserId == id);
                if (user == null)
                    return NotFound();

                // Only allow updating certain fields
                if (!string.IsNullOrEmpty(request.FullName))
                    user.FullName = request.FullName;

                if (!string.IsNullOrEmpty(request.PhoneNumber))
                    user.PhoneNumber = request.PhoneNumber;

                if (!string.IsNullOrEmpty(request.Address))
                    user.Address = request.Address;

                if (!string.IsNullOrEmpty(request.ProfilePhotoUrl))
                    user.ProfilePhotoUrl = request.ProfilePhotoUrl;

                if (request.ZoneId.HasValue)
                    user.ZoneId = request.ZoneId;

                user.UpdatedAt = DateTime.Now;

                db.SaveChanges();

                return Ok(new
                {
                    Message = "User updated successfully.",
                    UserId = user.UserId
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // CHANGE PASSWORD
        // =====================================================
        [HttpPost]
        [Route("{id:guid}/change-password")]
        public IHttpActionResult ChangePassword(Guid id, [FromBody] ChangePasswordRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.CurrentPassword) || string.IsNullOrEmpty(request.NewPassword))
                    return BadRequest("Current password and new password are required.");

                var user = db.Users.FirstOrDefault(u => u.UserId == id);
                if (user == null)
                    return NotFound();

                // Verify current password
                if (user.PasswordHash != request.CurrentPassword)
                    return BadRequest("Current password is incorrect.");

                // Update password
                user.PasswordHash = request.NewPassword;
                user.UpdatedAt = DateTime.Now;

                db.SaveChanges();

                return Ok(new { Message = "Password changed successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // GET ALL USERS - Admin only with pagination
        // =====================================================
        [HttpGet]
        [Route("all")]
        public IHttpActionResult GetAllUsers(int page = 1, int pageSize = 30, string userType = null)
        {
            try
            {
                var query = db.Users.AsQueryable();

                if (!string.IsNullOrEmpty(userType))
                    query = query.Where(u => u.UserType == userType);

                var totalCount = query.Count();

                var users = query
                    .OrderBy(u => u.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new
                    {
                        u.UserId,
                        u.UserType,
                        u.FullName,
                        u.Email,
                        u.PhoneNumber,
                        u.IsActive,
                        u.IsVerified,
                        u.CreatedAt,
                        u.LastLogin
                    })
                    .ToList();

                return Ok(new
                {
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize),
                    Users = users
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // DEACTIVATE USER - Admin only
        // =====================================================
        [HttpPost]
        [Route("{id:guid}/deactivate")]
        public IHttpActionResult DeactivateUser(Guid id)
        {
            try
            {
                var user = db.Users.FirstOrDefault(u => u.UserId == id);
                if (user == null)
                    return NotFound();

                user.IsActive = false;
                user.UpdatedAt = DateTime.Now;

                db.SaveChanges();

                return Ok(new { Message = "User deactivated successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // =====================================================
        // ACTIVATE USER - Admin only
        // =====================================================
        [HttpPost]
        [Route("{id:guid}/activate")]
        public IHttpActionResult ActivateUser(Guid id)
        {
            try
            {
                var user = db.Users.FirstOrDefault(u => u.UserId == id);
                if (user == null)
                    return NotFound();

                user.IsActive = true;
                user.UpdatedAt = DateTime.Now;

                db.SaveChanges();

                return Ok(new { Message = "User activated successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        private IHttpActionResult Unauthorized(string message)
        {
            return Content(HttpStatusCode.Unauthorized, new { error = message });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    // =====================================================
    // DTOs for requests
    // =====================================================

    public class UpdateUserRequest
    {
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; }
        public string ProfilePhotoUrl { get; set; }
        public Guid? ZoneId { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
    }
}