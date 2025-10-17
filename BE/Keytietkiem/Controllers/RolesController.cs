using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Keytietkiem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RolesController : ControllerBase
    {
        private readonly KeytietkiemContext _context;
        public RolesController(KeytietkiemContext context)
        {
            _context = context;
        }
        // GET: api/<RolesController>
        [HttpGet]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _context.Roles.ToListAsync();
            return Ok(roles);
        }

        // GET api/<RolesController>/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetRoleById(long id)
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
                        IsActive = true,
                        EffectiveFrom = DateTime.Now
                    });
                }
            }

            _context.RolePermissions.AddRange(rolePermissions);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetRoleById), new { id = newRole.RoleId }, newRole);
        }
        // PUT api/<RolesController>/5
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateRole(long id, [FromBody] Role updatedRole)
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
            existingRole.Desc = updatedRole.Desc;
            existingRole.IsActive = updatedRole.IsActive;
            existingRole.UpdatedAt = DateTime.UtcNow;
            _context.Roles.Update(existingRole);
            await _context.SaveChangesAsync();
            return NoContent();
        }
        // DELETE api/<ModulesController>/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRoleById(long id)
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
