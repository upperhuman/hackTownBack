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
            var userExists = await _context.Users.AnyAsync(u => u.Id == userRequestDto.UserId);
            if (!userExists)
            {
                return NotFound($"User with ID {userRequestDto.UserId} does not exist.");
            }

            // Checking if the `text` parameter exists
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

            var userRequest = new UserRequest
            {
                UserId = userRequestDto.UserId,
                EventType = userRequestDto.EventType ?? "",
                PeopleCount = userRequestDto.PeopleCount,
                CostTier = userRequestDto.CostTier,
                RequestTime = DateTime.UtcNow,
                Text = userRequestDto.Text,
                Response = "" // Temporarily empty
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


            // Step 1: Get location descriptions from AI
            var descriptions = await GetLocationDescriptionsAsync(requestText);
            if (descriptions == null || !descriptions.Any())
            {
                return StatusCode(500, "Failed to generate location descriptions from AI.");
            }

            // Step 2: Query Google Places API with descriptions and collect unique locations
            var allLocations = new List<LocationDetails>();
            var placeIdSet = new HashSet<string>();
            string defaultCoords = userRequestDto.Coords ?? "48.465417,35.053883";

            foreach (var description in descriptions)
            {
                var locations = await LocationsService.SearchLocationsByText(description, defaultCoords);
                var topLocations = locations.Take(5).ToList(); // Limit to top 5 per description

                foreach (var location in topLocations)
                {
                    if (!placeIdSet.Contains(location.Place_id))
                    {
                        placeIdSet.Add(location.Place_id);
                        allLocations.Add(location);
                    }
                }
            }

            if (!allLocations.Any())
            {
                return StatusCode(500, "No suitable locations found for the given descriptions.");
            }

            // Step 3: Send locations to AI to build routes
            var locationsChunks = allLocations
                .Select((location, index) => new { location, index })
                .GroupBy(x => x.index / 15) // Chunk into groups of 15
                .Select(g => g.Select(x => x.location).ToList())
                .ToList();

            var tasks = locationsChunks.Select(chunk => GrokService.SendRequestWithRetriesAsync(chunk, requestText));
            string[] grokResponses;
            try
            {
                grokResponses = await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Failed to contact Grok API: {ex.Message}");
            }

            // Combine AI responses
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

            // Save routes to database
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

            // Return response
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

/*                               ------------ADDICTIONAL FUNCTIONAL - VOICE INPUT------------------
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
        private async Task<List<string>> GetLocationDescriptionsAsync(string requestText)
        {
            var prompt = $"Based on the following user preferences for an event: {requestText}, please provide 3-5 descriptions of types of locations that would be suitable for this event. For example, 'a cozy cafe for coffee', 'a scenic park for a walk', etc. Please provide the descriptions in a JSON array like [\"description1\", \"description2\", ...].";

            var grokRequestPayload = new
            {
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                },
                model = "llama3-8b-8192",
                temperature = 0.2
            };

            var response = await GrokService.SendRequestToGrokAsync(grokRequestPayload);
            return ParseDescriptions(response);
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
        // Helper method to parse AI response for descriptions
        private List<string> ParseDescriptions(string jsonResponse)
        {
            try
            {
                var jsonObject = JObject.Parse(jsonResponse);
                var content = jsonObject["choices"]?[0]?["message"]?["content"]?.ToString();
                if (string.IsNullOrEmpty(content))
                {
                    return null;
                }

                // Шукаємо JSON-масив у тексті
                var match = Regex.Match(content, @"\[\s*""(.*?)""\s*\]", RegexOptions.Singleline);
                if (match.Success)
                {
                    string jsonArray = match.Value; // Отримуємо знайдений масив
                    return JsonConvert.DeserializeObject<List<string>>(jsonArray);
                }

                Console.WriteLine("No valid JSON array found in the response.");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse descriptions: {ex.Message}");
                return null;
            }
        }


        // Existing ParseJsonResponse method remains unchanged
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

                return JsonConvert.DeserializeObject<List<RouteResponse>>(cleanedJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse JSON response: {ex.Message}");
                return null;
            }
        }
        private bool UserRequestExists(int id)
        {
            return _context.UserRequests.Any(e => e.Id == id);
        }
    }
}
