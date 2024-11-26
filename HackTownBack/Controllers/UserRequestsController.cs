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
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RouteResponse))] // Swagger буде використовувати RouteResponse
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> PostUserRequest(UserRequestDto userRequestDto)
        {
            // Перевірка чи існує користувач
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
                EventTime = userRequestDto.EventTime?.ToUniversalTime(),
                CostTier = userRequestDto.CostTier,
                RequestTime = DateTime.UtcNow,
                Response = ""
            };

            _context.UserRequests.Add(userRequest);

            try
            {
                await _context.SaveChangesAsync(); // Збереження UserRequest перед створенням маршруту
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(500, $"An error occurred while saving: {ex.Message}");
            }

            // Створюємо API-відповідь із фіктивними даними 
            var apiResponse = new List<LocationDto>
            {
                new LocationDto { Name = "Park A", Address = "123 Main St", Description = "A lovely park with fountains." },
                new LocationDto { Name = "Museum B", Address = "456 Elm St", Description = "Historical museum with art exhibits." }
            };

            // Створення маршруту
            var eventRoute = new EventRoute
            {
                RouteName = $"{userRequest.EventType} Route",
                CreatedAt = DateTime.UtcNow,
                StepsCount = apiResponse.Count
            };

            _context.EventRoutes.Add(eventRoute);
            await _context.SaveChangesAsync(); // Зберігаємо маршрут для отримання його ID

            // Прив'язуємо маршрут до запиту
            userRequest.EventRoutesId = eventRoute.Id;
            _context.UserRequests.Update(userRequest);

            // Додаємо локації до маршруту
            foreach (var location in apiResponse)
            {
                var locationEntity = new Location
                {
                    EventId = eventRoute.Id,
                    Name = location.Name,
                    Address = location.Address,
                    Description = location.Description,
                    StepNumber = apiResponse.IndexOf(location) + 1,
                    Type = "PointOfInterest", // Приклад типу
                    Latitude = 0, // Заглушка
                    Longitude = 0 // Заглушка
                };
                _context.Locations.Add(locationEntity);
            }

            try
            {
                await _context.SaveChangesAsync(); // Збереження локацій
            }
            catch (DbUpdateException ex)
            {
                return StatusCode(500, $"An error occurred while saving locations: {ex.Message}");
            }

            // Формуємо спрощену відповідь
            var result = new RouteResponse
            {
                RouteId = eventRoute.Id,
                Locations = apiResponse
            };

            return Ok(result);
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
            existingRequest.EventTime = userRequestDto.EventTime?.ToUniversalTime();
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
