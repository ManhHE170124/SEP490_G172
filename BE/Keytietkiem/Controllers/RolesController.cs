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
    - GET    /api/roles/{id}/permissions : Get role permissions matrix
    - POST   /api/roles              : Create role and seed role-permissions
    - PUT    /api/roles/{id}         : Update role
    - PUT    /api/roles/{id}/permissions : Bulk update role permissions
    - DELETE /api/roles/{id}         : Delete role and its role-permissions
*/

using Keytietkiem.Models;
using Keytietkiem.DTOs;
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
        [HttpGet]
        /**
         * Summary: Retrieve all roles.
         * Route: GET /api/roles
         * Params: none
         * Returns: 200 OK with list of roles
         */
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _context.Roles
                .Select(r => new RoleDTO
                {
                    RoleId = r.RoleId,
                    Name = r.Name,
                    IsSystem = r.IsSystem,
                    IsActive = r.IsActive,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                })
                .ToListAsync();
            return Ok(roles);
        }

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

            var roleResponse = new RoleResponseDTO
            {
                RoleId = role.RoleId,
                Name = role.Name,
                IsSystem = role.IsSystem,
                IsActive = role.IsActive,
                CreatedAt = role.CreatedAt,
                UpdatedAt = role.UpdatedAt,
                RolePermissions = role.RolePermissions.Select(rp => new RolePermissionDTO
                {
                    RoleId = rp.RoleId,
                    ModuleId = rp.ModuleId,
                    PermissionId = rp.PermissionId,
                    IsActive = rp.IsActive,
                    ModuleName = rp.Module?.ModuleName,
                    PermissionName = rp.Permission?.PermissionName
                }).ToList()
            };

            return Ok(roleResponse);
        }
        [HttpPost]
        /**
         * Summary: Create a new role and seed role-permissions for all modules & permissions.
         * Route: POST /api/roles
         * Body: Role newRole
         * Returns: 201 Created with created role, 400/409 on validation errors
         */
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleDTO createRoleDto)
        {
            if (createRoleDto == null || string.IsNullOrWhiteSpace(createRoleDto.Name))
            {
                return BadRequest("Role name is required.");
            }
            var existingRole = await _context.Roles
                .FirstOrDefaultAsync(m => m.Name == createRoleDto.Name);
            if (existingRole != null)
            {
                return Conflict(new { message = "Role name already exists." });
            }

            var newRole = new Role
            {
                RoleId = Guid.NewGuid().ToString(),
                Name = createRoleDto.Name,
                IsSystem = createRoleDto.IsSystem,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

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

            var roleDto = new RoleDTO
            {
                RoleId = newRole.RoleId,
                Name = newRole.Name,
                IsSystem = newRole.IsSystem,
                IsActive = newRole.IsActive,
                CreatedAt = newRole.CreatedAt,
                UpdatedAt = newRole.UpdatedAt
            };

            return CreatedAtAction(nameof(GetRoleById), new { id = newRole.RoleId }, roleDto);
        }
        [HttpPut("{id}")]
        /**
        * Summary: Update an existing role by id.
        * Route: PUT /api/roles/{id}
        * Params: id (string)
        * Body: Role updatedRole
        * Returns: 204 No Content, 400/404 on errors
        */
        public async Task<IActionResult> UpdateRole(string id, [FromBody] UpdateRoleDTO updateRoleDto)
        {
            if (updateRoleDto == null)
            {
                return BadRequest("Invalid role data.");
            }
            var existingRole = await _context.Roles.FindAsync(id);
            if (existingRole == null)
            {
                return NotFound();
            }
            existingRole.Name = updateRoleDto.Name;
            existingRole.IsActive = updateRoleDto.IsActive;
            existingRole.UpdatedAt = DateTime.UtcNow;
            _context.Roles.Update(existingRole);
            await _context.SaveChangesAsync();
            return NoContent();
        }
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

        [HttpGet("{id}/permissions")]
        /**
         * Summary: Get role permissions matrix for a specific role.
         * Route: GET /api/roles/{id}/permissions
         * Params: id (string) - role identifier
         * Returns: 200 OK with role permissions matrix, 404 if role not found
         */
        public async Task<IActionResult> GetRolePermissions(string id)
        {
            var role = await _context.Roles
                .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Module)
                .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(r => r.RoleId == id);

            if (role == null)
            {
                return NotFound(new { message = "Role not found." });
            }

            var rolePermissions = role.RolePermissions.Select(rp => new RolePermissionDTO
            {
                RoleId = rp.RoleId,
                ModuleId = rp.ModuleId,
                PermissionId = rp.PermissionId,
                IsActive = rp.IsActive,
                ModuleName = rp.Module?.ModuleName,
                PermissionName = rp.Permission?.PermissionName
            }).ToList();

            var response = new RolePermissionResponseDTO
            {
                RoleId = role.RoleId,
                RoleName = role.Name,
                RolePermissions = rolePermissions
            };

            return Ok(response);
        }

        [HttpPut("{id}/permissions")]
        /**
         * Summary: Bulk update role permissions for a specific role.
         * Route: PUT /api/roles/{id}/permissions
         * Params: id (string) - role identifier
         * Body: BulkRolePermissionUpdateDTO - list of role permissions to update
         * Returns: 200 OK with updated role permissions, 400/404 on errors
         */
        public async Task<IActionResult> UpdateRolePermissions(string id, [FromBody] BulkRolePermissionUpdateDTO updateDto)
        {
            if (updateDto == null || updateDto.RolePermissions == null || !updateDto.RolePermissions.Any())
            {
                return BadRequest(new { message = "Role permissions data is required." });
            }

            if (id != updateDto.RoleId)
            {
                return BadRequest(new { message = "Role ID mismatch." });
            }

            var role = await _context.Roles.FindAsync(id);
            if (role == null)
            {
                return NotFound(new { message = "Role not found." });
            }

            try
            {
                // Get existing role permissions
                var existingRolePermissions = await _context.RolePermissions
                    .Where(rp => rp.RoleId == id)
                    .ToListAsync();

                // Update or create role permissions
                foreach (var permissionDto in updateDto.RolePermissions)
                {
                    var existingPermission = existingRolePermissions
                        .FirstOrDefault(rp => rp.ModuleId == permissionDto.ModuleId &&
                                            rp.PermissionId == permissionDto.PermissionId);

                    if (existingPermission != null)
                    {
                        // Update existing permission
                        existingPermission.IsActive = permissionDto.IsActive;
                        _context.RolePermissions.Update(existingPermission);
                    }
                    else
                    {
                        // Create new permission
                        var newRolePermission = new RolePermission
                        {
                            RoleId = permissionDto.RoleId,
                            ModuleId = permissionDto.ModuleId,
                            PermissionId = permissionDto.PermissionId,
                            IsActive = permissionDto.IsActive
                        };
                        _context.RolePermissions.Add(newRolePermission);
                    }
                }

                await _context.SaveChangesAsync();

                // Return updated role permissions
                var updatedRolePermissions = await _context.RolePermissions
                    .Include(rp => rp.Module)
                    .Include(rp => rp.Permission)
                    .Where(rp => rp.RoleId == id)
                    .Select(rp => new RolePermissionDTO
                    {
                        RoleId = rp.RoleId,
                        ModuleId = rp.ModuleId,
                        PermissionId = rp.PermissionId,
                        IsActive = rp.IsActive,
                        ModuleName = rp.Module.ModuleName,
                        PermissionName = rp.Permission.PermissionName
                    })
                    .ToListAsync();

                var response = new RolePermissionResponseDTO
                {
                    RoleId = role.RoleId,
                    RoleName = role.Name,
                    RolePermissions = updatedRolePermissions
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while updating role permissions.", error = ex.Message });
            }
        }

    }
}
