using ERPAPI.Data;
using ERPAPI.Model;
using ERPAPI.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

[ApiController]
[Route("api/[controller]")]
public class QuantitySheetController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ProcessService _processService;

    public QuantitySheetController(AppDbContext context, ProcessService processService)
    {
        _context = context;
        _processService = processService;
    }

    // POST api/quantitysheet
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] List<QuantitySheet> newSheets)
    {
        if (newSheets == null || !newSheets.Any())
        {
            return BadRequest("No data provided.");
        }

        var projectId = newSheets.First().ProjectId;
        var projectTypeId = await _context.Projects
            .Where(p => p.ProjectId == projectId)
            .Select(p => p.TypeId)
            .FirstOrDefaultAsync();

        var projectType = await _context.Types
            .Where(t => t.TypeId == projectTypeId)
            .Select(t => t.Types)
            .FirstOrDefaultAsync();

        // If project type is Booklet, adjust quantities and duplicate entries
        if (projectType == "Booklets")
        {
            var adjustedSheets = new List<QuantitySheet>();
            foreach (var sheet in newSheets)
            {
                var adjustedQuantity = sheet.Quantity / 4;
                for (int i = 0; i < 4; i++)
                {
                    var newSheet = new QuantitySheet
                    {
                        CatchNo = sheet.CatchNo,
                        Paper = sheet.Paper,
                        Course = sheet.Course,
                        Subject = sheet.Subject,
                        InnerEnvelope = sheet.InnerEnvelope,
                        OuterEnvelope = sheet.OuterEnvelope,
                        LotNo = sheet.LotNo,
                        Quantity = adjustedQuantity,
                        PercentageCatch = 0, // This will be recalculated below
                        ProjectId = sheet.ProjectId,

                        ExamDate = sheet.ExamDate,
                        ExamTime = sheet.ExamTime,

                        ProcessId = new List<int>() // Start with an empty list for the new catch
                    };
                    adjustedSheets.Add(newSheet);
                }
            }
            newSheets = adjustedSheets;
        }

        // Get existing sheets for the same project and lots
        var existingSheets = await _context.QuantitySheets
        .Where(s => s.ProjectId == projectId && newSheets.Select(ns => ns.LotNo).Contains(s.LotNo))
        .ToListAsync();

        // Prepare a list to track new catches that need to be processed
        var processedNewSheets = new List<QuantitySheet>();

        foreach (var sheet in newSheets)
        {
            // For new sheets, clear the ProcessId and process it
            sheet.ProcessId.Clear();
            _processService.ProcessCatch(sheet);
            processedNewSheets.Add(sheet);
        }

        // Combine new sheets with existing sheets
        var allSheets = existingSheets.Concat(processedNewSheets).ToList();

        // Group by LotNo to recalculate quantities and percentages
        var groupedSheets = allSheets.GroupBy(sheet => sheet.LotNo);

        foreach (var group in groupedSheets)
        {
            double totalQuantityForLot = group.Sum(sheet => sheet.Quantity);

            if (totalQuantityForLot == 0)
            {
                return BadRequest($"Total quantity for lot {group.Key} is zero, cannot calculate percentages.");
            }

            // Calculate percentage catch for each sheet in the current group
            foreach (var sheet in group)
            {
                sheet.PercentageCatch = (sheet.Quantity / totalQuantityForLot) * 100;

                // For existing sheets, just update the percentage
                if (!processedNewSheets.Contains(sheet))
                {
                    // No need to call ProcessCatch again for existing sheets
                    continue;
                }
            }
        }

        // Add new sheets to the database
        await _context.QuantitySheets.AddRangeAsync(processedNewSheets);
        await _context.SaveChangesAsync();

        return Ok(processedNewSheets);
    }


    [HttpPut("{id}")]
    public async Task<IActionResult> PutQuantitySheet(int id, QuantitySheet quantity)
    {
        if (id != quantity.QuantitySheetId)
        {
            return BadRequest();
        }

        _context.Entry(quantity).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!QuantitySheetExists(id))
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

    [HttpGet("Lots")]
    public async Task<ActionResult<IEnumerable<string>>> GetLots(int ProjectId)
    {
        var uniqueLotNumbers = await _context.QuantitySheets
            .Where(r => r.ProjectId == ProjectId)
            .Select(r => r.LotNo) // Select the LotNo
            .Distinct() // Get unique LotNo values
            .ToListAsync();

        return Ok(uniqueLotNumbers);
    }

    [HttpGet("Columns")]
    public IActionResult GetColumnNames()
    {
        var columnNames = typeof(QuantitySheet).GetProperties()
            .Where(prop => prop.Name != "QuantitySheetId" &&
                           prop.Name != "PercentageCatch" &&
                           prop.Name != "ProjectId" &&
                           prop.Name != "ProcessId")
            .Select(prop => prop.Name)
            .ToList();

        return Ok(columnNames);
    }

    [HttpGet("Catch")]
    public async Task<ActionResult<IEnumerable<object>>> GetCatches(int ProjectId, string lotNo)
    {

        return await _context.QuantitySheets.Where(r => r.ProjectId == ProjectId && r.LotNo == lotNo).ToListAsync();
    }





    [HttpGet("CatchByproject")]
    public async Task<ActionResult<IEnumerable<object>>> CatchByproject(int ProjectId)
    {

        return await _context.QuantitySheets.Where(r => r.ProjectId == ProjectId).ToListAsync();
    }



    [HttpGet("check-all-quantity-sheets")]
    public async Task<ActionResult<IEnumerable<object>>> GetAllProjectsQuantitySheetStatus()
    {
        // Get all projects from the database
        var projects = await _context.Projects.ToListAsync();

        var result = new List<object>();

        foreach (var project in projects)
        {
            var hasQuantitySheet = await _context.QuantitySheets
                .AnyAsync(s => s.ProjectId == project.ProjectId);

            result.Add(new
            {
                projectId = project.ProjectId,
                quantitySheet = hasQuantitySheet
            });
        }

        return Ok(result);
    }


    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteQuantitysheet(int id)
    {
        var sheetToDelete = await _context.QuantitySheets.FindAsync(id);
        if (sheetToDelete == null)
        {
            return NotFound();
        }

        var projectId = sheetToDelete.ProjectId;
        var lotNo = sheetToDelete.LotNo;

        _context.QuantitySheets.Remove(sheetToDelete);
        await _context.SaveChangesAsync();

        // After deletion, recalculate percentages for remaining sheets in the same project and lot
        var remainingSheets = await _context.QuantitySheets
            .Where(s => s.ProjectId == projectId && s.LotNo == lotNo)
            .ToListAsync();

        double totalQuantityForLot = remainingSheets.Sum(sheet => sheet.Quantity);

        if (totalQuantityForLot > 0)
        {
            foreach (var sheet in remainingSheets)
            {
                sheet.PercentageCatch = (sheet.Quantity / totalQuantityForLot) * 100;
            }

            // Save changes to update the percentages
            await _context.SaveChangesAsync();
        }

        return NoContent();
    }

    private bool QuantitySheetExists(int id)
    {
        return _context.QuantitySheets.Any(e => e.QuantitySheetId == id);
    }

}
