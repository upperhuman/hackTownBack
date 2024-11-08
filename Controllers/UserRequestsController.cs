using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HackTownBack.Models;

namespace HackTownBack.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserRequestsController : ControllerBase
    {
        private readonly HackTownDbContext _context;

        public UserRequestsController(HackTownDbContext context)
        {
            _context = context;
        }

        // GET: api/UserRequests
        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserRequest>>> GetUserRequests()
        {
            return await _context.UserRequests.ToListAsync();
        }

        // GET: api/UserRequests/5
        [HttpGet("{id}")]
        public async Task<ActionResult<UserRequest>> GetUserRequest(int id)
        {
            var userRequest = await _context.UserRequests.FindAsync(id);

            if (userRequest == null)
            {
                return NotFound();
            }

            return userRequest;
        }

        // POST: api/UserRequests
        [HttpPost]
        public async Task<ActionResult> PostUserRequest(UserRequestDto userRequestDto)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == userRequestDto.UserId);
            if (!userExists)
            {
                return NotFound($"User with ID {userRequestDto.UserId} does not exist.");
            }

            // Створення нового запиту
            var userRequest = new UserRequest
            {
                UserId = userRequestDto.UserId,
                EventType = userRequestDto.EventType,
                PeopleCount = userRequestDto.PeopleCount,
                EventTime = userRequestDto.EventTime.ToUniversalTime(),
                CostTier = userRequestDto.CostTier,
                RequestTime = DateTime.UtcNow
            };

            _context.UserRequests.Add(userRequest);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(500, $"An error occurred while saving: {ex.Message}");
            }
            
            var openAiResponse = "";//                   REALISE REQUESTING TO OPENAI API

            return Ok(new { Response = openAiResponse });
        }


        // PUT: api/UserRequests/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUserRequest(int id, UserRequestDto userRequestDto)
        {
            var existingRequest = await _context.UserRequests.FindAsync(id);
            if (existingRequest == null)
            {
                return NotFound();
            }

            existingRequest.UserId = userRequestDto.UserId;
            existingRequest.EventType = userRequestDto.EventType;
            existingRequest.PeopleCount = userRequestDto.PeopleCount;
            existingRequest.EventTime = userRequestDto.EventTime.ToUniversalTime();
            existingRequest.CostTier = userRequestDto.CostTier;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserRequestExists(id))
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

        // DELETE: api/UserRequests/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUserRequest(int id)
        {
            var userRequest = await _context.UserRequests.FindAsync(id);
            if (userRequest == null)
            {
                return NotFound();
            }

            _context.UserRequests.Remove(userRequest);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool UserRequestExists(int id)
        {
            return _context.UserRequests.Any(e => e.Id == id);
        }
    }
}
