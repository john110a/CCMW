// Controllers/ComplaintCategoryController.cs
using CCMW.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Http;

namespace CCMW.Controllers
{
    [RoutePrefix("api/complaint-categories")]
    public class ComplaintCategoryController : ApiController
    {
        private CCMWDbContext db = new CCMWDbContext();

        // GET: api/complaint-categories
        [HttpGet]
        [Route("")]
        public IHttpActionResult GetAllCategories()
        {
            try
            {
                var categories = db.ComplaintCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.PriorityWeight)
                    .Select(c => new
                    {
                        c.CategoryId,
                        c.CategoryName,
                        c.CategoryCode,
                        c.Description,
                        c.IconName,
                        c.ColorCode,
                        c.PriorityWeight,
                        c.DepartmentId
                    })
                    .ToList();

                return Ok(categories);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET: api/complaint-categories/{id}
        [HttpGet]
        [Route("{id:guid}")]
        public IHttpActionResult GetCategoryById(Guid id)
        {
            try
            {
                var category = db.ComplaintCategories
                    .Where(c => c.CategoryId == id)
                    .Select(c => new
                    {
                        c.CategoryId,
                        c.CategoryName,
                        c.CategoryCode,
                        c.Description,
                        c.IconName,
                        c.ColorCode,
                        c.PriorityWeight,
                        c.ExpectedResolutionTimeHours,
                        c.DepartmentId,
                        c.IsActive
                    })
                    .FirstOrDefault();

                if (category == null)
                    return NotFound();

                return Ok(category);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET: api/complaint-categories/department/{departmentId}
        [HttpGet]
        [Route("department/{departmentId:guid}")]
        public IHttpActionResult GetCategoriesByDepartment(Guid departmentId)
        {
            try
            {
                var categories = db.ComplaintCategories
                    .Where(c => c.DepartmentId == departmentId && c.IsActive)
                    .OrderBy(c => c.PriorityWeight)
                    .Select(c => new
                    {
                        c.CategoryId,
                        c.CategoryName,
                        c.CategoryCode,
                        c.Description,
                        c.IconName,
                        c.ColorCode,
                        c.PriorityWeight
                    })
                    .ToList();

                return Ok(categories);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST: api/complaint-categories (Admin only)
        // FIXED: POST api/complaint-categories
        [HttpPost]
        [Route("")]
        public IHttpActionResult CreateCategory([FromBody] ComplaintCategory category)
        {
            try
            {
                if (category == null)
                    return BadRequest("Category data is required.");

                if (string.IsNullOrEmpty(category.CategoryName))
                    return BadRequest("Category name is required.");

                if (category.DepartmentId == null || category.DepartmentId == Guid.Empty)
                    return BadRequest("Department ID is required.");

                // Check if category code already exists
                if (!string.IsNullOrEmpty(category.CategoryCode) &&
                    db.ComplaintCategories.Any(c => c.CategoryCode == category.CategoryCode))
                {
                    return BadRequest("Category code already exists.");
                }

                category.CategoryId = Guid.NewGuid();
                category.CreatedAt = DateTime.Now;
                category.IsActive = true;

                // Set default values if not provided
                if (string.IsNullOrEmpty(category.IconName))
                    category.IconName = "report_problem";

                if (string.IsNullOrEmpty(category.ColorCode))
                    category.ColorCode = "#2196F3";

                if (category.PriorityWeight == 0)
                    category.PriorityWeight = 1;

                db.ComplaintCategories.Add(category);
                db.SaveChanges();

                return Ok(new
                {
                    Message = "Category created successfully",
                    CategoryId = category.CategoryId,
                    CategoryName = category.CategoryName
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // PUT: api/complaint-categories/{id} (Admin only)
        [HttpPut]
        [Route("{id:guid}")]
        public IHttpActionResult UpdateCategory(Guid id, [FromBody] ComplaintCategory updatedCategory)
        {
            try
            {
                var category = db.ComplaintCategories.Find(id);
                if (category == null)
                    return NotFound();

                category.CategoryName = updatedCategory.CategoryName ?? category.CategoryName;
                category.Description = updatedCategory.Description ?? category.Description;
                category.IconName = updatedCategory.IconName ?? category.IconName;
                category.ColorCode = updatedCategory.ColorCode ?? category.ColorCode;
                category.PriorityWeight = updatedCategory.PriorityWeight;
                category.ExpectedResolutionTimeHours = updatedCategory.ExpectedResolutionTimeHours;
                category.DepartmentId = updatedCategory.DepartmentId;
                category.IsActive = updatedCategory.IsActive;

                db.SaveChanges();

                return Ok(new { Message = "Category updated successfully" });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // DELETE: api/complaint-categories/{id} (Admin only)
        [HttpDelete]
        [Route("{id:guid}")]
        public IHttpActionResult DeleteCategory(Guid id)
        {
            try
            {
                var category = db.ComplaintCategories.Find(id);
                if (category == null)
                    return NotFound();

                // Soft delete
                category.IsActive = false;
                db.SaveChanges();

                return Ok(new { Message = "Category deactivated successfully" });
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
}