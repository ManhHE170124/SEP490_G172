/*
  File: ModulesController.cs
  Author: HieuNDHE173169
  Created: 16/10/2025
  Last Updated: 20/10/2025
  Version: 1.0.0
  Purpose: Manage application modules (CRUD). Also cascades delete to related
           role-permissions to maintain integrity.
  Endpoints:
    - GET    /api/modules              : List modules
    - GET    /api/modules/{id}         : Get a module by id
    - POST   /api/modules              : Create a module
    - PUT    /api/modules/{id}         : Update a module
    - DELETE /api/modules/{id}         : Delete a module and its role-permissions
*/
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Keytietkiem.Models;
using Microsoft.EntityFrameworkCore;
namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ModulesController : ControllerBase
    {
        private readonly KeytietkiemContext _context;
        public ModulesController(KeytietkiemContext context)
        {
            _context = context;
        }
        // GET: api/<ModulesController>
        [HttpGet]
        /**
 * Summary: Retrieve all modules.
 * Route: GET /api/modules
 * Params: none
 * Returns: 200 OK with list of modules
 */
        public async Task<IActionResult> GetModules()
        {
            var modules = await _context.Modules.ToListAsync();
            return Ok(modules);
        }
        // GET api/<ModulesController>/5
        /**
         * Summary: Retrieve a module by id.
         * Route: GET /api/modules/{id}
         * Params: id (Guid) - module identifier
         * Returns: 200 OK with module, 404 if not found
         */
        [HttpGet("{id}")]
        public async Task<IActionResult> GetModuleById(Guid id)
        {
            var module = await _context.Modules
                .FirstOrDefaultAsync(m => m.ModuleId == id);
            if (module == null)
            {
                return NotFound();
            }
            return Ok(module);
        }
        // POST api/<ModulesController>
        [HttpPost]
        /**
         * Summary: Create a new module.
         * Route: POST /api/modules
         * Body: Module newModule
         * Returns: 201 Created with created module, 400/409 on validation errors
         */
        public async Task<IActionResult> CreateModule([FromBody] Module newModule)
        {
            if (newModule == null || string.IsNullOrWhiteSpace(newModule.ModuleName))
            {
                return BadRequest("Module name is required.");
            }
            var existing = await _context.Modules
                .FirstOrDefaultAsync(m => m.ModuleName == newModule.ModuleName);
            if (existing != null)
            {
                return Conflict(new { message = "Module name already exists." });
            }
            newModule.CreatedAt = DateTime.Now;
            _context.Modules.Add(newModule);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetModuleById), new { id = newModule.ModuleId }, newModule);
        }
        // PUT api/<ModulesController>/5
        [HttpPut("{id}")]
        /**
         * Summary: Update an existing module by id.
         * Route: PUT /api/modules/{id}
         * Params: id (Guid)
         * Body: Module updatedModule
         * Returns: 204 No Content, 400/404 on errors
         */
        public async Task<IActionResult> UpdateModule(Guid id, [FromBody] Module updatedModule)
        {
            if (updatedModule == null || id != updatedModule.ModuleId)
            {
                return BadRequest("Invalid module data.");
            }
            var existing = await _context.Modules
                .FirstOrDefaultAsync(m => m.ModuleId == id);
            if (existing == null)
            {
                return NotFound();
            }
            existing.ModuleName = updatedModule.ModuleName;
            existing.UpdatedAt = DateTime.Now;
            _context.Modules.Update(existing);
            await _context.SaveChangesAsync();
            return NoContent();
        }
        // DELETE api/<ModulesController>/5
        [HttpDelete("{id}")]
        /**
         * Summary: Delete a module by id and cascade remove related role-permissions.
         * Route: DELETE /api/modules/{id}
         * Params: id (Guid)
         * Returns: 204 No Content, 404 if not found
         */
        public async Task<IActionResult> DeleteModule(Guid id)
        {
            var existingModule = await _context.Modules
                .FirstOrDefaultAsync(m => m.ModuleId == id);
            if (existingModule == null)
            {
                return NotFound();
            }
            _context.RolePermissions.RemoveRange(existingModule.RolePermissions);
            _context.Modules.Remove(existingModule);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
