using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HackTownBack.Models;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using HackTownBack.Services;
using Microsoft.IdentityModel.Tokens;

namespace HackTownBack.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserRequestsController : ControllerBase
    {
        private readonly HackTownDbContext _context;
        //private readonly WitAiService _witAiService = new WitAiService("GY3JTA6TOO7FPM4JLPUN54GDXJ6CHODC");

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

        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GroupedRoutesResponse))]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult> PostUserRequest(UserRequestDto userRequestDto)
        {
            // Перевірка наявності користувача
            var userExists = await _context.Users.AnyAsync(u => u.Id == userRequestDto.UserId);
            if (!userExists)
            {
                return NotFound($"User with ID {userRequestDto.UserId} does not exist.");
            }

            // Перевірка, чи є параметр `text`
            string requestText;
            if (!string.IsNullOrEmpty(userRequestDto.Text))
            {
                requestText = userRequestDto.Text;
            }
            else
            {
                if (string.IsNullOrEmpty(userRequestDto.EventType) ||
                    userRequestDto.PeopleCount == null ||
                    userRequestDto.CostTier == null)
                {
                    return BadRequest("Either 'text' or 'eventType', 'peopleCount', 'costTier' must be provided.");
                }

                requestText = $@"я хочу отримати маршрут з такими умовами. 
                        Тип події: {userRequestDto.EventType}. 
                        Витрати: {userRequestDto.CostTier} UAH. 
                        Кількість людей: {userRequestDto.PeopleCount}. 
                        Тривалість події: {userRequestDto.EventTime}.";
            }

            // Створення нового запиту
            var userRequest = new UserRequest
            {
                UserId = userRequestDto.UserId,
                EventType = userRequestDto.EventType ?? "",
                PeopleCount = userRequestDto.PeopleCount,
                CostTier = userRequestDto.CostTier,
                RequestTime = DateTime.UtcNow,
                Text = userRequestDto.Text,
                Response = "" // Тимчасово порожнє
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

            // Отримання локацій
            string locationsJson = await LocationsService.GetLocations(userRequestDto.Coords);
            var allLocations = JsonConvert.DeserializeObject<List<LocationDetails>>(locationsJson);

            // Розбиття локацій на частини по 30
            var locationsChunks = allLocations
                .Select((location, index) => new { location, index })
                .GroupBy(x => x.index / 30)
                .Select(g => g.Select(x => x.location).ToList())
                .ToList();

            // Формування запитів
            var tasks = locationsChunks.Select(chunk => GrokService.SendRequestWithRetriesAsync(chunk, requestText));

            // Надсилання запитів
            string[] grokResponses;
            try
            {
                grokResponses = await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed to contact Grok API: {ex.Message}");
            }

            // Об'єднання відповідей
            var combinedResponses = grokResponses.SelectMany(response =>
            {
                try
                {
                    var parsedRoutes = ParseJsonResponse(response);
                    return parsedRoutes ?? new List<RouteResponse>();
                }
                catch
                {
                    return new List<RouteResponse>();
                }
            }).ToList();

            if (!combinedResponses.Any())
            {
                return StatusCode(500, "Failed to parse the route responses.");
            }

            // Збереження маршрутів у базу даних
            var groupId = Guid.NewGuid();
            var routesToSave = new List<EventRoute>();

            foreach (var route in combinedResponses)
            {
                var eventRoute = new EventRoute
                {
                    GroupId = groupId,
                    RouteName = route.RouteName,
                    CreatedAt = DateTime.UtcNow,
                    StepsCount = route.Locations.Count
                };

                routesToSave.Add(eventRoute);
                _context.EventRoutes.Add(eventRoute);
                await _context.SaveChangesAsync();

                foreach (var (location, index) in route.Locations.Select((loc, idx) => (loc, idx)))
                {
                    var dbLocation = new Location
                    {
                        EventId = eventRoute.Id,
                        Name = location.Name,
                        Address = location.Address,
                        Latitude = location.Latitude,
                        Longitude = location.Longitude,
                        Description = location.Description,
                        Type = "PointOfInterest",
                        StepNumber = index + 1
                    };
                    _context.Locations.Add(dbLocation);
                }
            }

            await _context.SaveChangesAsync();

            // Повернення відповіді
            var finalResponse = new GroupedRoutesResponse
            {
                GroupId = groupId,
                Routes = routesToSave.Select(r => new RouteSummary
                {
                    RouteId = r.Id,
                    RouteName = r.RouteName
                }).ToList()
            };

            return Ok(finalResponse);
        }



        private List<RouteResponse> ParseJsonResponse(string jsonResponse)
        {
            try
            {
                var jsonObject = JObject.Parse(jsonResponse);
                var content = jsonObject["choices"]?[0]?["message"]?["content"]?.ToString();

                if (string.IsNullOrEmpty(content))
                {
                    Console.WriteLine("Content is null or empty.");
                    return null;
                }

                var match = Regex.Match(content, @"```(?:json)?([\s\S]*?)```", RegexOptions.IgnoreCase);
                string cleanedJson = match.Groups[1].Value.Trim();

                // Deserializing multiple routes
                return JsonConvert.DeserializeObject<List<RouteResponse>>(cleanedJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse JSON response: {ex.Message}");
                return null;
            }
        }

/*                               ------------ADDICTIONAL FUNCTIONAL ------------------
        [HttpPost("transcribe")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> TranscribeAudio(IFormFile audioFile)
        {
            if (audioFile == null || audioFile.Length == 0)
            {
                return BadRequest("No audio file provided.");
            }

            using var memoryStream = new MemoryStream();
            await audioFile.CopyToAsync(memoryStream);
            var audioBytes = memoryStream.ToArray();

            try
            {
                var text = await _witAiService.TranscribeAudioAsync(audioBytes);
                return Ok(new { Text = text });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Error = ex.Message });
            }
        }
*/

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
            existingRequest.EventTime = DateTime.UtcNow;
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
