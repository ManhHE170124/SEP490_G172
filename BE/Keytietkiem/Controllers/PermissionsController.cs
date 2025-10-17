using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Keytietkiem.Controllers
{
        [Route("api/[controller]")]
        [ApiController]
        public class PermissionsController : ControllerBase
        {
            private readonly KeytietkiemContext _context;
            public PermissionsController(KeytietkiemContext context)
            {
                _context = context;
            }

            // GET: api/<PermissionsController>
            [HttpGet]
            public async Task<IActionResult> GetPermissions()
            {
                var permissions = await _context.Permissions.ToListAsync();
                return Ok(permissions);
            }

            // GET api/<PermissionsController>/5
            [HttpGet("{id}")]
            public async Task<IActionResult> GetPermissionById(Guid id)
            {
                var permission = await _context.Permissions
                    .FirstOrDefaultAsync(m => m.PermissionId == id);
                if (permission == null)
                {
                    return NotFound();
                }
                return Ok(permission);
            }

            // POST api/<PermissionsController>
            [HttpPost]
            public async Task<IActionResult> CreatePermission([FromBody] Permission newPermission)
            {
                if (newPermission == null || string.IsNullOrWhiteSpace(newPermission.PermissionName))
                {
                    return BadRequest("Permission name is required.");
                }
                var existing = await _context.Permissions
                    .FirstOrDefaultAsync(m => m.PermissionName == newPermission.PermissionName);
                if (existing != null)
                {
                    return Conflict(new { message = "Permission name already exists." });
                }
                newPermission.CreatedAt = DateTime.Now;
                _context.Permissions.Add(newPermission);
                await _context.SaveChangesAsync();
                return CreatedAtAction(nameof(GetPermissionById), new { id = newPermission.PermissionId }, newPermission);
            }

            // PUT api/<PermissionsController>/5
            [HttpPut("{id}")]
            public async Task<IActionResult> UpdatePermission(Guid id, [FromBody] Permission updatedPermission)
            {
                if (updatedPermission == null || id != updatedPermission.PermissionId)
                {
                    return BadRequest("Invalid permission data.");
                }
                var existing = await _context.Permissions
                    .FirstOrDefaultAsync(m => m.PermissionId == id);
                if (existing == null)
                {
                    return NotFound();
                }
                existing.PermissionName = updatedPermission.PermissionName;
                existing.Description = updatedPermission.Description;
                existing.UpdatedAt = DateTime.Now;
                _context.Permissions.Update(existing);
                await _context.SaveChangesAsync();
                return NoContent();
            }

            // DELETE api/<PermissionsController>/5
            [HttpDelete("{id}")]
            public async Task<IActionResult> DeletePermission(Guid id)
            {
                var existingPermission = await _context.Permissions
                    .FirstOrDefaultAsync(m => m.PermissionId == id);
                if (existingPermission == null)
                {
                    return NotFound();
                }
            _context.RolePermissions.RemoveRange(existingPermission.RolePermissions);
            _context.Permissions.Remove(existingPermission);
                await _context.SaveChangesAsync();
                return NoContent();
            }
        }
}
