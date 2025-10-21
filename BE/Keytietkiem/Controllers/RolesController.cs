/*
  File: RolesController.cs
  Author: HieuNDHE173169
  Created: 17-10-2025
  Last Updated: 20-10-2025
  Version: 1.0.0
  Purpose: Manage roles (CRUD). Initializes role-permissions for all modules &
           permissions on role creation and maintains referential integrity on
           updates/deletions.
  Endpoints:
    - GET    /api/roles              : List roles
    - GET    /api/roles/{id}         : Get role by id (includes role-permissions)
    - POST   /api/roles              : Create role and seed role-permissions
    - PUT    /api/roles/{id}         : Update role
    - DELETE /api/roles/{id}         : Delete role and its role-permissions
*/

using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RolesController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;
        public RolesController(KeytietkiemDbContext context)
        {
            _context = context;
        }
        // GET: api/<RolesController>
        [HttpGet]
        /**
         * Summary: Retrieve all roles.
         * Route: GET /api/roles
         * Params: none
         * Returns: 200 OK with list of roles
         */
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _context.Roles.ToListAsync();
            return Ok(roles);
        }

        // GET api/<RolesController>/5
        [HttpGet("{id}")]
        /**
         * Summary: Retrieve a role by id including role-permissions.
         * Route: GET /api/roles/{id}
         * Params: id (string) - role identifier
         * Returns: 200 OK with role, 404 if not found
         */
        public async Task<IActionResult> GetRoleById(string id)
        {
            var role = await _context.Roles
                .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Module)
                .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(r => r.RoleId == id);
            if (role == null)
            {
                return NotFound();
            }
            return Ok(role);

        }
        // POST api/<RolesController>
        [HttpPost]
        /**
         * Summary: Create a new role and seed role-permissions for all modules & permissions.
         * Route: POST /api/roles
         * Body: Role newRole
         * Returns: 201 Created with created role, 400/409 on validation errors
         */
        public async Task<IActionResult> CreateRole([FromBody] Role newRole)
        {
            if (newRole == null || string.IsNullOrWhiteSpace(newRole.Name))
            {
                return BadRequest("Role name is required.");
            }
            var existingRole = await _context.Roles
                .FirstOrDefaultAsync(m => m.Name == newRole.Name);
            if (existingRole != null)
            {
                return Conflict(new { message = "Role name already exists." });
            }

            newRole.CreatedAt = DateTime.UtcNow;
            newRole.IsActive = true;

            _context.Roles.Add(newRole);
            await _context.SaveChangesAsync();

            var modules = await _context.Modules.ToListAsync();
            var permissions = await _context.Permissions.ToListAsync();

            var rolePermissions = new List<RolePermission>();

            foreach (var module in modules)
            {
                foreach (var permission in permissions)
                {
                    rolePermissions.Add(new RolePermission
                    {
                        RoleId = newRole.RoleId,
                        ModuleId = module.ModuleId,
                        PermissionId = permission.PermissionId,
                        IsActive = true
                    });
                }
            }

            _context.RolePermissions.AddRange(rolePermissions);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetRoleById), new { id = newRole.RoleId }, newRole);
        }
        // PUT api/<RolesController>/5
        [HttpPut("{id}")]
        /**
        * Summary: Update an existing role by id.
        * Route: PUT /api/roles/{id}
        * Params: id (string)
        * Body: Role updatedRole
        * Returns: 204 No Content, 400/404 on errors
        */
        public async Task<IActionResult> UpdateRole(string id, [FromBody] Role updatedRole)
        {
            if (updatedRole == null || id != updatedRole.RoleId)
            {
                return BadRequest("Invalid role data.");
            }
            var existingRole = await _context.Roles.FindAsync(id);
            if (existingRole == null)
            {
                return NotFound();
            }
            existingRole.Name = updatedRole.Name;
            existingRole.IsActive = updatedRole.IsActive;
            existingRole.UpdatedAt = DateTime.UtcNow;
            _context.Roles.Update(existingRole);
            await _context.SaveChangesAsync();
            return NoContent();
        }
        // DELETE api/<RolesController>/5
        [HttpDelete("{id}")]
        /**
         * Summary: Delete a role by id and cascade remove related role-permissions.
         * Route: DELETE /api/roles/{id}
         * Params: id (string)
         * Returns: 204 No Content, 404 if not found
         */
        public async Task<IActionResult> DeleteRoleById(string id)
        {
            var existingRole = await _context.Roles.FindAsync(id);
            if (existingRole == null)
            {
                return NotFound();
            }
            var rolePermissions = _context.RolePermissions.Where(rp => rp.RoleId == id);
            _context.RolePermissions.RemoveRange(rolePermissions);
            _context.Roles.Remove(existingRole);
            await _context.SaveChangesAsync();
            return NoContent();
        }


    }
}
