using ERPAPI.Data;
using ERPAPI.Model;
using ERPAPI.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using NuGet.Protocol.Plugins;

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
        if (projectType == "Booklet" && project.NoOfSeries.HasValue)
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
                        Status = sheet.Status,

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

    [HttpPost("ReleaseForProduction")]
    public async Task<IActionResult> ReleaseForProduction([FromBody] LotRequest request)
    {
        if (string.IsNullOrEmpty(request?.LotNo))
        {
            return BadRequest("Invalid lot number.");
        }

        // Find all records that belong to the given lot
        var quantitySheets = await _context.QuantitySheets
            .Where(q => q.LotNo == request.LotNo)
            .ToListAsync();

        if (quantitySheets == null || quantitySheets.Count == 0)
        {
            return NotFound($"No records found for Lot No: {request.LotNo}");
        }

        // Update the status to 1 (released for production)
        foreach (var sheet in quantitySheets)
        {
            sheet.Status = 1;
        }

        // Save changes to the database
        await _context.SaveChangesAsync();

        return Ok($"Lot {request.LotNo} has been released for production.");
    }

    public class LotRequest
    {
        public string LotNo { get; set; }
    }





    [HttpGet("calculate-date-range")]
    public async Task<IActionResult> CalculateDateRange([FromQuery] string selectedLot, [FromQuery] int projectId)
    {
        // Validate input parameters
        if (string.IsNullOrEmpty(selectedLot))
        {
            return BadRequest("Selected lot is required.");
        }

        try
        {
            // Fetch the unique list of lots for the given project
            var lots = await _context.QuantitySheets
                .Where(l => l.ProjectId == projectId)
                .Select(l => l.LotNo)
                .Distinct()
                .ToListAsync();

            // Validate that there are lots available for the given project
            if (lots == null || !lots.Any())
            {
                return NotFound("No lots found for the specified project.");
            }

            // Sort the lots to ensure they're in order
            var sortedLots = lots.Select(lot => lot.Trim()).OrderBy(lot => lot).ToList();

            // Ensure the selected lot is valid
            if (!sortedLots.Contains(selectedLot))
            {
                return NotFound("Selected lot not found in the available lots.");
            }

            int selectedLotIndex = sortedLots.IndexOf(selectedLot);
            bool isFirstLot = selectedLotIndex == 0;
            bool isLastLot = selectedLotIndex == sortedLots.Count - 1;

            DateTime? startDate = null;
            DateTime? endDate = null;

            // Fetch dates for the previous and next lots
            List<DateTime> previousLotDates = null;
            List<DateTime> nextLotDates = null;

            // Fetch dates for previous lot if not the first lot
            if (!isFirstLot)
            {
                previousLotDates = await _context.QuantitySheets
                    .Where(l => l.ProjectId == projectId && l.LotNo == sortedLots[selectedLotIndex - 1])
                    .Select(l => DateTime.Parse(l.ExamDate))  // Parse ExamDate to DateTime
                    .ToListAsync();
            }

            // Fetch dates for next lot if not the last lot
            if (!isLastLot)
            {
                nextLotDates = await _context.QuantitySheets
                    .Where(l => l.ProjectId == projectId && l.LotNo == sortedLots[selectedLotIndex + 1])
                    .Select(l => DateTime.Parse(l.ExamDate))  // Parse ExamDate to DateTime
                    .ToListAsync();
            }

            // Logic based on the position of the selected lot
            if (isFirstLot)
            {
                // If first lot, startDate is today and endDate is the min date of the next lot
                startDate = DateTime.Today;
                if (nextLotDates != null && nextLotDates.Any())
                {
                    endDate = nextLotDates.Min().AddDays(-1);  // endDate is one day before the minimum date of the next lot
                }
            }
            else if (isLastLot)
            {
                // If last lot, startDate is the max date of the previous lot, and endDate can be any date after startDate
                if (previousLotDates != null && previousLotDates.Any())
                {
                    startDate = previousLotDates.Max();
                }

                // Set endDate to a date after startDate (e.g., 3 months after startDate)
                if (startDate.HasValue)
                {
                    endDate = startDate.Value.AddMonths(3);
                }
            }
            else
            {
                // If the selected lot is somewhere in between, startDate is the max date of the previous lot, and endDate is the min date of the next lot
                if (previousLotDates != null && previousLotDates.Any())
                {
                    startDate = previousLotDates.Max();
                }

                if (nextLotDates != null && nextLotDates.Any())
                {
                    endDate = nextLotDates.Min().AddDays(-1);  // endDate is one day before the minimum date of the next lot
                }
            }

            // If no valid date range, return an error
            if (!startDate.HasValue || !endDate.HasValue)
            {
                return BadRequest("Unable to calculate a valid date range.");
            }

            // Return the calculated date range
            return Ok(new
            {
                startDate = startDate.Value.ToString("yyyy-MM-dd"),
                endDate = endDate.Value.ToString("yyyy-MM-dd"),
                isFirstLot,
                isLastLot
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Internal server error: {ex.Message}");
        }
    }


    [HttpGet("lot-dates")]
    public async Task<ActionResult<Dictionary<string, object>>> GetLotDates(int projectId)
    {
        try
        {
            // Fetch all distinct exam dates grouped by LotNo for the given project
            var lotwiseExamDates = await _context.QuantitySheets
                .Where(qs => qs.ProjectId == projectId && !string.IsNullOrEmpty(qs.ExamDate))
                .GroupBy(qs => qs.LotNo)
                .Select(group => new
                {
                    LotNo = group.Key,
                    ExamDates = group.Select(qs => qs.ExamDate).ToList() // Get all dates for the group
                })
                .ToListAsync();

            if (!lotwiseExamDates.Any())
            {
                return NotFound($"No exam dates found for project {projectId}");
            }

            // Process the exam dates and calculate Min and Max per lot
            var result = new Dictionary<string, object>();
            foreach (var lot in lotwiseExamDates)
            {
                var parsedDates = lot.ExamDates
                    .Select(date => DateTime.TryParse(date, out var parsedDate) ? parsedDate : (DateTime?)null)
                    .Where(date => date.HasValue)
                    .Select(date => date.Value)
                    .ToList();

                if (parsedDates.Any())
                {
                    var minDate = parsedDates.Min();
                    var maxDate = parsedDates.Max();

                    result[lot.LotNo] = new { MinDate = minDate.ToString("dd-MM-yyyy"), MaxDate = maxDate.ToString("dd-MM-yyyy") };
                }
                else
                {
                    result[lot.LotNo] = new { MinDate = (string)null, MaxDate = (string)null };
                }
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Failed to retrieve exam dates: {ex.Message}");
        }
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

    [HttpPut]
    public async Task<IActionResult> UpdateQuantitySheet([FromBody] List<QuantitySheet> newSheets)
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

        // Adjust for "Booklet" type project if necessary
        if (projectType == "Booklet" && project.NoOfSeries.HasValue)
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
                        PercentageCatch = 0, // Will be recalculated later
                        ProjectId = sheet.ProjectId,
                        ExamDate = sheet.ExamDate,
                        ExamTime = sheet.ExamTime,
                        ProcessId = new List<int>() // Empty list for new catch
                    };
                    adjustedSheets.Add(newSheet);
                }
            }

            newSheets = adjustedSheets; // Replace with adjusted sheets
        }

        // Get existing sheets for the same projectId
        var existingSheets = await _context.QuantitySheets
            .Where(s => s.ProjectId == projectId)
            .ToListAsync();

        // Prepare a list to track new sheets that need to be processed
        var processedNewSheets = new List<QuantitySheet>();

        foreach (var sheet in newSheets)
        {
            // For new sheets, clear the ProcessId and process it
            sheet.ProcessId.Clear();
            _processService.ProcessCatch(sheet);
            processedNewSheets.Add(sheet);
        }

        // Now handle inserting or updating the QuantitySheets based on ProjectId
        foreach (var newSheet in processedNewSheets)
        {
            var existingSheet = existingSheets
                .FirstOrDefault(s => s.LotNo == newSheet.LotNo && s.ProjectId == newSheet.ProjectId);

            if (existingSheet != null)
            {
                // Update the existing sheet
                existingSheet.CatchNo = newSheet.CatchNo;
                existingSheet.Paper = newSheet.Paper;
                existingSheet.Course = newSheet.Course;
                existingSheet.Subject = newSheet.Subject;
                existingSheet.InnerEnvelope = newSheet.InnerEnvelope;
                existingSheet.OuterEnvelope = newSheet.OuterEnvelope;
                existingSheet.Quantity = newSheet.Quantity;
                existingSheet.PercentageCatch = newSheet.PercentageCatch;
                existingSheet.ExamDate = newSheet.ExamDate;
                existingSheet.ExamTime = newSheet.ExamTime;
                existingSheet.ProcessId = newSheet.ProcessId;
            }
            else
            {
                // If no existing sheet found, add it to the context for insertion
                _context.QuantitySheets.Add(newSheet);
            }
        }

        // Recalculate the percentages and save
        var allSheets = existingSheets.Concat(processedNewSheets).ToList();
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
            }
        }

        await _context.SaveChangesAsync();

        return Ok(processedNewSheets);
    }

    [HttpGet("Lots")]
    public async Task<ActionResult<IEnumerable<string>>> GetLots(int ProjectId)
    {
        // Fetch the data from the database first
        var uniqueLotNumbers = await _context.QuantitySheets
            .Where(r => r.ProjectId == ProjectId)
            .Select(r => r.LotNo) // Select the LotNo
            .Distinct() // Get unique LotNo values
            .ToListAsync(); // Bring the data into memory

        // Sort the LotNo values by parsing them as integers
        var sortedLotNumbers = uniqueLotNumbers
            .Where(lotNo => int.TryParse(lotNo, out _)) // Filter out non-numeric LotNo values
            .OrderBy(lotNo => int.Parse(lotNo)) // Order by LotNo as integers
            .ToList();

        return Ok(sortedLotNumbers);
    }




    [HttpGet("ReleasedLots")]
    public async Task<ActionResult<IEnumerable<string>>> GetReleasedLots(int ProjectId)
    {
        var uniqueLotNumbers = await _context.QuantitySheets
            .Where(r => r.ProjectId == ProjectId && r.Status == 1)
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
        public string? NewExamDate { get; set; }  // Optional exam date in dd-MM-yyyy format
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
                foreach (var catch_ in catchesToTransfer)
                {
                    catch_.LotNo = request.TargetLotNo;
                    
                    // Update exam date if provided
                    if (!string.IsNullOrEmpty(request.NewExamDate))
                    {
                        if (DateTime.TryParseExact(request.NewExamDate, "dd-MM-yyyy", 
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
                        {
                            catch_.ExamDate = parsedDate.ToString("yyyy-MM-dd"); // Store in database format
                        }
                        else
                        {
                            return BadRequest("Invalid exam date format. Please use dd-MM-yyyy");
                        }
                    }
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
    // Get Exam Dates for a given project and lot
    [HttpGet("exam-dates")]
    public async Task<ActionResult<IEnumerable<string>>> GetExamDates(int projectId, string lotNo)
    {
        var examDates = await _context.QuantitySheets
            .Where(qs => qs.ProjectId == projectId && qs.LotNo == lotNo)
            .Select(qs => qs.ExamDate)
            .Distinct()
            .ToListAsync();

        if (!examDates.Any())
        {
            return NotFound($"No exam dates found for project {projectId} and lot {lotNo}");
        }

        // Convert dates to Indian format
        var formattedDates = examDates
            .Where(date => !string.IsNullOrEmpty(date))
            .Select(date => DateTime.TryParse(date, out var parsedDate) 
                ? parsedDate.ToString("dd-MM-yyyy") 
                : date)
            .ToList();

        return Ok(formattedDates);
    }

    // Get Lot Data for a given project and lot
    [HttpGet("lot-data")]
    public async Task<ActionResult<IEnumerable<QuantitySheet>>> GetLotData(int projectId, string lotNo)
    {
        var lotData = await _context.QuantitySheets
            .Where(qs => qs.ProjectId == projectId && qs.LotNo == lotNo)
            .ToListAsync();

        if (!lotData.Any())
        {
            return NotFound($"No data found for project {projectId} and lot {lotNo}");
        }

        return Ok(lotData);
    }


    // Get Catch Data for a given project, lot, and catch
    [HttpGet("catch-data")]
    public async Task<ActionResult<IEnumerable<QuantitySheet>>> GetCatchData(int projectId, string lotNo, string catchNo)
    {
        var catchData = await _context.QuantitySheets
            .Where(qs => qs.ProjectId == projectId 
                      && qs.LotNo == lotNo 
                      && qs.CatchNo == catchNo)
            .ToListAsync();

        if (!catchData.Any())
        {
            return NotFound($"No data found for project {projectId}, lot {lotNo}, catch {catchNo}");
        }

        return Ok(catchData);
    }

    [HttpDelete("DeleteByProjectId/{projectId}")]
    public async Task<IActionResult> DeleteByProjectId(int projectId)
    {
        // Find all quantity sheets for the given projectId
        var sheetsToDelete = await _context.QuantitySheets
            .Where(s => s.ProjectId == projectId)
            .ToListAsync();

        if (sheetsToDelete == null || !sheetsToDelete.Any())
        {
            return NotFound($"No quantity sheets found for Project ID: {projectId}");
        }

        // Remove the sheets from the context
        _context.QuantitySheets.RemoveRange(sheetsToDelete);
        await _context.SaveChangesAsync();

        return NoContent(); // Return 204 No Content on successful deletion
    }

}
