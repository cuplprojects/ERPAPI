using ERPAPI.Data;
using ERPAPI.Model;
using ERPAPI.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DisplayController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DisplayController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Displays
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Display>>> GetDisplays()
        {
            try
            {
                var Displays = await _context.Displays.OrderByDescending(g => g.DisplayId).ToListAsync();
                return Displays;
            }
            catch (Exception ex)
            {

                return StatusCode(500, "Internal server error");
            }
        }

        // GET: api/Displays/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Display>> GetDisplay(int id)
        {
            try
            {
                var Display = await _context.Displays.FindAsync(id);

                if (Display == null)
                {

                    return NotFound();
                }

                return Display;
            }
            catch (Exception)
            {

                return StatusCode(500, "Internal server error");
            }
        }

        // PUT: api/Displays/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutDisplay(int id, Display @Display)
        {
            if (id != @Display.DisplayId)
            {
                return BadRequest();
            }

            // Fetch existing entity to capture old values
            var existingDisplay = await _context.Displays.AsNoTracking().FirstOrDefaultAsync(g => g.DisplayId == id);
            if (existingDisplay == null)
            {
                return NotFound();
            }

            // Capture old and new values for logging
            string oldValue = Newtonsoft.Json.JsonConvert.SerializeObject(existingDisplay);
            string newValue = Newtonsoft.Json.JsonConvert.SerializeObject(@Display);

            _context.Entry(@Display).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!DisplayExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error");
            }

            return NoContent();
        }

        // POST: api/Displays
        [HttpPost]
        public async Task<ActionResult<Display>> PostDisplay(Display @Display)
        {
            try
            {
                _context.Displays.Add(@Display);
                await _context.SaveChangesAsync();
                return CreatedAtAction("GetDisplay", new { id = @Display.DisplayId }, @Display);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error");
            }
        }

        // DELETE: api/Displays/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDisplay(int id)
        {
            try
            {
                var Display = await _context.Displays.FindAsync(id);
                if (Display == null)
                {
                    return NotFound();
                }

                _context.Displays.Remove(Display);
                await _context.SaveChangesAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("ZonalDisplay")]
        public async Task<IActionResult> ZonalDisplay(int id)
        {
            try
            {
                var display = await _context.Displays.Where(p => p.DisplayId == id).ToListAsync();
                var displayIds = display.SelectMany(d=>d.Zones).ToList();
                var runningcatch = await _context.Transaction.Where(x => displayIds.Contains(x.ZoneId) && x.Status == 1).ToListAsync();
                var project = await _context.Projects.ToListAsync();
                var process = await _context.Processes.ToListAsync();
                var supervisor = await _context.ProjectProcesses.ToListAsync();
                var machine = await _context.Machine.ToListAsync();
                var quantitysheet = await _context.QuantitySheets.ToListAsync();
                var zones = await _context.Zone.ToListAsync();
                var user = await _context.Users.Where(u => u.RoleId == 5 || u.RoleId == 6 || u.RoleId == 3).ToListAsync();

                var transaction = runningcatch
                    .GroupBy(x => new {x.ZoneId, x.ProcessId, x.ProjectId })  // Grouping by ProcessId and ProjectId
                    .Select(group => new
                    {
                        ZoneId = group.Key.ZoneId,
                        ProcessId = group.Key.ProcessId,  // This is the ProcessId of the group
                        ProjectId = group.Key.ProjectId,  // This is the ProjectId of the group
                        Transactions = group.Select(x => new
                        {
                            x.LotNo,
                            TeamName = string.Join(", ", user
                                .Where(u => x.TeamId.Contains(u.UserId))  // Find all users whose UserId is in the TeamId list
                                .Select(u => u.FirstName + " " + u.LastName)),
                            ZoneNo = zones.FirstOrDefault(z => z.ZoneId == x.ZoneId)?.ZoneNo,
                            CurrentProcess = process.FirstOrDefault(s => s.Id == x.ProcessId)?.Name,
                            PreviousProcess = GetPreviousProcess(process, x.ProcessId, supervisor, x.ProjectId),
                            QuantitySheet = quantitysheet.FirstOrDefault(q => q.QuantitySheetId == x.QuantitysheetId)?.Quantity,
                            CatchNo = quantitysheet.FirstOrDefault(q => q.QuantitySheetId == x.QuantitysheetId)?.CatchNo,
                            MachineName = machine.FirstOrDefault(m => m.MachineId == x.MachineId)?.MachineName,
                            ProjectName = project.FirstOrDefault(p => p.ProjectId == x.ProjectId)?.Name,
                            Supervisor = string.Join(", ", user
                                .Where(u => supervisor.FirstOrDefault(s => s.ProcessId == x.ProcessId)?.UserId.Contains(u.UserId) == true)
                                .Select(u => u.FirstName + " " + u.LastName))
                        }).ToList()
                    }).ToList();

                var orderedTransactions = transaction
           .OrderBy(g => g.ZoneId)
           .ThenBy(g => g.ProcessId)
           .ThenBy(g => g.ProjectId)
           .ToList();

                if (runningcatch == null || !transaction.Any())
                {
                    return NotFound();
                }

                return Ok(orderedTransactions);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error");
            }
        }

        /* private string GetPreviousProcess(List<ERPAPI.Model.Process> processes, int currentProcessId, List<ProjectProcess> projectProcesses)
         {
             // Example: Custom logic for previous process when ProcessId is 4
             if (currentProcessId == 4)
             {
                 return processes.FirstOrDefault(p => p.Id == 2)?.Name;
             }

             // Get the current process based on its Id
             var currentProcess = processes.FirstOrDefault(p => p.Id == currentProcessId);
             var dependentprocessonindependent = processes.FirstOrDefault(p => p.RangeEnd == currentProcess.Id);

             // Ensure the current process exists before proceeding
             if (currentProcess != null)
             {
                 if (currentProcess.ProcessType == "Independent")
                 {
                     var previousProcess = processes.FirstOrDefault(p => p.Id == currentProcess.RangeStart);
                     return previousProcess?.Name;
                 }

                 if (dependentprocessonindependent != null)
                 {
                     var previousProcess = processes.FirstOrDefault(p => p.Id == dependentprocessonindependent.Id);
                     return previousProcess?.Name;
                 }

                 var currentProjectProcess = projectProcesses.FirstOrDefault(pp => pp.ProcessId == currentProcessId);

                 // If the current ProjectProcess exists, we can use the Sequence from there
                 if (currentProjectProcess != null)
                 {
                     // Find the ProjectProcess with Sequence - 1
                     var previousProjectProcess = projectProcesses.FirstOrDefault(pp => pp.Sequence == currentProjectProcess.Sequence - 1);

                     // If we found the previous ProjectProcess, find the corresponding Process
                     if (previousProjectProcess != null)
                     {
                         var previousProcess = processes.FirstOrDefault(p => p.Id == previousProjectProcess.ProcessId);
                         return previousProcess?.Name;
                     }
                 }
             }

             return null;  // Return null if no previous process is found
         }*/




        private string GetPreviousProcess(List<ERPAPI.Model.Process> processes, int currentProcessId, List<ProjectProcess> projectProcesses, int ProjectId)
        {
            // Get the current process based on its Id
            var currentProcess = processes.FirstOrDefault(p => p.Id == currentProcessId);

            if (currentProcess == null)
            {
                Console.WriteLine($"Current process with ID {currentProcessId} not found.");
                return null; // Return null if the current process is not found
            }

            Console.WriteLine("1st Console: " + currentProcess.Name);

            // Special handling if ProcessId is 4
            if (currentProcessId == 4)
            {
                Console.WriteLine("14th Console: Special case for ProcessId 4");
                return processes.FirstOrDefault(p => p.Id == 2)?.Name;
            }

            // 1. If the current process is "Independent", the previous process is based on RangeStart
            if (currentProcess.ProcessType == "Independent")
            {
                var previousProcess = processes.FirstOrDefault(p => p.Id == currentProcess.RangeStart);
                if (previousProcess != null)
                {
                    Console.WriteLine("2nd Console: Previous process (Independent) " + previousProcess.Name);
                    return previousProcess.Name;
                }
                else
                {
                    Console.WriteLine($"2nd Console: No previous process found for RangeStart {currentProcess.RangeStart}");
                }
            }

            // 2. If the current process is "Dependent", check for a process with RangeEnd matching currentProcess.Id
            if (currentProcess.ProcessType == "Dependent")
            {
                // Find a process where RangeEnd matches the currentProcess.Id
                var dependentProcess = processes.FirstOrDefault(p => p.RangeEnd == currentProcess.Id);
                if (dependentProcess != null)
                {
                    Console.WriteLine("6th Console: Dependent process found: " + dependentProcess.Name);

                    var isdependentinprojectprocess = projectProcesses.Where(p => p.ProjectId == ProjectId).FirstOrDefault(pp => pp.ProcessId == dependentProcess.Id);
                    if (isdependentinprojectprocess != null)
                    {
                        var currentProjectProcess = projectProcesses.Where(p => p.ProjectId == ProjectId).FirstOrDefault(pp => pp.ProcessId == currentProcessId);
                        if (currentProjectProcess != null)
                        {
                            Console.WriteLine("7th Console: Current Project Process Sequence: " + currentProjectProcess.Sequence);
                            var previousProjectProcess = projectProcesses.Where(p => p.ProjectId == ProjectId)
                                .FirstOrDefault(pp => pp.Sequence == currentProjectProcess.Sequence - 1);

                            // Continue to traverse backward through the sequence until a dependent process is found
                            while (previousProjectProcess != null)
                            {
                                Console.WriteLine("8th Console: Checking previous Project Process Sequence: " + previousProjectProcess.Sequence);
                                var previousProcess = processes.FirstOrDefault(p => p.Id == previousProjectProcess.ProcessId);

                                // If a dependent process is found, stop and return it
                                if (previousProcess?.ProcessType == "Dependent")
                                {
                                    Console.WriteLine("3rd Console: Previous dependent process: " + previousProcess.Name);
                                    return previousProcess.Name;
                                }

                                // Otherwise, continue looking backward through the sequence
                                currentProjectProcess = previousProjectProcess;
                                previousProjectProcess = projectProcesses.Where(p => p.ProjectId == ProjectId)
                                    .FirstOrDefault(pp => pp.Sequence == currentProjectProcess.Sequence - 1);
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("6th Console: No dependent process found with RangeEnd matching currentProcess.Id.");
                }

                // If no dependent process is found, continue with the sequence logic
                var currentProjectProcessFallback = projectProcesses.Where(p => p.ProjectId == ProjectId).FirstOrDefault(pp => pp.ProcessId == currentProcessId);
                if (currentProjectProcessFallback != null)
                {
                    Console.WriteLine("41st Console: Current Project Process Sequence: " + currentProjectProcessFallback.Sequence);
                    var previousProjectProcess = projectProcesses.Where(p => p.ProjectId == ProjectId).FirstOrDefault(pp => pp.Sequence == currentProjectProcessFallback.Sequence - 1);
                    if (previousProjectProcess != null)
                    {
                        Console.WriteLine("42nd Console: Previous Project Process Sequence: " + previousProjectProcess.Sequence);
                        var previousProcess = processes.FirstOrDefault(p => p.Id == previousProjectProcess.ProcessId);
                        if (previousProcess != null)
                        {
                            Console.WriteLine("4th Console: Previous process (sequence fallback): " + previousProcess.Name);
                            return previousProcess.Name;
                        }
                        else
                        {
                            Console.WriteLine("4th Console: No process found for previous ProjectProcess.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("41st Console: No previous ProjectProcess with Sequence - 1 found.");
                    }
                }
                else
                {
                    Console.WriteLine("41st Console: No ProjectProcess found for ProcessId: " + currentProcessId);
                }
            }

            return null;  // Return null if no previous process is found
        }
        private bool DisplayExists(int id)
        {
            return _context.Displays.Any(e => e.DisplayId == id);
        }
    }
}

