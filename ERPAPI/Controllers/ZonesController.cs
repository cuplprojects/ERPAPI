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
    public class ZonesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ZonesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Zones
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetZones()
        {
            // Fetch all zones from the database
            var zones = await _context.Zone.ToListAsync();

            // Fetch all machines and create a dictionary of their names
            var machines = await _context.Machine.ToListAsync();
            var machineDictionary = machines.ToDictionary(m => m.MachineId, m => m.MachineName);

            // Fetch all cameras and create a dictionary of their names
            var cameras = await _context.Camera.ToListAsync();
            var cameraDictionary = cameras.ToDictionary(c => c.CameraId, c => c.Name);

            // Map zones to include full names of assigned machines and cameras
            var zonesWithNames = zones.Select(zone => new
            {
                zone.ZoneId,
                zone.ZoneNo,
                zone.ZoneDescription,
                zone.CameraIds,
                zone.MachineId,
                MachineNames = zone.MachineId
                    .Select(machineId => machineDictionary.TryGetValue(machineId, out var machineName) ? machineName : "Unknown Machine")
                    .ToList(),
                CameraNames = zone.CameraIds
                    .Select(cameraId => cameraDictionary.TryGetValue(cameraId, out var cameraName) ? cameraName : "Unknown Camera")
                    .ToList()
            }).ToList();

            return Ok(zonesWithNames);
        }


        // GET: api/Zones/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Zone>> GetZone(int id)
        {
            var zone = await _context.Zone.FindAsync(id);

            if (zone == null)
            {
                return NotFound();
            }

            return zone;
        }

        // PUT: api/Zones/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutZone(int id, Zone zone)
        {
            if (id != zone.ZoneId)
            {
                return BadRequest();
            }

            _context.Entry(zone).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ZoneExists(id))
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

        // POST: api/Zones
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Zone>> PostZone(Zone zone)
        {
            _context.Zone.Add(zone);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetZone", new { id = zone.ZoneId }, zone);
        }

        // DELETE: api/Zones/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteZone(int id)
        {
            var zone = await _context.Zone.FindAsync(id);
            if (zone == null)
            {
                return NotFound();
            }

            _context.Zone.Remove(zone);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ZoneExists(int id)
        {
            return _context.Zone.Any(e => e.ZoneId == id);
        }
    }
}
