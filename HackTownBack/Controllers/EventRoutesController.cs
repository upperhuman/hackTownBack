using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HackTownBack.Models;

namespace HackTownBack.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EventRoutesController : ControllerBase
    {
        private readonly HackTownDbContext _context;

        private const string ApiKey = "AIzaSyCE5WTbBE1wj6sOibVurOLXsPwlVqAQP5U";
        public EventRoutesController(HackTownDbContext context)
        {
            _context = context;
        }

        // GET: api/EventRoutes
        [HttpGet]
        public async Task<ActionResult<IEnumerable<EventRoute>>> GetEventRoutes()
        {
            return await _context.EventRoutes.ToListAsync();
        }

        // GET: api/EventRoutes/5
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(LocationDto))]
        public async Task<ActionResult<EventRoute>> GetEventRoute(int id)
        {
            var eventRoute = await _context.EventRoutes
                .Include(er => er.Locations.OrderBy(loc => loc.StepNumber))
                .FirstOrDefaultAsync(er => er.Id == id);

            if (eventRoute == null || !eventRoute.Locations.Any())
            {
                return NotFound("Маршрут або точки маршруту не знайдено.");
            }

            var locations = eventRoute.Locations.Select(loc => new
            {
                loc.Name,
                loc.Latitude,
                loc.Longitude,
                loc.Address,
                loc.Description,
                loc.StepNumber
            }).ToList();

            return Ok(locations);
        }

        // PUT: api/EventRoutes/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutEventRoute(int id, EventRoute eventRoute)
        {
            if (id != eventRoute.Id)
            {
                return BadRequest();
            }

            _context.Entry(eventRoute).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EventRouteExists(id))
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

        // POST: api/EventRoutes
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<EventRoute>> PostEventRoute(EventRoute eventRoute)
        {
            _context.EventRoutes.Add(eventRoute);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetEventRoute", new { id = eventRoute.Id }, eventRoute);
        }

        // DELETE: api/EventRoutes/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEventRoute(int id)
        {
            var eventRoute = await _context.EventRoutes.FindAsync(id);
            if (eventRoute == null)
            {
                return NotFound();
            }

            _context.EventRoutes.Remove(eventRoute);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool EventRouteExists(int id)
        {
            return _context.EventRoutes.Any(e => e.Id == id);
        }
    }
}
