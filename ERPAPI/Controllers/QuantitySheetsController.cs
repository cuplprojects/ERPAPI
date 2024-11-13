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
        var project = await _context.Projects
       .Where(p => p.ProjectId == projectId)
       .Select(p => new { p.TypeId, p.NoOfSeries })
       .FirstOrDefaultAsync();
        if (project == null)
        {
            return BadRequest("Project not found.");
        }

        var projectTypeId = project.TypeId;
        var projectType = await _context.Types
            .Where(t => t.TypeId == projectTypeId)
            .Select(t => t.Types)
            .FirstOrDefaultAsync();
        if(projectType == "Booklet" && project.NoOfSeries.HasValue)
        {
            var noOfSeries = project.NoOfSeries.Value;

            var adjustedSheets = new List<QuantitySheet>();
            foreach (var sheet in newSheets)
            {
                var adjustedQuantity = sheet.Quantity / 4;
                for (int i = 0; i < noOfSeries; i++)
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
                           prop.Name != "ProcessId" &&
                           prop.Name != "Status")
            .Select(prop => prop.Name)
            .ToList();

        return Ok(columnNames);
    }

    [HttpGet("Catch")]
    public async Task<ActionResult<IEnumerable<object>>> GetCatches(int ProjectId, string lotNo)
    {
        // Retrieve current lot data with necessary fields only
        var currentLotData = await _context.QuantitySheets
            .Where(r => r.ProjectId == ProjectId && r.LotNo == lotNo)
            .Select(r => new
            {
                r.QuantitySheetId,
                r.CatchNo,
                r.Paper,
                r.ExamDate,
                r.ExamTime,
                r.Course,
                r.Subject,
                r.InnerEnvelope,
                r.OuterEnvelope,
                r.LotNo,
                r.Quantity,
                r.PercentageCatch,
                r.ProjectId,
                r.ProcessId
            })
            .ToListAsync();

        if (!currentLotData.Any())
        {
            return Ok(new List<object>()); // Return empty if no data for current lot
        }

        // Retrieve previous lots' data with only necessary ExamDate field for overlap check, if not lot 1
        var previousExamDates = lotNo != "1"
            ? await _context.QuantitySheets
                .Where(r => r.ProjectId == ProjectId && r.LotNo != lotNo)
                .Select(r => r.ExamDate)
                .Distinct()
                .ToListAsync()
            : new List<string>(); // Empty list if lot 1

        // Define a function to convert Excel date to Indian format
        string ConvertToIndianDate(string examDateString)
        {
            if (DateTime.TryParse(examDateString, out var examDate))
            {
                return examDate.ToString("dd-MM-yyyy"); // Format date to Indian style
            }
            return "Invalid Date";
        }

        // Process each item in the current lot and check for overlap only if lotNo is not "1"
        var result = currentLotData.Select(current => new
        {
            current.QuantitySheetId,
            current.CatchNo,
            current.Paper,
            ExamDate = ConvertToIndianDate(current.ExamDate),
            current.ExamTime,
            current.Course,
            current.Subject,
            current.InnerEnvelope,
            current.OuterEnvelope,
            current.LotNo,
            current.Quantity,
            current.PercentageCatch,
            current.ProjectId,
            current.ProcessId,
            IsExamDateOverlapped = lotNo != "1" && previousExamDates.Contains(current.ExamDate)
        }).ToList();

        return Ok(result);
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

    // First, create a DTO to handle the transfer request
    public class CatchTransferRequest
    {
        public required int ProjectId { get; set; }
        public required string SourceLotNo { get; set; }
        public required string TargetLotNo { get; set; }
        public required List<int> CatchIds { get; set; }
    }

    [HttpPut("transfer-catches")]
    public async Task<IActionResult> TransferCatches([FromBody] CatchTransferRequest request)
    {
        try
        {
            // Validate request
            if (request == null || string.IsNullOrEmpty(request.SourceLotNo) ||
                string.IsNullOrEmpty(request.TargetLotNo) ||
                request.CatchIds == null || !request.CatchIds.Any())
            {
                return BadRequest("Invalid transfer request");
            }

            if (request.SourceLotNo == request.TargetLotNo)
            {
                return BadRequest("Source and target lots cannot be the same");
            }

            // First, retrieve the ProjectId and CatchNo for the first provided CatchId to infer the project and catch number.
            var initialCatch = await _context.QuantitySheets
                .Where(qs => qs.QuantitySheetId == request.CatchIds.First() && qs.LotNo == request.SourceLotNo)
                .Select(qs => new { qs.ProjectId, qs.CatchNo })
                .FirstOrDefaultAsync();

            if (initialCatch == null)
            {
                return NotFound("No valid catch found for the provided CatchIds in the source lot");
            }

            var projectId = initialCatch.ProjectId;
            var catchNo = initialCatch.CatchNo;

            // Retrieve all records with the same ProjectId and CatchNo in the source lot
            var catchesToTransfer = await _context.QuantitySheets
                .Where(qs => qs.ProjectId == projectId &&
                             qs.CatchNo == catchNo &&
                             qs.LotNo == request.SourceLotNo)
                .ToListAsync();

            if (!catchesToTransfer.Any())
            {
                return NotFound("No catches found to transfer");
            }

            using var transactionScope = await _context.Database.BeginTransactionAsync();
            try
            {
                // Update LotNo for all records in the source lot with matching ProjectId and CatchNo
                foreach (var catch_ in catchesToTransfer)
                {
                    catch_.LotNo = request.TargetLotNo;
                }

                // Save changes to persist the LotNo updates
                await _context.SaveChangesAsync();

                // Recalculate percentages for both lots
                await RecalculatePercentages(projectId, request.SourceLotNo);
                await RecalculatePercentages(projectId, request.TargetLotNo);

                // Save changes again after recalculating percentages
                await _context.SaveChangesAsync();

                await transactionScope.CommitAsync();

                return Ok(new
                {
                    Message = "Catches transferred successfully",
                    TransferredCatches = catchesToTransfer
                });
            }
            catch (Exception ex)
            {
                await transactionScope.RollbackAsync();
                return StatusCode(500, $"Failed to transfer catches: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to transfer catches: {ex.Message}");
        }
    }

    // Helper method to recalculate percentages for a lot
    private async Task RecalculatePercentages(int projectId, string lotNo)
    {
        var sheetsInLot = await _context.QuantitySheets
            .Where(s => s.ProjectId == projectId && s.LotNo == lotNo)
            .ToListAsync();

        double totalQuantityForLot = sheetsInLot.Sum(sheet => sheet.Quantity);

        if (totalQuantityForLot > 0)
        {
            foreach (var sheet in sheetsInLot)
            {
                sheet.PercentageCatch = (sheet.Quantity / totalQuantityForLot) * 100;
            }
        }
    }

}
