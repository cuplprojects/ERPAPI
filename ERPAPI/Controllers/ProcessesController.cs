using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ERPAPI.Data;
using ERPAPI.Model;
using ERPAPI.Services;
using ERPAPI.Service;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProcessesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ILoggerService _loggerService;

        public ProcessesController(AppDbContext context, ILoggerService loggerService)
        {
            _context = context;
            _loggerService = loggerService;
        }

        // GET: api/Processes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetProcesses()
        {
            try
            {
                var processes = await _context.Processes.ToListAsync();
                var features = await _context.Features.ToListAsync();
                var featureDictionary = features.ToDictionary(f => f.FeatureId, f => f.Features);

                var processesWithNames = processes.Select(process => new
                {
                    process.Id,
                    process.Name,
                    process.Weightage,
                    process.Status,
                    process.InstalledFeatures,
                    FeatureNames = process.InstalledFeatures
                        .Select(featureId => featureDictionary.TryGetValue(featureId, out var featureName) ? featureName : "Unknown Feature")
                        .ToList(),
                 
                    process.ProcessType,
                    process.RangeStart,
                    process.RangeEnd
                }).ToList();

                return Ok(processesWithNames);
            }
            catch (Exception)
            {
                
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Processes/process
        [HttpGet("process")]
        public IActionResult GetCatchesByProcess(int processid)
        {
            try
            {
                var catches = _context.QuantitySheets.ToList();
                var filteredCatches = catches
                    .Where(q => q.ProcessId != null && q.ProcessId.Contains(processid))
                    .ToList();

                if (filteredCatches == null || !filteredCatches.Any())
                {
                    
                    return NotFound($"No catches found for the process: {processid}");
                }

                return Ok(filteredCatches);
            }
            catch (Exception ex)
            {
              
                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Processes/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Process>> GetProcess(int id)
        {
            try
            {
                var process = await _context.Processes.FindAsync(id);
                if (process == null)
                {
                    _loggerService.LogEvent($"Process with ID {id} not found", "Processes", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }
                return process;
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error fetching process by ID", ex.Message, nameof(ProcessesController));
                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/Processes/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutProcess(int id, Process process)
        {
            if (id != process.Id)
            {
                return BadRequest();
            }

            // Fetch existing entity to capture old values
            var existingProcess = await _context.Processes.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
            if (existingProcess == null)
            {
                _loggerService.LogEvent($"Process with ID {id} not found during update", "Processes", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                return NotFound();
            }

            // Capture old and new values for logging
            string oldValue = Newtonsoft.Json.JsonConvert.SerializeObject(existingProcess);
            string newValue = Newtonsoft.Json.JsonConvert.SerializeObject(process);

            _context.Entry(process).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
                _loggerService.LogEvent($"Updated process with ID {id}", "Processes", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0, oldValue, newValue);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!ProcessExists(id))
                {
                    _loggerService.LogEvent($"Process with ID {id} not found during update", "Processes", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }
                else
                {
                    _loggerService.LogError("Concurrency error during process update", ex.Message, nameof(ProcessesController));
                    throw;
                }
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error updating process", ex.Message, nameof(ProcessesController));
                return StatusCode(500, "Internal server error");
            }

            return NoContent();
        }

        // POST: api/Processes
        [HttpPost]
        public async Task<ActionResult<Process>> PostProcess(Process process)
        {
            try
            {
                _context.Processes.Add(process);
                await _context.SaveChangesAsync();

                _loggerService.LogEvent("Created a new process", "Processes", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                return CreatedAtAction("GetProcess", new { id = process.Id }, process);
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error creating process", ex.Message, nameof(ProcessesController));
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/Processes/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProcess(int id)
        {
            try
            {
                var process = await _context.Processes.FindAsync(id);
                if (process == null)
                {
                    _loggerService.LogEvent($"Process with ID {id} not found during delete", "Processes", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                    return NotFound();
                }

                _context.Processes.Remove(process);
                await _context.SaveChangesAsync();

                _loggerService.LogEvent($"Deleted process with ID {id}", "Processes", User.Identity?.Name != null ? int.Parse(User.Identity.Name) : 0);
                return NoContent();
            }
            catch (Exception ex)
            {
                _loggerService.LogError("Error deleting process", ex.Message, nameof(ProcessesController));
                return StatusCode(500, "Internal server error");
            }
        }

        private bool ProcessExists(int id)
        {
            return _context.Processes.Any(e => e.Id == id);
        }
    }
}






/*using System;
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

            // Map processes to include full names of installed features

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
*/