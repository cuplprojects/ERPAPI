using System;
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
    public class TextLabelsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TextLabelsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/TextLabels
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TextLabel>>> GetTextLabel()
        {
            return await _context.TextLabel.ToListAsync();
        }

        // GET: api/TextLabels/5
        [HttpGet("{id}")]
        public async Task<ActionResult<TextLabel>> GetTextLabel(int id)
        {
            var textLabel = await _context.TextLabel.FindAsync(id);

            if (textLabel == null)
            {
                return NotFound();
            }

            return textLabel;
        }

        //translation labels data
        [HttpGet("translations/{language}")]
        public async Task<ActionResult<Dictionary<string, object>>> GetTranslations(string language)
        {
            // Validate the language parameter to ensure it's either "en" or "hi"
            if (language != "en" && language != "hi")
            {
                return BadRequest("Invalid language. Supported languages are 'en' and 'hi'.");
            }

            // Fetch all text labels from the database
            var textLabels = await _context.TextLabel.ToListAsync();

            // Transform the data into the required dictionary format based on the language
            var translations = textLabels.ToDictionary(
                label => label.LabelKey,
                label => new
                {
                    id = label.TextLabelId,
                    text = language == "en" ? label.EnglishLabel : label.HindiLabel
                });

            return Ok(translations);
        }


        // PUT: api/TextLabels/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutTextLabel(int id, TextLabel textLabel)
        {
            if (id != textLabel.TextLabelId)
            {
                return BadRequest();
            }

            _context.Entry(textLabel).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!TextLabelExists(id))
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

        // POST: api/TextLabels
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<TextLabel>> PostTextLabel(TextLabel textLabel)
        {
            _context.TextLabel.Add(textLabel);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetTextLabel", new { id = textLabel.TextLabelId }, textLabel);
        }

        // DELETE: api/TextLabels/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTextLabel(int id)
        {
            var textLabel = await _context.TextLabel.FindAsync(id);
            if (textLabel == null)
            {
                return NotFound();
            }

            _context.TextLabel.Remove(textLabel);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool TextLabelExists(int id)
        {
            return _context.TextLabel.Any(e => e.TextLabelId == id);
        }
    }
}
