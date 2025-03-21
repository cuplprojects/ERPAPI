﻿using ERPAPI.Data;
using ERPAPI.Model;
using ERPAPI.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Net;

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

        /*   public async Task<IActionResult> ZonalDisplay(int id)
           {
               try
               {
                   var display = await _context.Displays.Where(p => p.DisplayId == id).ToListAsync();
                   var displayIds = display.SelectMany(d => d.Zones).ToList();
                   var runningcatch = await _context.Transaction.Where(x => displayIds.Contains(x.ZoneId) && x.Status == 1).ToListAsync();
                   var project = await _context.Projects.ToListAsync();
                   var process = await _context.Processes.ToListAsync();
                   var supervisor = await _context.ProjectProcesses.ToListAsync();
                   var machine = await _context.Machine.ToListAsync();
                   var quantitysheet = await _context.QuantitySheets.ToListAsync();
                   var zones = await _context.Zone.ToListAsync();
                   var user = await _context.Users.Where(u => u.RoleId == 5 || u.RoleId == 6).ToListAsync();

                   // Grouping transactions by ZoneId -> ProcessId -> ProjectId -> LotNo -> CatchNo
                   var groupedByZone = runningcatch
                       .GroupBy(x => x.ZoneId)
                       .Select(zoneGroup => new
                       {
                           ZoneId = zoneGroup.Key,
                           ZoneNo = zones.FirstOrDefault(z => z.ZoneId == zoneGroup.Key)?.ZoneNo,
                           Processes = zoneGroup
                               .GroupBy(x => x.ProcessId)
                               .Select(processGroup => new
                               {
                                   ProcessId = processGroup.Key,
                                   ProcessName = process.FirstOrDefault(p => p.Id == processGroup.Key)?.Name,
                                   Supervisor = string.Join(", ", user
                                       .Where(u => supervisor
                                           .Any(s => s.ProcessId == processGroup.Key && s.UserId.Contains(u.UserId)))
                                       .Select(u => u.FirstName + " " + u.LastName)),
                                   Projects = processGroup
                                       .GroupBy(x => x.ProjectId)
                                       .Select(projectGroup => new
                                       {
                                           ProjectId = projectGroup.Key,
                                           ProjectName = project.FirstOrDefault(p => p.ProjectId == projectGroup.Key)?.Name,
                                           Lots = projectGroup
                                               .GroupBy(x => x.LotNo)
                                               .Select(lotGroup => new
                                               {
                                                   LotNo = lotGroup.Key,
                                                   Catches = lotGroup
                                                       .Select(catchGroup => new
                                                       {
                                                           QuantitysheetId = catchGroup.QuantitysheetId,
                                                           // Get series letter based on QuantitySheetId
                                                           CatchNoWithSeries = quantitysheet
                                                               .Where(p => p.ProjectId == projectGroup.Key)
                                                               .Where(q => q.CatchNo == quantitysheet
                                                                   .FirstOrDefault(q => q.QuantitySheetId == catchGroup.QuantitysheetId)?.CatchNo)
                                                               .Select((q, index) => new
                                                               {
                                                                   CatchNoWithSeries = string.IsNullOrEmpty(project.FirstOrDefault(p => p.ProjectId == projectGroup.Key)?.SeriesName)
                                                       ? q.CatchNo // If SeriesName is empty, just use CatchNo
                                                       : $"{q.CatchNo}-{project.FirstOrDefault(p => p.ProjectId == projectGroup.Key)?.SeriesName?.ToCharArray().ElementAtOrDefault(index)}", // Otherwise, append series


                                                                   q.QuantitySheetId
                                                               })
                                                               .FirstOrDefault(q => q.QuantitySheetId == catchGroup.QuantitysheetId)?.CatchNoWithSeries, // Match based on QuantitySheetId

                                                           // Fetch the actual CatchNo
                                                           CatchNo = quantitysheet
                                                               .Where(p => p.ProjectId == projectGroup.Key)
                                                               .FirstOrDefault(q => q.QuantitySheetId == catchGroup.QuantitysheetId)?.CatchNo,

                                                           Quantity = (processGroup.Key != 15 && processGroup.Key != 1 && processGroup.Key != 2 && processGroup.Key != 3 && processGroup.Key != 4)
                                                               ? quantitysheet
                                                                   .Where(q => q.CatchNo == quantitysheet
                                                                   .Where(c => c.QuantitySheetId == catchGroup.QuantitysheetId)
                                                                   .Select(c => c.CatchNo)
                                                                   .FirstOrDefault())
                                                                   .Sum(q => q.Quantity)
                                                               : quantitysheet
                                                                   .Where(p => p.ProjectId == projectGroup.Key)
                                                                   .FirstOrDefault(q => q.QuantitySheetId == catchGroup.QuantitysheetId)?.Quantity,

                                                           Machine = machine.FirstOrDefault(m => m.MachineId == catchGroup.MachineId)?.MachineName,
                                                           Team = string.Join(", ", user
                                                               .Where(u => catchGroup.TeamId.Contains(u.UserId))
                                                               .Select(u => u.FirstName + " " + u.LastName)),
                                                           PreviousProcess = GetPreviousProcess(process, catchGroup.ProcessId, supervisor, catchGroup.ProjectId),
                                                       })
                                                       .ToList()
                                               }).ToList()
                                       }).ToList()
                               }).ToList()
                       }).ToList();







                   if (groupedByZone == null || !groupedByZone.Any())
                   {
                       return NotFound();
                   }

                   return Ok(groupedByZone);
               }
               catch (Exception ex)
               {
                   return StatusCode(500, "Internal server error");
               }
           }*/


        [HttpGet("ZonalDisplay")]
        public async Task ZonalDisplaySse(int id)
        {
            try
            {
                // Fetch all necessary data upfront
                var display = await _context.Displays.Where(p => p.DisplayId == id).ToListAsync();
                var displayIds = display.SelectMany(d => d.Zones).ToList();
                var zones = await _context.Zone.ToListAsync();
                var runningcatch = await _context.Transaction.Where(x => displayIds.Contains(x.ZoneId) && x.Status == 1).ToListAsync();
                var project = await _context.Projects.ToListAsync();
                var process = await _context.Processes.ToListAsync();
                var supervisor = await _context.ProjectProcesses.ToListAsync();
                var machine = await _context.Machine.ToListAsync();
                var quantitysheet = await _context.QuantitySheets.ToListAsync();
                var user = await _context.Users.Where(u => u.RoleId == 5 || u.RoleId == 6).ToListAsync();

                // Streaming response with SSE: start sending data
                Response.ContentType = "text/event-stream";
                Response.Headers.Add("Cache-Control", "no-cache");
                Response.Headers.Add("Connection", "keep-alive");

                // Group by zone and process the data one by one
                var zoneGroups = runningcatch
                    .GroupBy(x => x.ZoneId);

                foreach (var zoneGroup in zoneGroups)
                {
                    var zoneId = zoneGroup.Key;
                    var zone = zones.FirstOrDefault(z => z.ZoneId == zoneId);

                    if (zone == null) continue;  // If zone is not found, skip it

                    // Group by process for this specific zone
                    var processesInZone = zoneGroup
                        .GroupBy(x => x.ProcessId)
                        .Select(processGroup => new
                        {
                            ProcessId = processGroup.Key,
                            ProcessName = process.FirstOrDefault(p => p.Id == processGroup.Key)?.Name,
                            Supervisor = string.Join(", ", user
                                .Where(u => supervisor
                                    .Any(s => s.ProcessId == processGroup.Key && s.UserId.Contains(u.UserId)))
                                .Select(u => u.FirstName + " " + u.LastName)),
                            Projects = processGroup
                                .GroupBy(x => x.ProjectId)
                                .Select(projectGroup => new
                                {
                                    ProjectId = projectGroup.Key,
                                    ProjectName = project.FirstOrDefault(p => p.ProjectId == projectGroup.Key)?.Name,
                                    Lots = projectGroup
                                        .GroupBy(x => x.LotNo)
                                        .Select(lotGroup => new
                                        {
                                            LotNo = lotGroup.Key,
                                            Catches = lotGroup
                                              .Select(catchGroup => new
                                              {
                                                  QuantitysheetId = catchGroup.QuantitysheetId,
                                                  CatchNoWithSeries = quantitysheet
                                                      .Where(p => p.ProjectId == projectGroup.Key)
                                                      .Where(q => q.CatchNo == quantitysheet
                                                          .FirstOrDefault(q => q.QuantitySheetId == catchGroup.QuantitysheetId)?.CatchNo)
                                                      .Select((q, index) => new
                                                      {
                                                          CatchNoWithSeries = string.IsNullOrEmpty(project.FirstOrDefault(p => p.ProjectId == projectGroup.Key)?.SeriesName)
                                                              ? q.CatchNo // If SeriesName is empty, just use CatchNo
                                                              : $"{q.CatchNo}-{project.FirstOrDefault(p => p.ProjectId == projectGroup.Key)?.SeriesName?.ToCharArray().ElementAtOrDefault(index)}", // Otherwise, append series
                                                          q.QuantitySheetId
                                                      })
                                                      .FirstOrDefault(q => q.QuantitySheetId == catchGroup.QuantitysheetId)?.CatchNoWithSeries,
                                                  CatchNo = quantitysheet
                                                      .Where(p => p.ProjectId == projectGroup.Key)
                                                      .FirstOrDefault(q => q.QuantitySheetId == catchGroup.QuantitysheetId)?.CatchNo,
                                                  Quantity = (processGroup.Key != 15 && processGroup.Key != 1 && processGroup.Key != 2 && processGroup.Key != 3 && processGroup.Key != 4)
                                                      ? quantitysheet
                                                          .Where(q => q.CatchNo == quantitysheet
                                                          .Where(c => c.QuantitySheetId == catchGroup.QuantitysheetId)
                                                          .Select(c => c.CatchNo)
                                                          .FirstOrDefault())
                                                          .Sum(q => q.Quantity)
                                                      : quantitysheet
                                                          .Where(p => p.ProjectId == projectGroup.Key)
                                                          .FirstOrDefault(q => q.QuantitySheetId == catchGroup.QuantitysheetId)?.Quantity,
                                                  Machine = machine.FirstOrDefault(m => m.MachineId == catchGroup.MachineId)?.MachineName,
                                                  Team = string.Join(", ", user
                                                      .Where(u => catchGroup.TeamId.Contains(u.UserId))
                                                      .Select(u => u.FirstName + " " + u.LastName)),
                                                  PreviousProcess = GetPreviousProcess(process, catchGroup.ProcessId, supervisor, catchGroup.ProjectId),
                                              })
                                            .ToList()
                                        }).ToList()
                                }).ToList()
                        }).ToList();

                    // Serialize the zone data
                    var jsonData = JsonConvert.SerializeObject(new { ZoneId = zone.ZoneId, ZoneNo = zone.ZoneNo, Processes = processesInZone });

                    // Write to the response immediately
                    await Response.WriteAsync($"data: {jsonData}\n\n");
                    await Response.Body.FlushAsync();

                    // Wait for a while before sending the next zone (10 seconds)
                    await Task.Delay(10000);  // 10 seconds
                }
            }
            catch (Exception ex)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                await Response.WriteAsync("Internal server error");
            }
        }


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

