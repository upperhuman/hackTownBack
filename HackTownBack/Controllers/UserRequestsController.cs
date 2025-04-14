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
            // Check if the user exists
            var userExists = await _context.Users.AnyAsync(u => u.Id == userRequestDto.UserId);
            if (!userExists)
            {
                return NotFound($"User with ID {userRequestDto.UserId} does not exist.");
            }

            // Check if either text or the necessary event parameters are provided
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

                if (userRequestDto.PeopleCount <= 0)
                {
                    return BadRequest("People count must be a positive number.");
                }
                if (userRequestDto.CostTier <= 0)
                {
                    return BadRequest("Cost tier must be a positive number.");
                }

                requestText = $@"я хочу отримати маршрут з такими умовами. 
                        Тип події: {userRequestDto.EventType}. 
                        Витрати: {userRequestDto.CostTier} UAH. 
                        Кількість людей: {userRequestDto.PeopleCount}. 
                        Тривалість події: {userRequestDto.EventTime}.";
            }

            // Create the new request
            var userRequest = new UserRequest
            {
                UserId = userRequestDto.UserId,
                EventType = userRequestDto.EventType ?? "",
                PeopleCount = userRequestDto.PeopleCount,
                CostTier = userRequestDto.CostTier,
                RequestTime = DateTime.UtcNow,
                Text = userRequestDto.Text,
                Response = "" // Initially empty
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

            var tasks = GeminiService.SendRequestWithRetriesAsync(requestText, userRequestDto.Coords ?? "48.465417,35.053883");

            // Send requests to Gemini API
            string[] geminiResponses;
            try
            {
                geminiResponses = await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("rate_limit"))
                {
                    return StatusCode(429, "Too many requests, please try again later.");
                }
                else if (ex.Message.Contains("UnavailableForLegalReasons"))
                {
                    return StatusCode(451, "The requested service is unavailable due to legal reasons.");
                }
                else
                {
                    return StatusCode(500, $"Failed to contact Gemini API: {ex.Message}");
                }
            }

            // Combine responses
            var combinedResponses = geminiResponses.SelectMany(response =>
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

            // Save routes to the database
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

            // Return final response
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
                if (string.IsNullOrWhiteSpace(jsonResponse))
                {
                    Console.WriteLine("Error: Empty or null response from Gemini API.");
                    return null;
                }

                Console.WriteLine($"Raw Gemini response: {jsonResponse}");

                JObject jsonObject;
                try
                {
                    jsonObject = JObject.Parse(jsonResponse);
                }
                catch (JsonReaderException ex)
                {
                    Console.WriteLine($"JSON Parsing Error: {ex.Message}, StackTrace: {ex.StackTrace}");
                    return null;
                }

                var contentToken = jsonObject.SelectToken("candidates[0].content.parts[0].text");
                if (contentToken == null)
                {
                    Console.WriteLine("Error: Missing or empty 'content' field in response.");
                    return null;
                }

                string content = contentToken.ToString();

                try
                {
                    var match = Regex.Match(content, @"```(?:json)?([\s\S]*?)```", RegexOptions.IgnoreCase);
                    string cleanedJson = match.Groups[1].Value.Trim();

                    return JsonConvert.DeserializeObject<List<RouteResponse>>(cleanedJson);
                }
                catch (JsonSerializationException ex)
                {
                    Console.WriteLine($"Deserialization Error: {ex.Message}, StackTrace: {ex.StackTrace}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error while parsing Gemini response: {ex.Message}, StackTrace: {ex.StackTrace}");
                return null;
            }
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
