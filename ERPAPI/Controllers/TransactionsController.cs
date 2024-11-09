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

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetTransaction(int projectId, int processId)
        {
            // Fetch the data first (without trying to parse AlarmId)
            var transactions = await (from t in _context.Transaction
                                      where t.ProjectId == projectId && t.ProcessId == processId
                                      select t).ToListAsync(); // Fetch data to memory

            // Now, you can perform the parsing on the client-side after data is fetched
            var transactionsWithAlarms = transactions
                .Select(t =>
                {
                    var parsedAlarmId = TryParseAlarmId(t.AlarmId); // Apply parsing here
                                                                    // Check if parsedAlarmId is an integer (in case the parsing was successful)
                    var alarm = parsedAlarmId is int parsedId
                        ? _context.Alarm.FirstOrDefault(a => a.AlarmId == parsedId)
                        : null;

                    return new
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
                        t.VoiceRecording,
                        t.Status,
                        t.MachineId,
                        AlarmMessage = alarm != null ? alarm.Message : null // Handle null case for alarms
                    };
                }).ToList(); // Apply the transformation in memory

            if (transactionsWithAlarms == null || !transactionsWithAlarms.Any())
            {
                return NotFound(); // Return a 404 if no transactions are found
            }

            return Ok(transactionsWithAlarms);
        }

        // Utility function to attempt parsing AlarmId and return an integer if possible, else return the original value
        private object TryParseAlarmId(object alarmId)
        {
            if (alarmId == null)
            {
                return null; // Return null if AlarmId is null
            }

            int parsedId;
            if (int.TryParse(alarmId.ToString(), out parsedId))
            {
                return parsedId; // Return integer if parsing succeeds
            }

            return alarmId; // Return the original value if parsing fails
        }





        // GET: api/Transactions/5
        //[HttpGet("{id}")]
        //public async Task<ActionResult<Transaction>> GetTransaction(int id)
        //{
        //    var transaction = await _context.Transaction.FindAsync(id);

        //    if (transaction == null)
        //    {
        //        return NotFound();
        //    }

        //    return transaction;
        //}

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

        [HttpPut("quantitysheet/{quantitysheetId}")]
        public async Task<IActionResult> PutTransactionId(int quantitysheetId, Transaction transaction)
        {
            if (quantitysheetId != transaction.QuantitysheetId)
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
                if (!TransactionExists(quantitysheetId))
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

        [HttpPost]
        public async Task<IActionResult> CreateTransaction([FromBody] Transaction transaction)
        {
            if (transaction == null)
            {
                return BadRequest("Invalid data.");
            }

            // Add a new transaction object
            _context.Transaction.Add(transaction);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Transaction created successfully." });
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
        //   [HttpGet("percentages")]
        //   public async Task<ActionResult<Percentages>> GetPercentages(int ProjectId)
        //   {
        //       var processes = await _context.ProjectProcesses.Where(p => p.ProjectId == ProjectId).ToListAsync();
        //       var quantitySheets = await _context.QuantitySheets.Where(p => p.ProjectId == ProjectId).ToListAsync();
        //       var transactions = await _context.Transaction.Where(p => p.ProjectId == ProjectId).ToListAsync();

        //       var result = CalculatePercentages(processes, quantitySheets, transactions);
        //       return Ok(result);
        //   }



        //   private Percentages CalculatePercentages(
        //List<ProjectProcess> processes,
        //List<QuantitySheet> quantitySheets,
        //List<Transaction> transactions)
        //   {
        //       if (processes == null || quantitySheets == null || transactions == null)
        //       {
        //           throw new ArgumentNullException("One or more input lists are null.");
        //       }

        //       var completedProcesses = transactions.Where(t => t.StatusId == 3).ToList();
        //       var partiallyCompletedProcesses = transactions.Where(t => t.StatusId == 2).ToList();

        //       var sheetPercentages = new List<SheetPercentage>();
        //       var lotPercentages = new Dictionary<string, double>();
        //       var lotCatchPercentages = new Dictionary<string, double>();

        //       // Group quantity sheets by LotNo
        //       var sheetsGroupedByLotNo = quantitySheets.GroupBy(sheet => sheet.LotNo);

        //       foreach (var group in sheetsGroupedByLotNo)
        //       {
        //           double lotTotalWeightage = 0;
        //           double lotTotalCatchPercent = 0;
        //           double totalLotPercent = 0; // For this specific lot

        //           foreach (var sheet in group)
        //           {

        //               double completedProcessWeightage = 0;
        //               double partiallyCompletedWeightage = 0;

        //               // Analyze processes
        //               foreach (var process in processes)
        //               {
        //                   var completedCount = completedProcesses.Count(t => t.QuantitysheetId == sheet.QuantitySheetId && t.ProcessId == process.ProcessId);
        //                   var partiallyCount = partiallyCompletedProcesses.Count(t => t.QuantitysheetId == sheet.QuantitySheetId && t.ProcessId == process.ProcessId);

        //                   if (completedCount > 0)
        //                   {
        //                       completedProcessWeightage += process.Weightage;
        //                   }
        //                   if (partiallyCount > 0)
        //                   {
        //                       partiallyCompletedWeightage += process.Weightage;
        //                   }
        //               }

        //               var partiallyCompletedQty = partiallyCompletedProcesses
        //                   .Where(t => t.QuantitysheetId == sheet.QuantitySheetId)
        //                   .Sum(t => t.Quantity);

        //               var totalWeightage = completedProcessWeightage +
        //                   (partiallyCompletedWeightage * partiallyCompletedQty / Math.Max(sheet.Quantity, 1));

        //               // Calculate Lot Percent
        //               double lotPercent = sheet.PercentageCatch * totalWeightage / 100;
        //               totalLotPercent += lotPercent; // Accumulate lot percent for this lot
        //               lotTotalWeightage += totalWeightage; // Accumulate total weightage for the lot

        //               // Calculate Catch Percent
        //               double catchPercent = processes
        //                   .Where(p => completedProcesses.Any(t => t.QuantitysheetId == sheet.QuantitySheetId && t.ProcessId == p.ProcessId))
        //                   .Sum(p => p.Weightage);


        //               foreach (var transaction in partiallyCompletedProcesses
        //                   .Where(t => t.QuantitysheetId == sheet.QuantitySheetId))
        //               {
        //                   var processWeightage = processes.FirstOrDefault(p => p.ProcessId == transaction.ProcessId)?.Weightage ?? 0;

        //                   if (processWeightage > 0)
        //                   {
        //                       catchPercent += (processWeightage * transaction.Quantity) / Math.Max(sheet.Quantity, 1);
        //                   }
        //               }

        //               var totalProcessWeightage = processes.Sum(p => p.Weightage);
        //               double catchPercentNormalized = totalProcessWeightage > 0 ? (catchPercent / totalProcessWeightage) * 100 : 0;
        //               lotTotalCatchPercent += catchPercentNormalized; // Accumulate catch percent for the lot

        //               sheetPercentages.Add(new SheetPercentage
        //               {
        //                   QuantitySheetId = sheet.QuantitySheetId,
        //                   LotPercent = lotPercent,
        //                   CatchPercent = catchPercentNormalized
        //               });
        //           }

        //           // Store the total lot percentage for this specific lot
        //           if (group.Key != null)
        //           {
        //               lotPercentages[group.Key] = totalLotPercent; // Store cumulative lot percent
        //               lotCatchPercentages[group.Key] = lotTotalCatchPercent; // Store cumulative catch percent for the lot
        //           }
        //       }

        //       // Return the final Percentages object with detailed lot percentages
        //       return new Percentages
        //       {
        //           LotPercent = lotPercentages, // Return individual lot percentages
        //           ProjectPercent = lotPercentages.Sum(l => l.Value), // Calculate total project percent from individual lot percentages
        //           SheetPercentages = sheetPercentages
        //       };
        //   }


        // This is the main endpoint where percentages are calculated for the project




        [HttpGet("all-project-completion-percentages")]
        public async Task<ActionResult> GetAllProjectCompletionPercentages()
        {
            var projects = await _context.Projects.ToListAsync();
            var projectCompletionPercentages = new List<dynamic>();

            foreach (var project in projects)
            {
                var projectId = project.ProjectId;

        [HttpGet("alarms")]
        public async Task<ActionResult<IEnumerable<object>>> GetAlarmsByProjectId(int projectId)
        {
            // Fetch alarms that belong to the specified projectId where AlarmId != 0 and not an empty string
            var alarms = await _context.Transaction
                .Where(a => a.ProjectId == projectId && a.AlarmId != "0" && !string.IsNullOrEmpty(a.AlarmId))
                .ToListAsync();

            if (alarms == null || !alarms.Any())
            {
                return NotFound(); // Return 404 if no alarms are found
            }

            var alarmData = alarms.Select(a => new
            {
                a.TransactionId,
                a.AlarmId,
                a.MachineId,
                a.InterimQuantity,
                a.TeamId,
                a.ZoneId,
                a.QuantitysheetId,
                a.ProjectId,
                a.LotNo,
               
            }).ToList();

            return Ok(alarmData);
        }



                // Fetch relevant data for each project
                var projectProcesses = await _context.ProjectProcesses
                    .Where(p => p.ProjectId == projectId)
                    .ToListAsync();


                var quantitySheets = await _context.QuantitySheets
                    .Where(qs => qs.ProjectId == projectId)
                    .ToListAsync();

                var transactions = await _context.Transaction
                    .Where(t => t.ProjectId == projectId)
                    .ToListAsync();

                var totalLotPercentages = new Dictionary<string, double>();
                var lotQuantities = new Dictionary<string, double>();
                double projectTotalQuantity = 0;

                foreach (var quantitySheet in quantitySheets)
                {
                    var processIdWeightage = new Dictionary<int, double>();
                    double totalWeightageSum = 0;

                    // Calculate weightages for each process in the quantity sheet
                    foreach (var processId in quantitySheet.ProcessId)
                    {
                        var process = projectProcesses.FirstOrDefault(p => p.ProcessId == processId);
                        if (process != null)
                        {
                            processIdWeightage[processId] = Math.Round(process.Weightage, 2);
                            totalWeightageSum += process.Weightage;
                        }
                    }

                    // Adjust weightages if they don’t sum up to 100
                    if (totalWeightageSum < 100)
                    {
                        double deficit = 100 - totalWeightageSum;
                        double adjustment = deficit / processIdWeightage.Count;
                        foreach (var key in processIdWeightage.Keys.ToList())
                        {
                            processIdWeightage[key] = Math.Round(processIdWeightage[key] + adjustment, 2);
                        }
                    }

                    // Calculate the completed weightage sum
                    double completedWeightageSum = 0;
                    foreach (var kvp in processIdWeightage)
                    {
                        var processId = kvp.Key;
                        var weightage = kvp.Value;
                        var completedProcess = transactions
                            .Any(t => t.QuantitysheetId == quantitySheet.QuantitySheetId
                                      && t.ProcessId == processId
                                      && t.Status == 2);

                        if (completedProcess)
                        {
                            completedWeightageSum += weightage;
                        }
                    }


                    // Calculate lot percentage and add to total project quantity
                    double lotPercentage = Math.Round(quantitySheet.PercentageCatch * (completedWeightageSum / 100), 2);
                    var lotNumber = quantitySheet.LotNo;


                    totalLotPercentages[lotNumber] = Math.Round(totalLotPercentages.GetValueOrDefault(lotNumber) + lotPercentage, 2);
                    lotQuantities[lotNumber] = lotQuantities.GetValueOrDefault(lotNumber) + quantitySheet.Quantity;
                    projectTotalQuantity += quantitySheet.Quantity;
                }

                // Calculate lot weightages and the final project completion percentage
                double totalProjectLotPercentage = 0;

                foreach (var lot in totalLotPercentages)
                {
                    var lotNumber = lot.Key;
                    var quantity = lotQuantities[lotNumber];
                    var lotWeightage = projectTotalQuantity > 0 ? (quantity / projectTotalQuantity) * 100 : 0;

                    totalProjectLotPercentage += totalLotPercentages[lotNumber] * (lotWeightage / 100);
                }

                totalProjectLotPercentage = Math.Round(totalProjectLotPercentage, 2);

                projectCompletionPercentages.Add(new
                {
                    ProjectId = projectId,
                    CompletionPercentage = totalProjectLotPercentage
                });
            }

            return Ok(projectCompletionPercentages);
        }

        [HttpGet("combined-percentages")]
        public async Task<ActionResult> GetCombinedPercentages(int projectId)
        {
            var projectProcesses = await _context.ProjectProcesses
                .Where(p => p.ProjectId == projectId)
                .ToListAsync();

            var quantitySheets = await _context.QuantitySheets
                .Where(p => p.ProjectId == projectId)
                .ToListAsync();

            var transactions = await _context.Transaction
                .Where(t => t.ProjectId == projectId)
                .ToListAsync();

            var lots = new Dictionary<string, Dictionary<int, dynamic>>();
            var totalLotPercentages = new Dictionary<string, double>();
            var lotQuantities = new Dictionary<string, double>();
            var lotWeightages = new Dictionary<string, double>();
            var projectLotPercentages = new Dictionary<string, double>();
            var lotProcessWeightageSum = new Dictionary<string, Dictionary<int, double>>();
            double projectTotalQuantity = 0;

            foreach (var quantitySheet in quantitySheets)
            {
                var processIdWeightage = new Dictionary<int, double>();
                double totalWeightageSum = 0;

                foreach (var processId in quantitySheet.ProcessId)
                {
                    var process = projectProcesses.FirstOrDefault(p => p.ProcessId == processId);
                    if (process != null)
                    {
                        processIdWeightage[processId] = Math.Round(process.Weightage, 2);
                        totalWeightageSum += process.Weightage;
                    }
                }


                if (totalWeightageSum < 100)
                {
                    double deficit = 100 - totalWeightageSum;
                    double adjustment = deficit / processIdWeightage.Count;

                    foreach (var key in processIdWeightage.Keys.ToList())
                    {
                        processIdWeightage[key] = Math.Round(processIdWeightage[key] + adjustment, 2);

                    }

                    totalWeightageSum = processIdWeightage.Values.Sum();
                }

                double completedWeightageSum = 0;
                foreach (var kvp in processIdWeightage)
                {
                    var processId = kvp.Key;
                    var weightage = kvp.Value;

                    var completedProcess = transactions
                        .Any(t => t.QuantitysheetId == quantitySheet.QuantitySheetId
                                  && t.ProcessId == processId
                                  && t.Status == 2);

                    if (completedProcess)
                    {
                        completedWeightageSum += weightage;
                    }
                }

                double lotPercentage = Math.Round(quantitySheet.PercentageCatch * (completedWeightageSum / 100), 2);
                var lotNumber = quantitySheet.LotNo;

                if (!lots.ContainsKey(lotNumber))
                {
                    lots[lotNumber] = new Dictionary<int, dynamic>();
                    totalLotPercentages[lotNumber] = 0;
                    lotQuantities[lotNumber] = 0;
                }

                lots[lotNumber][quantitySheet.QuantitySheetId] = new
                {
                    CompletedProcessPercentage = Math.Round(completedWeightageSum, 2),
                    LotPercentage = lotPercentage,
                    ProcessDetails = processIdWeightage
                };

                totalLotPercentages[lotNumber] = Math.Round(totalLotPercentages[lotNumber] + lotPercentage, 2);
                lotQuantities[lotNumber] += quantitySheet.Quantity;
                projectTotalQuantity += quantitySheet.Quantity;

                if (!lotProcessWeightageSum.ContainsKey(lotNumber))
                {
                    lotProcessWeightageSum[lotNumber] = new Dictionary<int, double>();
                }

                foreach (var processId in processIdWeightage.Keys)
                {
                    var lotNumberStr = lotNumber.ToString();

                    var completedQuantitySheets = transactions
                        .Count(t => t.LotNo.ToString() == lotNumberStr && t.ProcessId == processId && t.Status == 2);

                    var totalQuantitySheets = quantitySheets
                        .Count(qs => qs.LotNo.ToString() == lotNumberStr);

                    double processPercentage = totalQuantitySheets > 0
                        ? Math.Round((double)completedQuantitySheets / totalQuantitySheets * 100, 2)
                        : 0;

                    lotProcessWeightageSum[lotNumber][processId] = processPercentage;
                }


            }

            foreach (var lot in lotQuantities)
            {
                var lotNumber = lot.Key;
                var quantity = lot.Value;

                lotWeightages[lotNumber] = Math.Round((quantity / projectTotalQuantity) * 100, 2);
                projectLotPercentages[lotNumber] = Math.Round(totalLotPercentages[lotNumber] * lotWeightages[lotNumber] / 100, 2);
            }

            double totalProjectLotPercentage = Math.Round(projectLotPercentages.Values.Sum(), 2);
            projectTotalQuantity = Math.Round(projectTotalQuantity, 2);

            return Ok(new
            {
                //Lots = lots,
                TotalLotPercentages = totalLotPercentages,
                LotQuantities = lotQuantities,
                LotWeightages = lotWeightages,
                ProjectLotPercentages = projectLotPercentages,
                TotalProjectLotPercentage = totalProjectLotPercentage,
                ProjectTotalQuantity = projectTotalQuantity,
                LotProcessWeightageSum = lotProcessWeightageSum
            });
        }


        private double NormalizePercentage(double currentPercentage)
        {
            double remainingPercent = 100 - currentPercentage;
            return remainingPercent > 0 ? currentPercentage + remainingPercent * (currentPercentage / (100 - currentPercentage)) : currentPercentage;
        }

        private double CalculateTotalCatchQuantity(int projectId)
        {
            var catchQuantitySheets = _context.QuantitySheets
                .Where(sheet => sheet.ProjectId == projectId)
                .ToList();

            return catchQuantitySheets.Sum(sheet => sheet.Quantity);
        }

        // Inner classes for SheetPercentage and Percentages
        public class SheetPercentage
        {
            public int QuantitySheetId { get; set; }
            public double LotPercent { get; set; }
            public double CatchPercent { get; set; }
        }

        public class Percentages
        {
            public Dictionary<string, double> LotPercent { get; set; }
            public List<SheetPercentage> SheetPercentages { get; set; }
            public double ProjectPercent { get; set; }
            public double TotalCatchQuantity { get; set; }
        }

    }
}


