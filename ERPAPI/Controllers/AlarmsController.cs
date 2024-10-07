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
    public class AlarmsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AlarmsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Alarms
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Alarm>>> GetAlarm()
        {
            return await _context.Alarm.ToListAsync();
        }

        // GET: api/Alarms/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Alarm>> GetAlarm(int id)
        {
            var alarm = await _context.Alarm.FindAsync(id);

            if (alarm == null)
            {
                return NotFound();
            }

            return alarm;
        }

        // PUT: api/Alarms/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutAlarm(int id, Alarm alarm)
        {
            if (id != alarm.AlarmId)
            {
                return BadRequest();
            }

            _context.Entry(alarm).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!AlarmExists(id))
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

        // POST: api/Alarms
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Alarm>> PostAlarm(Alarm alarm)
        {
            _context.Alarm.Add(alarm);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetAlarm", new { id = alarm.AlarmId }, alarm);
        }

        // DELETE: api/Alarms/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAlarm(int id)
        {
            var alarm = await _context.Alarm.FindAsync(id);
            if (alarm == null)
            {
                return NotFound();
            }

            _context.Alarm.Remove(alarm);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool AlarmExists(int id)
        {
            return _context.Alarm.Any(e => e.AlarmId == id);
        }
    }
}
