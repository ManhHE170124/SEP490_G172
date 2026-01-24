/**
 * File: PermissionsController.cs
 * Author: HieuNDHE173169
 * Created: 17/10/2025
 * Last Updated: 28/10/2025
 * Version: 1.0.0
 * Purpose: Manage permissions (CRUD). Ensures unique permission names and
 *          cascades deletion to related role-permissions.
 * Endpoints:
 *   - GET    /api/permissions              : List permissions
 *   - GET    /api/permissions/{id}         : Get permission by id
 *   - POST   /api/permissions              : Create permission
 *   - PUT    /api/permissions/{id}         : Update permission
 *   - DELETE /api/permissions/{id}         : Delete permission and role-permissions
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Keytietkiem.Models;
using Keytietkiem.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Keytietkiem.DTOs.Roles;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Http;
using Keytietkiem.Utils.Constants;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class PermissionsController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;
        private readonly IAuditLogger _auditLogger;

        public PermissionsController(
            KeytietkiemDbContext context,
            IAuditLogger auditLogger)
        {
            _context = context;
            _auditLogger = auditLogger;
        }

        /**
         * Summary: Retrieve all permissions.
         * Route: GET /api/permissions
         * Params: none
         * Returns: 200 OK with list of permissions
         */
        [HttpGet]
        [RequireRole(RoleCodes.ADMIN)]
        public async Task<IActionResult> GetPermissions()
        {
            var permissions = await _context.Permissions
                .Select(p => new PermissionDTO
                {
                    PermissionId = p.PermissionId,
                    PermissionName = p.PermissionName,
                    Code = p.Code,
                    Description = p.Description,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .ToListAsync();
            return Ok(permissions);
        }

        /**
         * Summary: Retrieve a permission by id.
         * Route: GET /api/permissions/{id}
         * Params: id (long) - permission identifier
         * Returns: 200 OK with permission, 404 if not found
         */
        [HttpGet("{id}")]
        [RequireRole(RoleCodes.ADMIN)]
        public async Task<IActionResult> GetPermissionById(long id)
        {
            var permission = await _context.Permissions
                .FirstOrDefaultAsync(m => m.PermissionId == id);
            if (permission == null)
            {
                return NotFound();
            }

            var permissionDto = new PermissionDTO
            {
                PermissionId = permission.PermissionId,
                PermissionName = permission.PermissionName,
                Code = permission.Code,
                Description = permission.Description,
                CreatedAt = permission.CreatedAt,
                UpdatedAt = permission.UpdatedAt
            };

            return Ok(permissionDto);
        }

        /**
         * Summary: Create a new permission.
         * Route: POST /api/permissions
         * Body: CreatePermissionDTO createPermissionDto
         * Returns: 201 Created with created permission, 400/409 on validation errors
         */
        [HttpPost]
        [RequireRole(RoleCodes.ADMIN)]
        public async Task<IActionResult> CreatePermission([FromBody] CreatePermissionDTO createPermissionDto)
        {
            if (createPermissionDto == null)
            {
                return BadRequest("Dữ liệu không hợp lệ.");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(new { message = string.Join(" ", errors) });
            }
            var existing = await _context.Permissions
                .FirstOrDefaultAsync(m => m.PermissionName == createPermissionDto.PermissionName);
            if (existing != null)
            {
                return Conflict(new { message = "Tên quyền đã tồn tại." });
            }

            // Check if Code is unique (if provided)
            if (!string.IsNullOrWhiteSpace(createPermissionDto.Code))
            {
                var existingCode = await _context.Permissions
                    .FirstOrDefaultAsync(m => m.Code == createPermissionDto.Code);
                if (existingCode != null)
                {
                    return Conflict(new { message = "Mã quyền đã tồn tại." });
                }
            }

            var newPermission = new Permission
            {
                PermissionName = createPermissionDto.PermissionName,
                Code = createPermissionDto.Code,
                Description = createPermissionDto.Description,
                CreatedAt = DateTime.Now
            };

            _context.Permissions.Add(newPermission);
            await _context.SaveChangesAsync();

            // Add RolePermissions for all existing roles and modules with this new permission
            var roles = await _context.Roles.ToListAsync();
            var modules = await _context.Modules.ToListAsync();

            var rolePermissions = new List<RolePermission>();
            foreach (var role in roles)
            {
                foreach (var module in modules)
                {
                    rolePermissions.Add(new RolePermission
                    {
                        RoleId = role.RoleId,
                        ModuleId = module.ModuleId,
                        PermissionId = newPermission.PermissionId,
                        IsActive = true
                    });
                }
            }

            _context.RolePermissions.AddRange(rolePermissions);
            await _context.SaveChangesAsync();

            var permissionDto = new PermissionDTO
            {
                PermissionId = newPermission.PermissionId,
                PermissionName = newPermission.PermissionName,
                Code = newPermission.Code,
                Description = newPermission.Description,
                CreatedAt = newPermission.CreatedAt,
                UpdatedAt = newPermission.UpdatedAt
            };

            await _auditLogger.LogAsync(
                HttpContext,
                action: "CreatePermission",
                entityType: "Permission",
                entityId: newPermission.PermissionId.ToString(),
                before: null,
                after: new
                {
                    newPermission.PermissionId,
                    newPermission.PermissionName,
                    newPermission.Code,
                    newPermission.Description,
                    newPermission.CreatedAt,
                    newPermission.UpdatedAt,
                    RolesCount = roles.Count,
                    ModulesCount = modules.Count
                });

            return CreatedAtAction(nameof(GetPermissionById), new { id = newPermission.PermissionId }, permissionDto);
        }

        /**
         * Summary: Update an existing permission by id.
         * Route: PUT /api/permissions/{id}
         * Params: id (long)
         * Body: UpdatePermissionDTO updatePermissionDto
         * Returns: 204 No Content, 400/404 on errors
         */
        [HttpPut("{id}")]
        [RequireRole(RoleCodes.ADMIN)]
        public async Task<IActionResult> UpdatePermission(long id, [FromBody] UpdatePermissionDTO updatePermissionDto)
        {
            if (updatePermissionDto == null)
            {
                return BadRequest("Dữ liệu không hợp lệ.");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(new { message = string.Join(" ", errors) });
            }
            var existing = await _context.Permissions
                .FirstOrDefaultAsync(m => m.PermissionId == id);
            if (existing == null)
            {
                // Không audit lỗi để tránh spam log
                return NotFound();
            }

            // Check if Code is unique (if provided and changed)
            if (!string.IsNullOrWhiteSpace(updatePermissionDto.Code) && existing.Code != updatePermissionDto.Code)
            {
                var existingCode = await _context.Permissions
                    .FirstOrDefaultAsync(m => m.Code == updatePermissionDto.Code && m.PermissionId != id);
                if (existingCode != null)
                {
                    return Conflict(new { message = "Mã quyền đã tồn tại." });
                }
            }

            var before = new
            {
                existing.PermissionId,
                existing.PermissionName,
                existing.Code,
                existing.Description,
                existing.CreatedAt,
                existing.UpdatedAt
            };

            existing.PermissionName = updatePermissionDto.PermissionName;
            existing.Code = updatePermissionDto.Code;
            existing.Description = updatePermissionDto.Description;
            existing.UpdatedAt = DateTime.Now;
            _context.Permissions.Update(existing);
            await _context.SaveChangesAsync();

            await _auditLogger.LogAsync(
                HttpContext,
                action: "UpdatePermission",
                entityType: "Permission",
                entityId: existing.PermissionId.ToString(),
                before: before,
                after: new
                {
                    existing.PermissionId,
                    existing.PermissionName,
                    existing.Code,
                    existing.Description,
                    existing.CreatedAt,
                    existing.UpdatedAt
                });

            return NoContent();
        }

        /**
        * Summary: Delete a permission by id and cascade remove related role-permissions.
        * Route: DELETE /api/permissions/{id}
        * Params: id (long)
        * Returns: 204 No Content, 404 if not found
        */
        [HttpDelete("{id}")]
        [RequireRole(RoleCodes.ADMIN)]
        public async Task<IActionResult> DeletePermission(long id)
        {
            var existingPermission = await _context.Permissions
                .Include(p => p.RolePermissions)
                .FirstOrDefaultAsync(m => m.PermissionId == id);
            if (existingPermission == null)
            {
                // Không audit lỗi để tránh spam log
                return NotFound();
            }

            var before = new
            {
                existingPermission.PermissionId,
                existingPermission.PermissionName,
                existingPermission.Code,
                existingPermission.Description,
                existingPermission.CreatedAt,
                existingPermission.UpdatedAt,
                RolePermissionsCount = existingPermission.RolePermissions?.Count ?? 0
            };

            _context.RolePermissions.RemoveRange(existingPermission.RolePermissions);
            _context.Permissions.Remove(existingPermission);
            await _context.SaveChangesAsync();

            await _auditLogger.LogAsync(
                HttpContext,
                action: "DeletePermission",
                entityType: "Permission",
                entityId: existingPermission.PermissionId.ToString(),
                before: before,
                after: new
                {
                    existingPermission.PermissionId,
                    Deleted = true
                });

            return NoContent();
        }
    }
}
