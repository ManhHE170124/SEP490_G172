/*
  File: PermissionsController.cs
  Author: HieuNDHE173169
  Created: 17/10/2025
  Last Updated: 20/10/2025
  Version: 1.0.0
  Purpose: Manage permissions (CRUD). Ensures unique permission names and
           cascades deletion to related role-permissions.
  Endpoints:
    - GET    /api/permissions              : List permissions
    - GET    /api/permissions/{id}         : Get permission by id
    - POST   /api/permissions              : Create permission
    - PUT    /api/permissions/{id}         : Update permission
    - DELETE /api/permissions/{id}         : Delete permission and role-permissions
*/
using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PermissionsController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;
        public PermissionsController(KeytietkiemDbContext context)
        {
            _context = context;
        }

        // GET: api/<PermissionsController>
        [HttpGet]
        /**
         * Summary: Retrieve all permissions.
         * Route: GET /api/permissions
         * Params: none
         * Returns: 200 OK with list of permissions
         */
        public async Task<IActionResult> GetPermissions()
        {
            var permissions = await _context.Permissions.ToListAsync();
            return Ok(permissions);
        }

        // GET api/<PermissionsController>/5
        [HttpGet("{id}")]
        /**
         * Summary: Retrieve a permission by id.
         * Route: GET /api/permissions/{id}
         * Params: id (long) - permission identifier
         * Returns: 200 OK with permission, 404 if not found
         */
        public async Task<IActionResult> GetPermissionById(long id)
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
        /**
         * Summary: Create a new permission.
         * Route: POST /api/permissions
         * Body: Permission newPermission
         * Returns: 201 Created with created permission, 400/409 on validation errors
         */
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
        /**
         * Summary: Update an existing permission by id.
         * Route: PUT /api/permissions/{id}
         * Params: id (long)
         * Body: Permission updatedPermission
         * Returns: 204 No Content, 400/404 on errors
         */
        public async Task<IActionResult> UpdatePermission(long id, [FromBody] Permission updatedPermission)
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
        /**
        * Summary: Delete a permission by id and cascade remove related role-permissions.
        * Route: DELETE /api/permissions/{id}
        * Params: id (long)
        * Returns: 204 No Content, 404 if not found
        */
        public async Task<IActionResult> DeletePermission(long id)
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
