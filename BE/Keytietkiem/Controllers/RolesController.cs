/**
 * File: RolesController.cs
 * Author: HieuNDHE173169
 * Created: 17-10-2025
 * Last Updated: 20-10-2025
 * Version: 1.0.0
 * Purpose: Manage roles (CRUD). Initializes role-permissions for all modules & 
 *          permissions on role creation and maintains referential integrity on
 *          updates/deletions.
 * Endpoints:
 *   - GET    /api/roles                   : List roles
 *   - GET    /api/roles/list              : List roles (full info)
 *   - GET    /api/roles/{id}              : Get role by id (includes role-permissions)
 *   - GET    /api/roles/{id}/permissions  : Get role permissions matrix
 *   - POST   /api/roles                   : Create role and seed role-permissions
 *   - PUT    /api/roles/{id}              : Update role
 *   - PUT    /api/roles/{id}/permissions  : Bulk update role permissions
 *   - DELETE /api/roles/{id}              : Delete role and its role-permissions
 *   - GET    /api/roles/active            : List active roles
 *   - POST   /api/roles/module-access     : List modules accessible by role codes
 *   - POST   /api/roles/check-permission  : Check permission for single role
 *   - POST   /api/roles/check-permissions : Check permissions for multiple roles
 */

using Keytietkiem.DTOs.Roles;
using Keytietkiem.Infrastructure;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Keytietkiem.Attributes;
using Keytietkiem.Constants;
using static Keytietkiem.Constants.ModuleCodes;
using static Keytietkiem.Constants.PermissionCodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Keytietkiem.DTOs.Roles;
using System.Linq;

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RolesController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;
        private readonly IAuditLogger _auditLogger;

        public RolesController(KeytietkiemDbContext context, IAuditLogger auditLogger)
        {
            _context = context;
            _auditLogger = auditLogger;
        }

        /// <summary>
        /// Get a list of roles excluding any role whose name contains "admin" (case-insensitive).
        /// </summary>
        [HttpGet]
        [RequirePermission(ModuleCodes.ROLE, PermissionCodes.VIEW_LIST)]
        public async Task<IActionResult> Get()
        {
            var roles = await _context.Roles
                .AsNoTracking()
                .Where(r => !EF.Functions.Like(r.Code.ToLower(), "%admin%"))
                .Select(r => new { r.RoleId, r.Name })
                .ToListAsync();

            return Ok(roles);
        }

        /**
         * Summary: Retrieve all roles.
         * Route: GET /api/roles/list
         * Params: none
         * Returns: 200 OK with list of roles
         */
        [HttpGet("list")]
        [RequirePermission(ModuleCodes.ROLE, PermissionCodes.VIEW_LIST)]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _context.Roles
                .Select(r => new RoleDTO
                {
                    RoleId = r.RoleId,
                    Name = r.Name,
                    Code = r.Code,
                    IsSystem = r.IsSystem,
                    IsActive = r.IsActive,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                })
                .ToListAsync();
            return Ok(roles);
        }

        /**
         * Summary: Retrieve a role by id including role-permissions.
         * Route: GET /api/roles/{id}
         * Params: id (string) - role identifier
         * Returns: 200 OK with role, 404 if not found
         */
        [HttpGet("{id}")]
        [RequirePermission(ModuleCodes.ROLE, PermissionCodes.VIEW_DETAIL)]
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
                Code = role.Code,
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

        /**
         * Summary: Create a new role and seed role-permissions for all modules & permissions.
         * Route: POST /api/roles
         * Body: CreateRoleDTO
         * Returns: 201 Created with created role, 400/409 on validation errors
         */
        [HttpPost]
        [RequirePermission(ModuleCodes.ROLE, PermissionCodes.CREATE)]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleDTO createRoleDto)
        {
            if (createRoleDto == null)
            {
                return BadRequest("Dữ liệu không hợp lệ.");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(new { message = string.Join(" ", errors) });
            }

            var existingRole = await _context.Roles
                .FirstOrDefaultAsync(m => m.Name == createRoleDto.Name);
            if (existingRole != null)
            {
                return Conflict(new { message = "Tên vai trò đã tồn tại." });
            }

            // Check if Code is unique (if provided)
            if (!string.IsNullOrWhiteSpace(createRoleDto.Code))
            {
                var existingCode = await _context.Roles
                    .FirstOrDefaultAsync(m => m.Code == createRoleDto.Code);
                if (existingCode != null)
                {
                    return Conflict(new { message = "Mã vai trò đã tồn tại." });
                }
            }

            // Use provided RoleId or use Code as RoleId
            var roleId = !string.IsNullOrWhiteSpace(createRoleDto.RoleId) 
                ? createRoleDto.RoleId 
                : createRoleDto.Code;
            
            // Check if RoleId already exists
            var existingRoleId = await _context.Roles
                .FirstOrDefaultAsync(m => m.RoleId == roleId);
            if (existingRoleId != null)
            {
                return Conflict(new { message = "ID vai trò đã tồn tại." });
            }

            var newRole = new Role
            {
                RoleId = roleId,
                Name = createRoleDto.Name,
                Code = createRoleDto.Code,
                IsSystem = createRoleDto.IsSystem,
                IsActive = true,
                CreatedAt = DateTime.Now
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
                Code = newRole.Code,
                IsSystem = newRole.IsSystem,
                IsActive = newRole.IsActive,
                CreatedAt = newRole.CreatedAt,
                UpdatedAt = newRole.UpdatedAt
            };

            // ===== AUDIT LOG: CREATE ROLE (success only) =====
            await _auditLogger.LogAsync(
                HttpContext,
                action: "Create",
                entityType: "Role",
                entityId: newRole.RoleId,
                before: null,
                after: roleDto
            );

            return CreatedAtAction(nameof(GetRoleById), new { id = newRole.RoleId }, roleDto);
        }

        /**
        * Summary: Update an existing role by id.
        * Route: PUT /api/roles/{id}
        * Params: id (string)
        * Body: UpdateRoleDTO
        * Returns: 204 No Content, 400/404 on errors
        */
        [HttpPut("{id}")]
        [RequirePermission(ModuleCodes.ROLE, PermissionCodes.EDIT)]
        public async Task<IActionResult> UpdateRole(string id, [FromBody] UpdateRoleDTO updateRoleDto)
        {
            if (updateRoleDto == null)
            {
                return BadRequest("Dữ liệu không hợp lệ.");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage);
                return BadRequest(new { message = string.Join(" ", errors) });
            }

            var existingRole = await _context.Roles.FindAsync(id);
            if (existingRole == null)
            {
                return NotFound();
            }

            // Snapshot before
            var before = new
            {
                existingRole.RoleId,
                existingRole.Name,
                existingRole.Code,
                existingRole.IsActive,
                existingRole.IsSystem
            };

            // Check if Code is unique (if provided and changed)
            if (!string.IsNullOrWhiteSpace(updateRoleDto.Code) &&
                existingRole.Code != updateRoleDto.Code)
            {
                var existingCode = await _context.Roles
                    .FirstOrDefaultAsync(m => m.Code == updateRoleDto.Code && m.RoleId != id);
                if (existingCode != null)
                {
                    return Conflict(new { message = "Mã vai trò đã tồn tại." });
                }
            }

            existingRole.Name = updateRoleDto.Name;
            existingRole.Code = updateRoleDto.Code;
            existingRole.IsActive = updateRoleDto.IsActive;
            existingRole.UpdatedAt = DateTime.Now;

            _context.Roles.Update(existingRole);
            await _context.SaveChangesAsync();

            var after = new
            {
                existingRole.RoleId,
                existingRole.Name,
                existingRole.Code,
                existingRole.IsActive,
                existingRole.IsSystem
            };

            // ===== AUDIT LOG: UPDATE ROLE (success only) =====
            await _auditLogger.LogAsync(
                HttpContext,
                action: "Update",
                entityType: "Role",
                entityId: existingRole.RoleId,
                before: before,
                after: after
            );

            return NoContent();
        }

        /**
         * Summary: Delete a role by id and cascade remove related role-permissions.
         * Route: DELETE /api/roles/{id}
         * Params: id (string)
         * Returns: 204 No Content, 404 if not found
         */
        [HttpDelete("{id}")]
        [RequirePermission(ModuleCodes.ROLE, PermissionCodes.DELETE)]
        public async Task<IActionResult> DeleteRoleById(string id)
        {
            var existingRole = await _context.Roles.FindAsync(id);
            if (existingRole == null)
            {
                return NotFound();
            }

            // Snapshot before delete
            var before = new
            {
                existingRole.RoleId,
                existingRole.Name,
                existingRole.Code,
                existingRole.IsActive,
                existingRole.IsSystem
            };

            var rolePermissions = _context.RolePermissions.Where(rp => rp.RoleId == id);
            _context.RolePermissions.RemoveRange(rolePermissions);
            _context.Roles.Remove(existingRole);
            await _context.SaveChangesAsync();

            // ===== AUDIT LOG: DELETE ROLE (success only) =====
            await _auditLogger.LogAsync(
                HttpContext,
                action: "Delete",
                entityType: "Role",
                entityId: existingRole.RoleId,
                before: before,
                after: null
            );

            return NoContent();
        }

        /**
         * Summary: Retrieve all active roles.
         * Route: GET /api/roles/active
         * Params: none
         * Returns: 200 OK with list of active roles
         */
        [HttpGet("active")]
        public async Task<IActionResult> GetActiveRoles()
        {
            var activeRoles = await _context.Roles
                .Where(r => r.IsActive == true)
                .Select(r => new RoleDTO
                {
                    RoleId = r.RoleId,
                    Name = r.Name,
                    Code = r.Code,
                    IsSystem = r.IsSystem,
                    IsActive = r.IsActive,
                    CreatedAt = r.CreatedAt,
                    UpdatedAt = r.UpdatedAt
                })
                .ToListAsync();

            return Ok(activeRoles);
        }

        /**
         * Summary: Get role permissions matrix for a specific role.
         * Route: GET /api/roles/{id}/permissions
         * Params: id (string) - role identifier
         * Returns: 200 OK with role permissions matrix, 404 if role not found
         */
        [HttpGet("{id}/permissions")]
        [RequirePermission(ModuleCodes.ROLE, PermissionCodes.VIEW_DETAIL)]
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

        /**
         * Summary: Bulk update role permissions for a specific role.
         * Route: PUT /api/roles/{id}/permissions
         * Params: id (string) - role identifier
         * Body: BulkRolePermissionUpdateDTO - list of role permissions to update
         * Returns: 200 OK with updated role permissions, 400/404 on errors
         */
        [HttpPut("{id}/permissions")]
        [RequirePermission(ModuleCodes.ROLE, PermissionCodes.EDIT)]
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

                // Snapshot before
                var before = existingRolePermissions
                    .Select(rp => new
                    {
                        rp.RoleId,
                        rp.ModuleId,
                        rp.PermissionId,
                        rp.IsActive
                    })
                    .ToList();

                // Update or create role permissions
                foreach (var permissionDto in updateDto.RolePermissions)
                {
                    var existingPermission = existingRolePermissions
                        .FirstOrDefault(rp =>
                            rp.ModuleId == permissionDto.ModuleId &&
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

                // ===== AUDIT LOG: UPDATE ROLE PERMISSIONS (success only) =====
                await _auditLogger.LogAsync(
                    HttpContext,
                    action: "UpdatePermissions",
                    entityType: "Role",
                    entityId: role.RoleId,
                    before: before,
                    after: response
                );

                return Ok(response);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while updating role permissions.", error = ex.Message });
            }
        }

        /**
         * Summary: Retrieve modules that the provided role codes can access for a specific permission.
         * Route: POST /api/roles/module-access
         * Body: ModuleAccessRequestDTO - list of role codes and permission code (required, no default)
         * Returns: 200 OK with list of modules the roles can access
         * Note: PermissionCode is now required (ACCESS permission has been removed)
         */
        [HttpPost("module-access")]
        public async Task<IActionResult> GetModuleAccessForRoles([FromBody] ModuleAccessRequestDTO request)
        {
            if (request == null || request.RoleCodes == null || !request.RoleCodes.Any())
            {
                return BadRequest(new { message = "Danh sách vai trò không được để trống." });
            }

            var normalizedRoleCodes = request.RoleCodes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim().ToUpper())
                .Distinct()
                .ToList();

            if (!normalizedRoleCodes.Any())
            {
                return BadRequest(new { message = "Danh sách vai trò không được để trống." });
            }

            if (string.IsNullOrWhiteSpace(request.PermissionCode))
            {
                return BadRequest(new { message = "PermissionCode là bắt buộc." });
            }

            var permissionCode = request.PermissionCode.Trim().ToUpper();

            var modules = await _context.RolePermissions
                .Include(rp => rp.Module)
                .Include(rp => rp.Permission)
                .Include(rp => rp.Role)
                .Where(rp =>
                    rp.IsActive &&
                    rp.Module != null &&
                    rp.Permission != null &&
                    rp.Role != null &&
                    !string.IsNullOrWhiteSpace(rp.Role.Code) &&
                    normalizedRoleCodes.Contains(rp.Role.Code.ToUpper()) &&
                    !string.IsNullOrWhiteSpace(rp.Permission.Code) &&
                    rp.Permission.Code.ToUpper() == permissionCode)
                .GroupBy(rp => new
                {
                    rp.ModuleId,
                    ModuleName = rp.Module!.ModuleName,
                    ModuleCode = rp.Module.Code
                })
                .Select(g => new ModuleAccessDTO
                {
                    ModuleId = g.Key.ModuleId,
                    ModuleName = g.Key.ModuleName,
                    ModuleCode = g.Key.ModuleCode
                })
                .ToListAsync();

            return Ok(modules);
        }

        /**
         * Summary: Check if a role has active permission for a module and permission.
         * Route: POST /api/roles/check-permission
         * Body: CheckPermissionRequestDTO - role code, module code, permission code
         * Returns: 200 OK with CheckPermissionResponseDTO indicating if access is granted
         */
        [HttpPost("check-permission")]
        public async Task<IActionResult> CheckPermission([FromBody] CheckPermissionRequestDTO request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.RoleCode) ||
                string.IsNullOrWhiteSpace(request.ModuleCode) ||
                string.IsNullOrWhiteSpace(request.PermissionCode))
            {
                return BadRequest(new { message = "Role code, module code, and permission code are required." });
            }

            try
            {
                // Find role by code
                var role = await _context.Roles
                    .FirstOrDefaultAsync(r => r.Code == request.RoleCode);

                if (role == null)
                {
                    return Ok(new CheckPermissionResponseDTO
                    {
                        HasAccess = false,
                        Message = "Role not found."
                    });
                }

                // Find module by code
                var module = await _context.Modules
                    .FirstOrDefaultAsync(m => m.Code == request.ModuleCode);

                if (module == null)
                {
                    return Ok(new CheckPermissionResponseDTO
                    {
                        HasAccess = false,
                        Message = "Module not found."
                    });
                }

                // Find permission by code
                var permission = await _context.Permissions
                    .FirstOrDefaultAsync(p => p.Code == request.PermissionCode);

                if (permission == null)
                {
                    return Ok(new CheckPermissionResponseDTO
                    {
                        HasAccess = false,
                        Message = "Permission not found."
                    });
                }

                // Check if role permission exists and is active
                var rolePermission = await _context.RolePermissions
                    .FirstOrDefaultAsync(rp =>
                        rp.RoleId == role.RoleId &&
                        rp.ModuleId == module.ModuleId &&
                        rp.PermissionId == permission.PermissionId);

                bool hasAccess = rolePermission != null && rolePermission.IsActive;

                return Ok(new CheckPermissionResponseDTO
                {
                    HasAccess = hasAccess,
                    Message = hasAccess ? "Access granted." : "Access denied."
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while checking permission.", error = ex.Message });
            }
        }

        /**
         * Summary: Check permissions for multiple role codes (for users with multiple roles).
         * Route: POST /api/roles/check-permissions
         * Body: List of CheckPermissionRequestDTO
         * Returns: 200 OK with list of CheckPermissionResponseDTO
         */
        [HttpPost("check-permissions")]
        public async Task<IActionResult> CheckPermissions([FromBody] List<CheckPermissionRequestDTO> requests)
        {
            if (requests == null || !requests.Any())
            {
                return BadRequest(new { message = "At least one permission check request is required." });
            }

            try
            {
                var results = new List<CheckPermissionResponseDTO>();

                foreach (var request in requests)
                {
                    if (string.IsNullOrWhiteSpace(request.RoleCode) ||
                        string.IsNullOrWhiteSpace(request.ModuleCode) ||
                        string.IsNullOrWhiteSpace(request.PermissionCode))
                    {
                        results.Add(new CheckPermissionResponseDTO
                        {
                            HasAccess = false,
                            Message = "Invalid request parameters."
                        });
                        continue;
                    }

                    // Find role by code
                    var role = await _context.Roles
                        .FirstOrDefaultAsync(r => r.Code == request.RoleCode);

                    if (role == null)
                    {
                        results.Add(new CheckPermissionResponseDTO
                        {
                            HasAccess = false,
                            Message = "Role not found."
                        });
                        continue;
                    }

                    // Find module by code
                    var module = await _context.Modules
                        .FirstOrDefaultAsync(m => m.Code == request.ModuleCode);

                    if (module == null)
                    {
                        results.Add(new CheckPermissionResponseDTO
                        {
                            HasAccess = false,
                            Message = "Module not found."
                        });
                        continue;
                    }

                    // Find permission by code
                    var permission = await _context.Permissions
                        .FirstOrDefaultAsync(p => p.Code == request.PermissionCode);

                    if (permission == null)
                    {
                        results.Add(new CheckPermissionResponseDTO
                        {
                            HasAccess = false,
                            Message = "Permission not found."
                        });
                        continue;
                    }

                    // Check if role permission exists and is active
                    var rolePermission = await _context.RolePermissions
                        .FirstOrDefaultAsync(rp =>
                            rp.RoleId == role.RoleId &&
                            rp.ModuleId == module.ModuleId &&
                            rp.PermissionId == permission.PermissionId);

                    var hasAccess = rolePermission != null && rolePermission.IsActive;

                    results.Add(new CheckPermissionResponseDTO
                    {
                        HasAccess = hasAccess,
                        Message = hasAccess ? "Access granted." : "Access denied."
                    });
                }

                return Ok(results);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while checking permissions.", error = ex.Message });
            }
        }

        /**
         * Summary: Get all permissions for the provided role codes.
         * Route: POST /api/roles/user-permissions
         * Body: UserPermissionsRequestDTO - list of role codes
         * Returns: 200 OK with list of all permissions (moduleCode, permissionCode pairs)
         */
        [HttpPost("user-permissions")]
        public async Task<IActionResult> GetUserPermissions([FromBody] UserPermissionsRequestDTO request)
        {
            if (request == null || request.RoleCodes == null || !request.RoleCodes.Any())
            {
                return BadRequest(new { message = "Danh sách vai trò không được để trống." });
            }

            var normalizedRoleCodes = request.RoleCodes
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Select(code => code.Trim().ToUpper())
                .Distinct()
                .ToList();

            if (!normalizedRoleCodes.Any())
            {
                return BadRequest(new { message = "Danh sách vai trò không được để trống." });
            }

            var permissions = await _context.RolePermissions
                .Include(rp => rp.Module)
                .Include(rp => rp.Permission)
                .Include(rp => rp.Role)
                .Where(rp =>
                    rp.IsActive &&
                    rp.Module != null &&
                    rp.Permission != null &&
                    rp.Role != null &&
                    !string.IsNullOrWhiteSpace(rp.Role.Code) &&
                    normalizedRoleCodes.Contains(rp.Role.Code.ToUpper()) &&
                    !string.IsNullOrWhiteSpace(rp.Module.Code) &&
                    !string.IsNullOrWhiteSpace(rp.Permission.Code))
                .Select(rp => new UserPermissionItemDTO
                {
                    ModuleCode = rp.Module!.Code!.Trim().ToUpper(),
                    PermissionCode = rp.Permission!.Code!.Trim().ToUpper()
                })
                .Distinct()
                .ToListAsync();

            var response = new UserPermissionsResponseDTO
            {
                Permissions = permissions
            };

            return Ok(response);
        }
    }
}
