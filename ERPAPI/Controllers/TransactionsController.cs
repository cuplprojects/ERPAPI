using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ERPAPI.Data;
using ERPAPI.Model;
using Microsoft.CodeAnalysis;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TransactionsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Transactions
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetTransaction(int projectId, int processId)
        {
            var transactionsWithAlarms = await (from t in _context.Transaction
                                                join a in _context.Alarm on t.AlarmId equals a.AlarmId into alarmGroup
                                                from a in alarmGroup.DefaultIfEmpty() // Left join
                                                where t.ProjectId == projectId && t.ProcessId == processId
                                                select new
                                                {
                                                    t.TransactionId,
                                                    t.AlarmId,
                                                    t.ZoneId,
                                                    t.QuantitysheetId,
                                                    t.TeamId,
                                                    t.Remarks,
                                                    t.LotNo,
                                                    t.InterimQuantity,
                                                    t.ProcessId,
                                                    t.Status,
                                                    AlarmMessage = a != null ? a.Message : null // Handle null case for alarms
                                                }).ToListAsync();

            if (transactionsWithAlarms == null || !transactionsWithAlarms.Any())
            {
                return NotFound(); // Return a 404 if no transactions are found
            }

            return Ok(transactionsWithAlarms);
        }



        // GET: api/Transactions/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Transaction>> GetTransaction(int id)
        {
            var transaction = await _context.Transaction.FindAsync(id);

            if (transaction == null)
            {
                return NotFound();
            }

            return transaction;
        }

        // PUT: api/Transactions/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTransaction(int id, Transaction transaction)
        {
            if (id != transaction.TransactionId)
            {
                return BadRequest();
            }

            _context.Entry(transaction).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TransactionExists(id))
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

        // POST: api/Transactions
        [HttpPost]
        public async Task<ActionResult<Transaction>> PostTransaction(Transaction transaction)
        {
            _context.Transaction.Add(transaction);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetTransaction", new { id = transaction.TransactionId }, transaction);
        }

        // DELETE: api/Transactions/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTransaction(int id)
        {
            var transaction = await _context.Transaction.FindAsync(id);
            if (transaction == null)
            {
                return NotFound();
            }

            _context.Transaction.Remove(transaction);
            await _context.SaveChangesAsync();

            return NoContent();
        }
        private bool TransactionExists(int id)
        {
            return _context.Transaction.Any(e => e.TransactionId == id);
        }

        // GET: api/Transactions/percentages
        [HttpGet("percentages")]
        public async Task<ActionResult<Percentages>> GetPercentages(int ProjectId)
        {
            var processes = await _context.ProjectProcesses.Where(p => p.ProjectId == ProjectId).ToListAsync();
            var quantitySheets = await _context.QuantitySheets.Where(p => p.ProjectId == ProjectId).ToListAsync();
            var transactions = await _context.Transaction.Where(p => p.ProjectId == ProjectId).ToListAsync();

            var result = CalculatePercentages(processes, quantitySheets, transactions);
            return Ok(result);
        }



        private Percentages CalculatePercentages(
     List<ProjectProcess> processes,
     List<QuantitySheet> quantitySheets,
     List<Transaction> transactions)
        {
            if (processes == null || quantitySheets == null || transactions == null)
            {
                throw new ArgumentNullException("One or more input lists are null.");
            }

            var completedProcesses = transactions.Where(t => t.Status == 3).ToList();
            var partiallyCompletedProcesses = transactions.Where(t => t.Status == 2).ToList();

            var sheetPercentages = new List<SheetPercentage>();
            var lotPercentages = new Dictionary<string, double>();
            var lotCatchPercentages = new Dictionary<string, double>();

            // Group quantity sheets by LotNo
            var sheetsGroupedByLotNo = quantitySheets.GroupBy(sheet => sheet.LotNo);

            foreach (var group in sheetsGroupedByLotNo)
            {
                double lotTotalWeightage = 0;
                double lotTotalCatchPercent = 0;
                double totalLotPercent = 0; // For this specific lot

                foreach (var sheet in group)
                {

                    double completedProcessWeightage = 0;
                    double partiallyCompletedWeightage = 0;

                    // Analyze processes
                    foreach (var process in processes)
                    {
                        var completedCount = completedProcesses.Count(t => t.QuantitysheetId == sheet.QuantitySheetId && t.ProcessId == process.ProcessId);
                        var partiallyCount = partiallyCompletedProcesses.Count(t => t.QuantitysheetId == sheet.QuantitySheetId && t.ProcessId == process.ProcessId);

                        if (completedCount > 0)
                        {
                            completedProcessWeightage += process.Weightage;
                        }
                        if (partiallyCount > 0)
                        {
                            partiallyCompletedWeightage += process.Weightage;
                        }
                    }

                    var partiallyCompletedQty = partiallyCompletedProcesses
                        .Where(t => t.QuantitysheetId == sheet.QuantitySheetId)
                        .Sum(t => t.InterimQuantity);

                    var totalWeightage = completedProcessWeightage +
                        (partiallyCompletedWeightage * partiallyCompletedQty / Math.Max(sheet.Quantity, 1));

                    // Calculate Lot Percent
                    double lotPercent = sheet.PercentageCatch * totalWeightage / 100;
                    totalLotPercent += lotPercent; // Accumulate lot percent for this lot
                    lotTotalWeightage += totalWeightage; // Accumulate total weightage for the lot

                    // Calculate Catch Percent
                    double catchPercent = processes
                        .Where(p => completedProcesses.Any(t => t.QuantitysheetId == sheet.QuantitySheetId && t.ProcessId == p.ProcessId))
                        .Sum(p => p.Weightage);


                    foreach (var transaction in partiallyCompletedProcesses
                        .Where(t => t.QuantitysheetId == sheet.QuantitySheetId))
                    {
                        var processWeightage = processes.FirstOrDefault(p => p.ProcessId == transaction.ProcessId)?.Weightage ?? 0;

                        if (processWeightage > 0)
                        {
                            catchPercent += (processWeightage * transaction.InterimQuantity) / Math.Max(sheet.Quantity, 1);
                        }
                    }

                    var totalProcessWeightage = processes.Sum(p => p.Weightage);
                    double catchPercentNormalized = totalProcessWeightage > 0 ? (catchPercent / totalProcessWeightage) * 100 : 0;
                    lotTotalCatchPercent += catchPercentNormalized; // Accumulate catch percent for the lot

                    sheetPercentages.Add(new SheetPercentage
                    {
                        QuantitySheetId = sheet.QuantitySheetId,
                        LotPercent = lotPercent,
                        CatchPercent = catchPercentNormalized
                    });
                }

                // Store the total lot percentage for this specific lot
                if (group.Key != null)
                {
                    lotPercentages[group.Key] = totalLotPercent; // Store cumulative lot percent
                    lotCatchPercentages[group.Key] = lotTotalCatchPercent; // Store cumulative catch percent for the lot
                }
            }

            // Return the final Percentages object with detailed lot percentages
            return new Percentages
            {
                LotPercent = lotPercentages, // Return individual lot percentages
                ProjectPercent = lotPercentages.Sum(l => l.Value), // Calculate total project percent from individual lot percentages
                SheetPercentages = sheetPercentages
            };
        }

    }



    public class Percentages
    {
        public Dictionary<string, double> LotPercent { get; set; }
        public List<SheetPercentage> SheetPercentages { get; set; }
        public double ProjectPercent { get; set; }
    }

    public class SheetPercentage
    {
        public int QuantitySheetId { get; set; }
        public double LotPercent { get; set; }
        public double CatchPercent { get; set; }
    }
}
