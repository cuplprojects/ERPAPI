﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ERPAPI.Data;
using ERPAPI.Model;

namespace ERPAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MachinesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MachinesController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/Machines
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Machine>>> GetMachines()
        {
            var machinesWithProcesses = await (from m in _context.Machine
                                               join p in _context.Processes on m.ProcessId equals p.Id
                                               select new
                                               {
                                                   m.MachineId,
                                                   m.MachineName,
                                                   m.Status,
                                                   m.ProcessId,
                                                   ProcessName = p.Name
                                               }).ToListAsync();

            return Ok(machinesWithProcesses);
        }

        // GET: api/Machines/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Machine>> GetMachine(int id)
        {
            var machine = await _context.Machine.FindAsync(id);

            if (machine == null)
            {
                return NotFound();
            }

            return machine;
        }

        // PUT: api/Machines/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutMachine(int id, Machine machine)
        {
            if (id != machine.MachineId)
            {
                return BadRequest();
            }

            _context.Entry(machine).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MachineExists(id))
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

        // POST: api/Machines
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Machine>> PostMachine(Machine machine)
        {
            _context.Machine.Add(machine);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetMachine", new { id = machine.MachineId }, machine);
        }

        // DELETE: api/Machines/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMachine(int id)
        {
            var machine = await _context.Machine.FindAsync(id);
            if (machine == null)
            {
                return NotFound();
            }

            _context.Machine.Remove(machine);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool MachineExists(int id)
        {
            return _context.Machine.Any(e => e.MachineId == id);
        }
    }
}
