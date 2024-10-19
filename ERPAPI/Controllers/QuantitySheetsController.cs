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
        if (projectType == "Booklet")
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
                        IsOverridden = sheet.IsOverridden,
                        ProcessId = sheet.ProcessId
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

        // Combine new sheets with existing sheets
        var allSheets = existingSheets.Concat(newSheets);

        // Group by lotNo
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
                _processService.ProcessCatch(sheet);
            }
        }

        // Add new sheets to the database
        await _context.QuantitySheets.AddRangeAsync(newSheets);
        await _context.SaveChangesAsync();

        return Ok(newSheets);
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


    [HttpGet]
    public async Task<ActionResult<IEnumerable<object>>> Get(int ProjectId, string lotNo)
    {

        return await _context.QuantitySheets.Where(r=> r.ProjectId == ProjectId && r.LotNo == lotNo).ToListAsync();
    }

    [HttpGet("Columns")]
    public IActionResult GetColumnNames()
    {
        var columnNames = typeof(QuantitySheet).GetProperties()
            .Where(prop => prop.Name != "QuantitySheetId" &&
                           prop.Name != "PercentageCatch" &&
                           prop.Name != "ProjectId" &&
                           prop.Name != "ProcessId" &&
                           prop.Name != "IsOverridden")
            .Select(prop => prop.Name)
            .ToList();

        return Ok(columnNames);
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
