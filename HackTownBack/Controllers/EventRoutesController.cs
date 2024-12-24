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
        public async Task<ActionResult<EventRoute>> GetEventRoute(int id)
        {
            // Getting directions with locations
            var eventRoute = await _context.EventRoutes
                .Include(er => er.Locations.OrderBy(loc => loc.StepNumber)) // Locations in order of steps
                .FirstOrDefaultAsync(er => er.Id == id);

            if (eventRoute == null || !eventRoute.Locations.Any())
            {
                return NotFound("No route or waypoints found.");
            }

            // Forming route points
            var waypoints = eventRoute.Locations
                .Select(loc => $"{loc.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{loc.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}")
                .ToList();
            // If only one point
            if (waypoints.Count == 1)
            {
                var singleLocation = waypoints.First();
                var googleMapsSingleLocationUrl = $"https://www.google.com/maps/embed/v1/place?key={ApiKey}&q={singleLocation}";

                return Ok(googleMapsSingleLocationUrl);
            }

            //If there are several points(we form a route)
            var origin = waypoints.First();
            var destination = waypoints.Last();

            // If there are intermediate points
            var intermediateWaypoints = waypoints.Skip(1).Take(waypoints.Count - 2);

            // URL generation for Google Maps Directions
            var waypointsParam = string.Join("|", intermediateWaypoints);
            var googleMapsUrl = $"https://www.google.com/maps/embed/v1/directions?key={ApiKey}" +
                                $"&origin={origin}&destination={destination}" +
                                (intermediateWaypoints.Any() ? $"&waypoints={waypointsParam}" : "");

            return Ok(googleMapsUrl);
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
