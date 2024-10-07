using ERPAPI.Data;
using ERPAPI.Model;
using System.Collections.Generic;
using System.Linq;

namespace ERPAPI.Service
{
    public class ProcessService
    {
        private readonly AppDbContext _context;

        public ProcessService(AppDbContext context)
        {
            _context = context;
        }

        public void ProcessCatch(QuantitySheet catchData)
        {
            // Fetch the project processes related to the catchData's project
            var projectProcesses = _context.ProjectProcesses
                .Where(pp => pp.ProjectId == catchData.ProjectId)
                .Select(pp => new
                {
                    pp.ProcessId,
                    ProcessName = _context.Processes
                        .Where(p => p.Id == pp.ProcessId)
                        .Select(p => p.Name)
                        .FirstOrDefault()
                })
                .ToList();

            // Clear existing ProcessIds for fresh processing
            catchData.ProcessId.Clear();

            // Identify ProcessIds for CTP, Offset Printing, and Digital Printing
            var ctpProcessId = projectProcesses.FirstOrDefault(p => p.ProcessName == "CTP")?.ProcessId;
            var offsetPrintingProcessId = projectProcesses.FirstOrDefault(p => p.ProcessName == "Offset Printing")?.ProcessId;
            var digitalPrintingProcessId = projectProcesses.FirstOrDefault(p => p.ProcessName == "Digital Printing")?.ProcessId;

            // Determine which processes to include in ProcessId
            if (catchData.Quantity > 80)
            {
                // Include CTP and Offset Printing if the quantity is greater than 80
                if (ctpProcessId.HasValue)
                {
                    catchData.ProcessId.Add(ctpProcessId.Value);
                }

                if (offsetPrintingProcessId.HasValue)
                {
                    catchData.ProcessId.Add(offsetPrintingProcessId.Value);
                }
            }
            else
            {
                // If the quantity is 80 or less, include only Digital Printing
                if (digitalPrintingProcessId.HasValue)
                {
                    catchData.ProcessId.Add(digitalPrintingProcessId.Value);
                }
            }

            // Add all remaining ProcessIds except CTP, Offset Printing, and Digital Printing

            foreach (var process in projectProcesses)
            {
                // Skip adding if it’s already included
                if (process.ProcessId != ctpProcessId &&
                    process.ProcessId != offsetPrintingProcessId &&
                    process.ProcessId != digitalPrintingProcessId)
                {
                    catchData.ProcessId.Add(process.ProcessId);
                }
            }
        }
    }
}
