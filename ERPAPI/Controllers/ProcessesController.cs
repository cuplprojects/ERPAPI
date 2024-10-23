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
    public class ProcessesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProcessesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Processes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetProcesses()
        {
            // Fetch all processes from the database
            var processes = await _context.Processes.ToListAsync();

            // Fetch all features and create a dictionary of their names
            var features = await _context.Features.ToListAsync();
            var featureDictionary = features.ToDictionary(f => f.FeatureId, f => f.Features);

            // Map processes to include full names of installed features and new properties
            var processesWithNames = processes.Select(process => new
            {
                process.Id,
                process.Name,
                process.Weightage,    // Include new field Weightage
                process.Status,
                process.InstalledFeatures,
                FeatureNames = process.InstalledFeatures
                    .Select(featureId => featureDictionary.TryGetValue(featureId, out var featureName) ? featureName : "Unknown Feature")
                    .ToList(),
                process.ProcessIdInput,  // Include new field ProcessIdInput
                process.ProcessType,     // Include new field ProcessType
                process.RangeStart,      // Include new field RangeStart
                process.RangeEnd         // Include new field RangeEnd
            }).ToList();

            return Ok(processesWithNames);
        }

        // GET: api/Processes/process
        [HttpGet("process")]
        public IActionResult GetCatchesByProcess(int processid)
        {
            // Fetch all QuantitySheets from the database
            var catches = _context.QuantitySheets.ToList();

            // Filter catches where the specified process ID is included in the ProcessId list
            var filteredCatches = catches
                .Where(q => q.ProcessId != null && q.ProcessId.Contains(processid))
                .ToList();

            // Check if there are no catches found
            if (filteredCatches == null || !filteredCatches.Any())
            {
                return NotFound($"No catches found for the process: {processid}");
            }

            return Ok(filteredCatches);
        }

        // GET: api/Processes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Process>> GetProcess(int id)
        {
            var process = await _context.Processes.FindAsync(id);

            if (process == null)
            {
                return NotFound();
            }

            return process;
        }

        // PUT: api/Processes/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutProcess(int id, Process process)
        {
            if (id != process.Id)
            {
                return BadRequest();
            }

            _context.Entry(process).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProcessExists(id))
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

        // POST: api/Processes
        [HttpPost]
        public async Task<ActionResult<Process>> PostProcess(Process process)
        {
            _context.Processes.Add(process);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetProcess", new { id = process.Id }, process);
        }

        // DELETE: api/Processes/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProcess(int id)
        {
            var process = await _context.Processes.FindAsync(id);
            if (process == null)
            {
                return NotFound();
            }

            _context.Processes.Remove(process);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ProcessExists(int id)
        {
            return _context.Processes.Any(e => e.Id == id);
        }
    }
}
