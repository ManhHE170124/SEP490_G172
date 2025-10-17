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
        public async Task<IActionResult> GetModules()
        {
            var modules = await _context.Modules.ToListAsync();
            return Ok(modules);
        }
        // GET api/<ModulesController>/5
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
