/**
 * File: ModulesController.cs
 * Author: HieuNDHE173169
 * Created: 16/10/2025
 * Last Updated: 20/10/2025
 * Version: 1.0.0
 * Purpose: Manage application modules (CRUD). Also cascades delete to related
 *          role-permissions to maintain integrity.
 * Endpoints:
 *   - GET    /api/modules              : List modules
 *   - GET    /api/modules/{id}         : Get a module by id
 *   - POST   /api/modules              : Create a module
 *   - PUT    /api/modules/{id}         : Update a module
 *   - DELETE /api/modules/{id}         : Delete a module and its role-permissions
 */
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Keytietkiem.DTOs.Roles;
using Keytietkiem.Models;
using Keytietkiem.Services;
using Keytietkiem.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Keytietkiem.Attributes;
using Keytietkiem.Constants;
using Microsoft.EntityFrameworkCore;
namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ModulesController : ControllerBase
    {
        private readonly KeytietkiemDbContext _context;
        private readonly IAuditLogger _auditLogger;

        public ModulesController(
            KeytietkiemDbContext context,
            IAuditLogger auditLogger)
        {
            _context = context;
            _auditLogger = auditLogger;
        }

        /**
        * Summary: Retrieve all modules.
        * Route: GET /api/modules
        * Params: none
        * Returns: 200 OK with list of modules
        */
        [HttpGet]
    [RequireRole(RoleCodes.ADMIN)]
        public async Task<IActionResult> GetModules()
        {
            var modules = await _context.Modules
                .Select(m => new ModuleDTO
                {
                    ModuleId = m.ModuleId,
                    ModuleName = m.ModuleName,
                    Code = m.Code,
                    Description = m.Description,
                    CreatedAt = m.CreatedAt,
                    UpdatedAt = m.UpdatedAt
                })
                .ToListAsync();

            return Ok(modules);
        }

        /**
         * Summary: Retrieve a module by id.
         * @Route: GET /api/modules/{id}
         * @Params: id (long) - module identifier
         * @Returns: 200 OK with module, 404 if not found
         */
        [HttpGet("{id}")]
    [RequireRole(RoleCodes.ADMIN)]
        public async Task<IActionResult> GetModuleById(long id)
        {
            var module = await _context.Modules
                .FirstOrDefaultAsync(m => m.ModuleId == id);

            if (module == null)
            {
                return NotFound();
            }

            var moduleDto = new ModuleDTO
            {
                ModuleId = module.ModuleId,
                ModuleName = module.ModuleName,
                Code = module.Code,
                Description = module.Description,
                CreatedAt = module.CreatedAt,
                UpdatedAt = module.UpdatedAt
            };

            return Ok(moduleDto);
        }

        /**
         * Summary: Create a new module.
         * Route: POST /api/modules
         * Body: CreateModuleDTO createModuleDto
         * Returns: 201 Created with created module, 400/409 on validation errors
         */
        [HttpPost]
    [RequireRole(RoleCodes.ADMIN)]
        public async Task<IActionResult> CreateModule([FromBody] CreateModuleDTO createModuleDto)
        {
            if (createModuleDto == null)
            {
                return BadRequest("Dữ liệu không hợp lệ.");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(new { message = string.Join(" ", errors) });
            }
            var existing = await _context.Modules
                .FirstOrDefaultAsync(m => m.ModuleName == createModuleDto.ModuleName);
            if (existing != null)
            {
                return Conflict(new { message = "Tên module đã tồn tại." });
            }

            // Check if Code is unique (if provided)
            if (!string.IsNullOrWhiteSpace(createModuleDto.Code))
            {
                var existingCode = await _context.Modules
                    .FirstOrDefaultAsync(m => m.Code == createModuleDto.Code);
                if (existingCode != null)
                {
                    return Conflict(new { message = "Mã module đã tồn tại." });
                }
            }

            var newModule = new Module
            {
                ModuleName = createModuleDto.ModuleName,
                Code = createModuleDto.Code,
                Description = createModuleDto.Description,
                CreatedAt = DateTime.Now
            };

            _context.Modules.Add(newModule);
            await _context.SaveChangesAsync();

            // Add RolePermissions for all existing roles and permissions with this new module
            var roles = await _context.Roles.ToListAsync();
            var permissions = await _context.Permissions.ToListAsync();

            var rolePermissions = new List<RolePermission>();
            foreach (var role in roles)
            {
                foreach (var permission in permissions)
                {
                    rolePermissions.Add(new RolePermission
                    {
                        RoleId = role.RoleId,
                        ModuleId = newModule.ModuleId,
                        PermissionId = permission.PermissionId,
                        IsActive = true
                    });
                }
            }

            _context.RolePermissions.AddRange(rolePermissions);
            await _context.SaveChangesAsync();

            var moduleDto = new ModuleDTO
            {
                ModuleId = newModule.ModuleId,
                ModuleName = newModule.ModuleName,
                Code = newModule.Code,
                Description = newModule.Description,
                CreatedAt = newModule.CreatedAt,
                UpdatedAt = newModule.UpdatedAt
            };

            await _auditLogger.LogAsync(
                HttpContext,
                action: "CreateModule",
                entityType: "Module",
                entityId: newModule.ModuleId.ToString(),
                before: createModuleDto,
                after: new
                {
                    newModule.ModuleId,
                    newModule.ModuleName,
                    newModule.Code
                }
            );

            return CreatedAtAction(nameof(GetModuleById), new { id = newModule.ModuleId }, moduleDto);
        }

        /**
         * Summary: Update an existing module by id.
         * Route: PUT /api/modules/{id}
         * Params: id (long)
         * Body: UpdateModuleDTO updateModuleDto
         * Returns: 204 No Content, 400/404/409 on errors
         */
        [HttpPut("{id}")]
    [RequireRole(RoleCodes.ADMIN)]
        public async Task<IActionResult> UpdateModule(long id, [FromBody] UpdateModuleDTO updateModuleDto)
        {
            if (updateModuleDto == null)
            {
                return BadRequest("Dữ liệu không hợp lệ.");
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                return BadRequest(new { message = string.Join(" ", errors) });
            }

            var existing = await _context.Modules
                .FirstOrDefaultAsync(m => m.ModuleId == id);
            if (existing == null)
            {
                return NotFound();
            }

            // Check if Code is unique (if provided and changed)
            if (!string.IsNullOrWhiteSpace(updateModuleDto.Code) && existing.Code != updateModuleDto.Code)
            {
                var existingCode = await _context.Modules
                    .FirstOrDefaultAsync(m => m.Code == updateModuleDto.Code && m.ModuleId != id);
                if (existingCode != null)
                {
                    return Conflict(new { message = "Mã module đã tồn tại." });
                }
            }

            var beforeSnapshot = new
            {
                existing.ModuleId,
                existing.ModuleName,
                existing.Code,
                existing.Description
            };

            existing.ModuleName = updateModuleDto.ModuleName;
            existing.Code = updateModuleDto.Code;
            existing.Description = updateModuleDto.Description;
            existing.UpdatedAt = DateTime.Now;

            _context.Modules.Update(existing);
            await _context.SaveChangesAsync();

            await _auditLogger.LogAsync(
                HttpContext,
                action: "UpdateModule",
                entityType: "Module",
                entityId: existing.ModuleId.ToString(),
                before: beforeSnapshot,
                after: new
                {
                    existing.ModuleId,
                    existing.ModuleName,
                    existing.Code,
                    existing.Description
                }
            );

            return NoContent();
        }

        /**
         * Summary: Delete a module by id and cascade remove related role-permissions.
         * Route: DELETE /api/modules/{id}
         * Params: id (long)
         * Returns: 204 No Content, 404 if not found
         */
        [HttpDelete("{id}")]
    [RequireRole(RoleCodes.ADMIN)]
        public async Task<IActionResult> DeleteModule(long id)
        {
            var existingModule = await _context.Modules
                .Include(m => m.RolePermissions)
                .FirstOrDefaultAsync(m => m.ModuleId == id);

            if (existingModule == null)
            {
                return NotFound();
            }

            var beforeSnapshot = new
            {
                existingModule.ModuleId,
                existingModule.ModuleName,
                existingModule.Code,
                existingModule.Description
            };

            _context.RolePermissions.RemoveRange(existingModule.RolePermissions);
            _context.Modules.Remove(existingModule);
            await _context.SaveChangesAsync();

            await _auditLogger.LogAsync(
                HttpContext,
                action: "DeleteModule",
                entityType: "Module",
                entityId: existingModule.ModuleId.ToString(),
                before: beforeSnapshot,
                after: null
            );

            return NoContent();
        }
    }
}
