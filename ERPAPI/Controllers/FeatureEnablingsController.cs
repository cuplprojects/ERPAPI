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
    public class FeatureEnablingsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FeatureEnablingsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/FeatureEnablings
        [HttpGet]
        public async Task<ActionResult<IEnumerable<FeatureEnabling>>> GetFeatureEnabling()
        {
            return await _context.FeatureEnabling.ToListAsync();
        }

        // GET: api/FeatureEnablings/5
        [HttpGet("{id}")]
        public async Task<ActionResult<FeatureEnabling>> GetFeatureEnabling(int id)
        {
            var featureEnabling = await _context.FeatureEnabling.FindAsync(id);

            if (featureEnabling == null)
            {
                return NotFound();
            }

            return featureEnabling;
        }

        // PUT: api/FeatureEnablings/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutFeatureEnabling(int id, FeatureEnabling featureEnabling)
        {
            if (id != featureEnabling.FeatureEnablingId)
            {
                return BadRequest();
            }

            _context.Entry(featureEnabling).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!FeatureEnablingExists(id))
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

        // POST: api/FeatureEnablings
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<FeatureEnabling>> PostFeatureEnabling(FeatureEnabling featureEnabling)
        {
            _context.FeatureEnabling.Add(featureEnabling);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetFeatureEnabling", new { id = featureEnabling.FeatureEnablingId }, featureEnabling);
        }

        // DELETE: api/FeatureEnablings/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteFeatureEnabling(int id)
        {
            var featureEnabling = await _context.FeatureEnabling.FindAsync(id);
            if (featureEnabling == null)
            {
                return NotFound();
            }

            _context.FeatureEnabling.Remove(featureEnabling);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool FeatureEnablingExists(int id)
        {
            return _context.FeatureEnabling.Any(e => e.FeatureEnablingId == id);
        }
    }
}
