using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ERPAPI.Data;
using ERPAPI.Model;
using Microsoft.CodeAnalysis;
using System.Diagnostics;


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
            // Fetch the transactions based on projectId and processId
            var transactions = await (from t in _context.Transaction
                                      where t.ProjectId == projectId && t.ProcessId == processId
                                      select t).ToListAsync();

            if (transactions == null || !transactions.Any())
            {
                return NotFound(); // Return a 404 if no transactions are found
            }

            // Now, we will get the users based on the teamId in each transaction
            var transactionsWithUsers = transactions
                .Select(t =>
                {
                    var parsedAlarmId = TryParseAlarmId(t.AlarmId); // Apply parsing here
                    var alarm = parsedAlarmId is int parsedId
                        ? _context.Alarm.FirstOrDefault(a => a.AlarmId == parsedId)
                        : null;

                    // Fetch the user names based on teamId (which is an array of userId)
                    var userNames = _context.Users
                        .Where(u => t.TeamId.Contains(u.UserId)) // Match userId with the ids in TeamId
                        .Select(u => u.FirstName + "" + u.LastName)
                        .ToList();

                    return new
                    {
                        t.TransactionId,
                        t.AlarmId,
                        t.ZoneId,
                        t.QuantitysheetId,
                        t.TeamId,
                        TeamUserNames = userNames, // Add the list of usernames here
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

            return Ok(transactionsWithUsers); // Return the modified transactions with user names
        }



        [HttpGet("GetProjectTransactionsData")]
        public async Task<ActionResult<IEnumerable<object>>> GetProjectTransactionsData(int projectId, int processId)
        {
            // Fetch quantity sheet data
            var quantitySheetData = await _context.QuantitySheets
                .Where(q => q.ProjectId == projectId)
                .ToListAsync();

            // Fetch transaction data and parse alarm messages if needed
            var transactions = await (from t in _context.Transaction
                                      where t.ProjectId == projectId && t.ProcessId == processId
                                      select new
                                      {
                                          t.TransactionId,
                                          t.AlarmId,
                                          t.ZoneId,
                                          t.QuantitysheetId,
                                          t.TeamId,  // Assuming TeamId is a list of userIds
                                          t.Remarks,
                                          t.LotNo,
                                          t.InterimQuantity,
                                          t.ProcessId,
                                          t.VoiceRecording,
                                          t.Status,
                                          t.MachineId
                                      }).ToListAsync();

            // Fetch alarm messages
            var alarms = await _context.Alarm.ToListAsync();

            // Fetch users for all team members in advance to minimize the number of queries
            var allUsers = await _context.Users.ToListAsync();

            var allZone = await _context.Zone.ToListAsync();

            var allMachine = await _context.Machine.ToListAsync();

            // Map transactions with their alarm messages and usernames
            var transactionsWithAlarms = transactions.Select(t =>
            {
                var parsedAlarmId = TryParseAlarmId(t.AlarmId);
                var alarm = parsedAlarmId is int parsedId
                    ? alarms.FirstOrDefault(a => a.AlarmId == parsedId)
                    : null;

                // Get the usernames for each userId in the TeamId array
                var userNames = allUsers
                    .Where(u => t.TeamId.Contains(u.UserId)) // Match userId with the ids in TeamId
                    .Select(u => u.FirstName + " " + u.LastName)  // Concatenate FirstName and LastName
                    .ToList();

                var zone = allZone.FirstOrDefault(z => z.ZoneId == t.ZoneId);
                var zoneNo = zone != null ? zone.ZoneNo : null;


                var machine = allMachine.FirstOrDefault(z => z.MachineId == t.MachineId);
                var machinename = machine != null ? machine.MachineName : null;


                return new
                {
                    t.TransactionId,
                    AlarmId = t.AlarmId,
                    ZoneId = t.ZoneId,
                    zoneNo = zoneNo,
                    machinename = machinename,
                    QuantitysheetId = t.QuantitysheetId,
                    TeamId = t.TeamId,
                    Remarks = t.Remarks,
                    LotNo = t.LotNo,
                    TeamUserNames = userNames,  // Include the usernames
                    InterimQuantity = t.InterimQuantity,
                    ProcessId = t.ProcessId,
                    VoiceRecording = t.VoiceRecording,
                    Status = t.Status,
                    MachineId = t.MachineId,
                    ProcessIds = t.ProcessId,
                    AlarmMessage = alarm != null ? alarm.Message : null // Handle null case for alarms
                };
            }).ToList();

            // Combine QuantitySheet and Transaction data in a structured response
            var responseData = quantitySheetData.Select(q => new
            {
                q.QuantitySheetId,
                q.ProjectId,
                q.LotNo,
                q.CatchNo,
                q.Paper,
                q.ExamDate,
                q.ExamTime,
                q.Course,
                q.Subject,
                q.InnerEnvelope,
                q.OuterEnvelope,
                q.Quantity,
                q.PercentageCatch,
                ProcessIds = q.ProcessId,  // Assuming ProcessId is a list, map it directly
                Transactions = transactionsWithAlarms
                    .Where(t => t.QuantitysheetId == q.QuantitySheetId) // Filter transactions by QuantitySheetId
                    .ToList()
            });

            return Ok(responseData);
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





        //GET: api/Transactions/5
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

     // Fetch the Process from the Process table using ProcessId
     var process = await _context.Processes
         .FirstOrDefaultAsync(p => p.Id == transaction.ProcessId);

     if (process == null)
     {
         return BadRequest("Invalid ProcessId.");
     }

     // List of process names to be handled in a standard way
     var validProcessNames = new List<string> { "Digital Printing", "CTP", "Offset Printing", "Cutting" };

     // Check if the process name matches one of the valid names
     if (validProcessNames.Contains(process.Name))
     {
         // Check if a transaction already exists for this QuantitysheetId, LotNo, and ProcessId
         var existingTransaction = await _context.Transaction
             .FirstOrDefaultAsync(t => t.QuantitysheetId == transaction.QuantitysheetId &&
                                       t.LotNo == transaction.LotNo &&
                                       t.ProcessId == transaction.ProcessId);

         if (existingTransaction != null)
         {
             // If an existing transaction is found, update it
             existingTransaction.InterimQuantity = transaction.InterimQuantity;
             existingTransaction.Remarks = transaction.Remarks;
             existingTransaction.VoiceRecording = transaction.VoiceRecording;
             existingTransaction.ZoneId = transaction.ZoneId;
             existingTransaction.MachineId = transaction.MachineId;
             existingTransaction.Status = transaction.Status;
             existingTransaction.AlarmId = transaction.AlarmId;
             existingTransaction.TeamId = transaction.TeamId;

             // Update the existing transaction
             _context.Transaction.Update(existingTransaction);
         }
         else
         {
             // If no existing transaction, create a new one
             _context.Transaction.Add(transaction);
         }

         // Save changes for the valid process transactions (either created or updated)
         await _context.SaveChangesAsync();

         return Ok(new { message = "Transaction created/updated successfully." });
     }
     else
     {
         // If it's not a valid process, fetch the CatchNumber using QuantitySheetId
         var quantitySheet = await _context.QuantitySheets
             .FirstOrDefaultAsync(qs => qs.QuantitySheetId == transaction.QuantitysheetId);

         if (quantitySheet == null)
         {
             return BadRequest("QuantitySheet not found.");
         }

         string catchNumber = quantitySheet.CatchNo;

         // Retrieve all QuantitySheetIds for the same CatchNumber, LotNo, and filtered by ProjectId
         var quantitySheets = await _context.QuantitySheets
             .Where(qs => qs.CatchNo == catchNumber && qs.LotNo == transaction.LotNo.ToString() && qs.ProjectId == transaction.ProjectId)
             .ToListAsync();

         if (quantitySheets == null || !quantitySheets.Any())
         {
             return BadRequest("No matching QuantitySheets found.");
         }

         foreach (var sheet in quantitySheets)
         {
             // Check if a transaction already exists for this QuantitysheetId, LotNo, and ProcessId
             var existingTransaction = await _context.Transaction
                 .FirstOrDefaultAsync(t => t.QuantitysheetId == sheet.QuantitySheetId &&
                                           t.LotNo == transaction.LotNo &&
                                           t.ProcessId == transaction.ProcessId);

             if (existingTransaction != null)
             {
                 // If an existing transaction is found, update it
                 existingTransaction.InterimQuantity = transaction.InterimQuantity;
                 existingTransaction.Remarks = transaction.Remarks;
                 existingTransaction.VoiceRecording = transaction.VoiceRecording;
                 existingTransaction.ZoneId = transaction.ZoneId;
                 existingTransaction.MachineId = transaction.MachineId;
                 existingTransaction.Status = transaction.Status;
                 existingTransaction.AlarmId = transaction.AlarmId;
                 existingTransaction.TeamId = transaction.TeamId;

                 // You can add more fields here to update as needed

                 _context.Transaction.Update(existingTransaction); // Mark as modified
             }
             else
             {
                 // If no existing transaction, create a new one
                 var newTransaction = new Transaction
                 {
                     InterimQuantity = transaction.InterimQuantity,
                     Remarks = transaction.Remarks,
                     VoiceRecording = transaction.VoiceRecording,
                     ProjectId = transaction.ProjectId,
                     QuantitysheetId = sheet.QuantitySheetId,
                     ProcessId = transaction.ProcessId,
                     ZoneId = transaction.ZoneId,
                     MachineId = transaction.MachineId,
                     Status = transaction.Status,
                     AlarmId = transaction.AlarmId,
                     LotNo = transaction.LotNo,
                     TeamId = transaction.TeamId
                 };

                 _context.Transaction.Add(newTransaction);
             }
         }

         // Save changes for all updates and new transactions
         await _context.SaveChangesAsync();

         return Ok(new { message = "Transactions created/updated successfully." });
     }
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


        [HttpGet("all-project-completion-percentages")]
        public async Task<ActionResult> GetAllProjectCompletionPercentages()
        {
            var projects = await _context.Projects.ToListAsync();
            var projectCompletionPercentages = new List<dynamic>();

            foreach (var project in projects)
            {
                var projectId = project.ProjectId;
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

        [HttpGet("alarms")]
        public async Task<ActionResult<IEnumerable<object>>> GetAlarmsByProjectId(int projectId)
        {
            // Fetch alarms that belong to the specified projectId where AlarmId != "0" and not an empty string
            var alarms = await (from t in _context.Transaction
                                join q in _context.QuantitySheets on t.QuantitysheetId equals q.QuantitySheetId into qtyJoin
                                from q in qtyJoin.DefaultIfEmpty() // Left join to handle cases where there's no matching Quantity
                                join p in _context.Processes on t.ProcessId equals p.Id into processjoin
                                from p in processjoin.DefaultIfEmpty()
                                join a in _context.Alarm on t.AlarmId equals a.AlarmId.ToString() into alarmJoin
                                from a in alarmJoin.DefaultIfEmpty() // Left join to handle cases where there's no matching Alarm
                                where t.ProjectId == projectId && t.AlarmId != "0" && !string.IsNullOrEmpty(t.AlarmId)
                                select new
                                {
                                    t.TransactionId,
                                    t.AlarmId,
                                    t.MachineId,
                                    t.InterimQuantity,
                                    t.TeamId,
                                    t.ZoneId,
                                    t.ProcessId,
                                    t.QuantitysheetId,
                                    t.ProjectId,
                                    t.LotNo,
                                    Process = p!=null ? p.Name : null,
                                    CatchNumber = q != null ? q.CatchNo : null, // Handle null if no matching Quantity
                                    AlarmMessage = a != null ? a.Message : null // Handle null if no matching Alarm
                                }).ToListAsync();

            if (alarms == null || !alarms.Any())
            {
                return NotFound(); // Return 404 if no alarms are found
            }

            return Ok(alarms);
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


