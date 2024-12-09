﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ERPAPI.Data;
using ERPAPI.Model;
using Microsoft.CodeAnalysis;
using System.Diagnostics;
using ERPAPI.Services;
using ERPAPI.Service.ProjectTransaction;


namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionsController : ControllerBase
    {
        private readonly AppDbContext _context;

        private readonly IProjectCompletionService _projectCompletionService;
        private readonly IProjectTransactionService _projectTransactionService;

        public TransactionsController(AppDbContext context, IProjectCompletionService projectCompletionService, IProjectTransactionService projectTransactionService)
        {
            _context = context;

            _projectCompletionService = projectCompletionService;
            _projectTransactionService = projectTransactionService;
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


        // 
        /*  [HttpGet("GetProjectTransactionsDataOld")]
          public async Task<ActionResult<IEnumerable<object>>> GetProjectTransactionsDataOld(int projectId, int processId)
          {
              // Fetch quantity sheet data
              var quantitySheetData = await _context.QuantitySheets
                  .Where(q => q.ProjectId == projectId && q.Status == 1)
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
                                            t.TeamId,
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
              var project = await _context.Projects.FirstOrDefaultAsync(p => p.ProjectId == projectId);

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
                      .Where(u => t.TeamId.Contains(u.UserId))
                      .Select(u => u.FirstName + " " + u.LastName)
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
                      AlarmMessage = alarm != null ? alarm.Message : null, // Handle null case for alarms
                  };
              }).ToList();

              // Group quantity sheet data by CatchNo
              var groupedQuantitySheets = quantitySheetData
                  .GroupBy(q => q.CatchNo)
                  .Select(group =>
                  {
                      // Retrieve the series name from the project and assign based on position in the group
                      var seriesName = project?.SeriesName ?? "";  // Fetch the SeriesName from Project

                      // Assign SeriesName based on the index of the QuantitySheet in the group
                      var quantitySheetsWithSeriesName = group.Select((q, index) =>
                      {
                          var seriesLetter = index < seriesName.Length ? seriesName[index].ToString() : ""; // Use SeriesName from project
                          return new
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
                              SeriesName = seriesLetter,  // Assign the SeriesName here
                              ProcessIds = q.ProcessId,   // Assuming ProcessIds is a list, map it directly
                          };
                      }).ToList();

                      // Get the transactions related to the QuantitySheetId(s) in this group
                      var relatedTransactions = transactionsWithAlarms
                          .Where(t => group.Select(g => g.QuantitySheetId).Contains(t.QuantitysheetId))
                          .ToList();

                      return new
                      {
                          CatchNo = group.Key,  // The CatchNo for this group
                          QuantitySheets = quantitySheetsWithSeriesName,
                          Transactions = relatedTransactions  // Add the related transactions for each QuantitySheet group
                      };
                  })
                  .ToList();

              // Flatten the grouped data into a single list
              var flatResponseData = groupedQuantitySheets.SelectMany(group => group.QuantitySheets.Select(q => new
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
                  SeriesName = q.SeriesName,  // Use the assigned SeriesName
                  ProcessIds = q.ProcessIds,  // Assuming ProcessIds is a list, map it directly
                  Transactions = group.Transactions  // Add the transactions related to this group
              }))
              .ToList();

              // Return the structured response
              return Ok(flatResponseData);
          }
  */


        /*   [HttpGet("GetProjectTransactionsDataOld")]
           public async Task<ActionResult<IEnumerable<object>>> GetProjectTransactionsDataOld(int projectId, int processId)
           {
               // Fetch quantity sheet data
               var quantitySheetData = await _context.QuantitySheets
                   .Where(q => q.ProjectId == projectId && q.Status == 1)
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
   */


        [HttpGet("GetProjectTransactionsDataOld")]
        public async Task<ActionResult<IEnumerable<object>>> GetProjectTransactionsDataOld(int projectId, int processId)
        {
            // Fetch quantity sheet data
            var quantitySheetData = await _context.QuantitySheets
                .Where(q => q.ProjectId == projectId && q.Status == 1)
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
                                          t.TeamId,
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

            // Fetch project details
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.ProjectId == projectId);

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
                    .Where(u => t.TeamId.Contains(u.UserId))
                    .Select(u => u.FirstName + " " + u.LastName)
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
                    AlarmMessage = alarm != null ? alarm.Message : null, // Handle null case for alarms
                };
            }).ToList();

            // Apply the logic for SeriesName from Project (using Project.SeriesName)
            var quantitySheetDataWithSeriesName = quantitySheetData
                .GroupBy(q => q.CatchNo)  // Group by CatchNo for SeriesName assignment
                .Select(group =>
                {
                    var seriesName = project?.SeriesName ?? "";  // Get the SeriesName from the project

                    // Assign SeriesName based on the group
                    var quantitySheetsWithSeriesName = group.Select((q, index) =>
                    {
                        var seriesLetter = index < seriesName.Length ? seriesName[index].ToString() : ""; // Use SeriesName from project
                        return new
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
                            SeriesName = seriesLetter,  // Assign the SeriesName here
                            ProcessIds = q.ProcessId,   // Assuming ProcessIds is a list, map it directly
                        };
                    }).ToList();

                    // Return the group with the SeriesName applied
                    return new
                    {
                        CatchNo = group.Key,  // The CatchNo for this group
                        QuantitySheets = quantitySheetsWithSeriesName
                    };
                })
                .ToList();

            // Flatten the grouped data into a single list
            var flattenedQuantitySheetData = quantitySheetDataWithSeriesName
                .SelectMany(group => group.QuantitySheets)
                .ToList();

            // Combine QuantitySheet and Transaction data in a structured response
            var responseData = flattenedQuantitySheetData.Select(q =>
            {
                return new
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
                    q.SeriesName,  // Directly use the SeriesName
                    ProcessIds = q.ProcessIds, // Assuming ProcessIds is a list, map it directly
                    Transactions = transactionsWithAlarms
                        .Where(t => t.QuantitysheetId == q.QuantitySheetId) // Only transactions matching the QuantitySheetId
                        .ToList()
                };
            }).ToList();

            return Ok(responseData);
        }


        // Utility function to attempt parsing AlarmId and return an integer if possible, else return the original value











        [HttpGet("GetProjectTransactionsData")]
        public async Task<ActionResult<IEnumerable<object>>> GetProjectTransactionsData(int projectId, int processId)
        {
            try
            {
                // Call the service method to get the data
                var projectTransactionsData = await _projectTransactionService.GetProjectTransactionsDataAsync(projectId, processId);

                // Return the data as a successful response
                return Ok(projectTransactionsData);
            }
            catch (System.Exception ex)
            {
                // In case of any error, return a bad request response with the exception message
                return BadRequest(new { message = ex.Message });
            }
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
        // Utility function to attempt parsing AlarmId and return an integer if possible, else return the original value
       





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
            var projectCompletionPercentages = await _projectCompletionService.CalculateProjectCompletionPercentages();
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
                                    Process = p != null ? p.Name : null,
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

                // Only consider processes that are part of the current quantity sheet's processes
                foreach (var processId in quantitySheet.ProcessId)
                {
                    if (processId == 14) continue; // Exclude ProcessId 14

                    var process = projectProcesses.FirstOrDefault(p => p.ProcessId == processId);
                    if (process != null)
                    {
                        processIdWeightage[processId] = Math.Round(process.Weightage, 2);
                        totalWeightageSum += process.Weightage;
                    }
                }

                // Ensure the total weightage sum equals 100
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
                projectTotalQuantity += quantitySheet.Quantity;  // This line now uses the total quantity from the sheet

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
                        .Count(qs => qs.LotNo.ToString() == lotNumberStr && qs.ProcessId.Contains(processId));

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

            // Add the distinct process IDs for the project
            var processIds = projectProcesses.Select(p => p.ProcessId).Distinct();

            return Ok(new
            {
                TotalLotPercentages = totalLotPercentages,
                LotQuantities = lotQuantities,
                LotWeightages = lotWeightages,
                ProjectLotPercentages = projectLotPercentages,
                TotalProjectLotPercentage = totalProjectLotPercentage,
                ProjectTotalQuantity = projectTotalQuantity,
                LotProcessWeightageSum = lotProcessWeightageSum
            });
        }




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

        [HttpGet("process-percentages")]
        public async Task<ActionResult> GetProcessPercentages(int projectId)
        {
            var processes = await _context.ProjectProcesses
                .Where(p => p.ProjectId == projectId)
                .ToListAsync();

            var quantitySheets = await _context.QuantitySheets
                .Where(qs => qs.ProjectId == projectId)
                .ToListAsync();

            var transactions = await _context.Transaction
                .Where(t => t.ProjectId == projectId)
                .ToListAsync();

            var processesList = new List<object>();
            var totalProjectSheets = 0;
            var totalProjectCompletedSheets = 0;

            foreach (var process in processes)
            {
                var uniqueLots = quantitySheets.Select(qs => qs.LotNo).Distinct();
                var lotsList = new List<object>();
                var totalProcessSheets = 0;
                var totalProcessCompletedSheets = 0;

                foreach (var lotNo in uniqueLots)
                {
                    var lotQuantitySheets = quantitySheets.Where(qs => qs.LotNo == lotNo).ToList();
                    var completedSheets = lotQuantitySheets.Count(qs =>
                        transactions.Any(t =>
                            t.QuantitysheetId == qs.QuantitySheetId &&
                            t.ProcessId == process.ProcessId &&
                            t.Status == 2
                        )
                    );

                    // Calculate total sheets for this lot that are related to the process
                    var totalSheets = lotQuantitySheets.Count(sheet => sheet.ProcessId.Contains(process.ProcessId));

                    // If totalSheets is 0, return 100% (since there is nothing to complete)
                    var percentage = totalSheets == 0
                        ? 100
                        : Math.Round((double)completedSheets / totalSheets * 100, 2);

                    totalProcessSheets += totalSheets;
                    totalProcessCompletedSheets += completedSheets;

                    lotsList.Add(new
                    {
                        lotNumber = lotNo,
                        percentage = percentage,
                        totalSheets = totalSheets,
                        completedSheets = completedSheets
                    });
                }

                totalProjectSheets += totalProcessSheets;
                totalProjectCompletedSheets += totalProcessCompletedSheets;

                var overallPercentage = totalProcessSheets > 0
                    ? Math.Round((double)totalProcessCompletedSheets / totalProcessSheets * 100, 2)
                    : 100; // If no total sheets for the process, consider 100%

                processesList.Add(new
                {
                    processId = process.ProcessId,
                    statistics = new
                    {
                        totalLots = lotsList.Count,
                        totalSheets = totalProcessSheets,
                        completedSheets = totalProcessCompletedSheets,
                        overallPercentage = overallPercentage
                    },
                    lots = lotsList
                });
            }

            var overallProjectPercentage = totalProjectSheets > 0
                ? Math.Round((double)totalProjectCompletedSheets / totalProjectSheets * 100, 2)
                : 100; // If no total sheets for the project, consider 100%

            var result = new
            {
                totalProcesses = processes.Count,
                overallProjectPercentage = overallProjectPercentage,
                processes = processesList
            };

            return Ok(result);
        }


        [HttpGet("process-lot-percentages")]
        public async Task<ActionResult> GetProcessLotPercentages(int projectId)
        {
            var processes = await _context.ProjectProcesses
                .Where(p => p.ProjectId == projectId)
                .ToListAsync();

            var quantitySheets = await _context.QuantitySheets
                .Where(qs => qs.ProjectId == projectId)
                .ToListAsync();

            var transactions = await _context.Transaction
                .Where(t => t.ProjectId == projectId)
                .ToListAsync();

            var processesList = new List<object>();
            var totalProjectSheets = 0;
            var totalProjectCompletedSheets = 0;
            var totalProjectQuantity = 0.0; // To track the overall project quantity

            foreach (var process in processes)
            {
                var processQuantitySheets = quantitySheets
    .Where(qs => qs.ProcessId.Contains(process.ProcessId)) // Check if the ProcessId list contains the current ProcessId
    .ToList();
                // Filter sheets for the current process

                var uniqueLots = processQuantitySheets
                    .Select(qs => qs.LotNo)
                    .Distinct();

                var lotsList = new List<object>();
                var totalProcessSheets = 0;
                var totalProcessCompletedSheets = 0;
                var totalProcessQuantity = processQuantitySheets.Sum(qs => qs.Quantity); // Sum quantity for the process

                foreach (var lotNo in uniqueLots)
                {
                    var lotQuantitySheets = processQuantitySheets
                        .Where(qs => qs.LotNo == lotNo)
                        .ToList();

                    var completedSheets = lotQuantitySheets.Count(qs =>
                        transactions.Any(t =>
                            t.QuantitysheetId == qs.QuantitySheetId &&
                            t.ProcessId == process.ProcessId &&
                            t.Status == 2
                        )
                    );

                    var totalSheets = lotQuantitySheets.Count;
                    var lotQuantity = lotQuantitySheets.Sum(qs => qs.Quantity); // Sum quantity for the lot

                    var percentage = totalSheets > 0
                        ? Math.Round((double)completedSheets / totalSheets * 100, 2)
                        : 0;

                    totalProcessSheets += totalSheets;
                    totalProcessCompletedSheets += completedSheets;

                    lotsList.Add(new
                    {
                        lotNumber = lotNo,
                        percentage = percentage,
                        totalSheets = totalSheets,
                        completedSheets = completedSheets,
                        lotQuantity = lotQuantity // Add quantity for the lot
                    });
                }

                totalProjectSheets += totalProcessSheets;
                totalProjectCompletedSheets += totalProcessCompletedSheets;
                totalProjectQuantity += totalProcessQuantity;

                var overallPercentage = totalProcessSheets > 0
                    ? Math.Round((double)totalProcessCompletedSheets / totalProcessSheets * 100, 2)
                    : 0;

                processesList.Add(new
                {
                    processId = process.ProcessId,
                    statistics = new
                    {
                        totalLots = lotsList.Count,
                        totalSheets = totalProcessSheets,
                        completedSheets = totalProcessCompletedSheets,
                        totalQuantity = totalProcessQuantity, // Add total quantity for the process
                        overallPercentage = overallPercentage
                    },
                    lots = lotsList
                });
            }

            var overallProjectPercentage = totalProjectSheets > 0
                ? Math.Round((double)totalProjectCompletedSheets / totalProjectSheets * 100, 2)
                : 0;

            var result = new
            {
                totalProcesses = processes.Count,
                overallProjectQuantity = totalProjectQuantity, // Add overall project quantity
                overallProjectPercentage = overallProjectPercentage,
                processes = processesList
            };

            return Ok(result);
        }


        [HttpGet("exists/{projectId}")]
        public async Task<ActionResult<bool>> TransactionExistsByProject(int projectId)
        {
            try
            {
                bool exists = await _context.Transaction
                    .AnyAsync(t => t.ProjectId == projectId);

                return Ok(exists);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

    }
}

