using Keytietkiem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KeytietkiemApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountsController : ControllerBase
    {
        private readonly KeytietkiemContext _context;

        public AccountsController(KeytietkiemContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Account>>> GetAccounts()
        {
            return await _context.Accounts.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Account>> GetAccount(Guid id)
        {
            
            var acc = await _context.Accounts.FindAsync(id);

            if (acc == null) return NotFound();
            return acc;
        }

        [HttpPost]
        public async Task<ActionResult<Account>> CreateAccount(Account acc)
        {
            acc.AccountId = Guid.NewGuid();
            _context.Accounts.Add(acc);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetAccount), new { id = acc.AccountId }, acc);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAccount(Guid id, Account acc)
        {
            if (id != acc.AccountId) return BadRequest();
            _context.Entry(acc).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAccount(Guid id)
        {
            var acc = await _context.Accounts.FindAsync(id);
            if (acc == null) return NotFound();
            _context.Accounts.Remove(acc);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
