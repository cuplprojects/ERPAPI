using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ERPAPI.Data;
using ERPAPI.Model;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PaperTypesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PaperTypesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/PaperTypes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<PaperType>>> GetTypes()
        {
            return await _context.Types.ToListAsync();
        }

        // GET: api/PaperTypes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<PaperType>> GetPaperType(int id)
        {
            var paperType = await _context.Types.FindAsync(id);

            if (paperType == null)
            {
                return NotFound();
            }

            return paperType;
        }

        // PUT: api/PaperTypes/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPaperType(int id, PaperType paperType)
        {
            if (id != paperType.TypeId)
            {
                return BadRequest();
            }

            _context.Entry(paperType).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PaperTypeExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/PaperTypes
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<PaperType>> PostPaperType(PaperType paperType)
        {
            _context.Types.Add(paperType);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetPaperType", new { id = paperType.TypeId }, paperType);
        }

        // DELETE: api/PaperTypes/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePaperType(int id)
        {
            var paperType = await _context.Types.FindAsync(id);
            if (paperType == null)
            {
                return NotFound();
            }

            _context.Types.Remove(paperType);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool PaperTypeExists(int id)
        {
            return _context.Types.Any(e => e.TypeId == id);
        }
    }
}
