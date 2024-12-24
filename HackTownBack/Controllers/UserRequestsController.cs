using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HackTownBack.Models;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using HackTownBack.Functionality;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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
            var userExists = await _context.Users.AnyAsync(u => u.Id == userRequestDto.UserId);
            if (!userExists)
            {
                return NotFound($"User with ID {userRequestDto.UserId} does not exist.");
            }

            // Creating new request
            var userRequest = new UserRequest
            {
                UserId = userRequestDto.UserId,
                EventType = userRequestDto.EventType,
                PeopleCount = userRequestDto.PeopleCount,
                EventTime = DateTime.UtcNow,//userRequestDto.EventTime?.ToUniversalTime(),
                CostTier = userRequestDto.CostTier,
                RequestTime = DateTime.UtcNow,
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

            string locationsJson = await Locations.GetLocations(userRequestDto.Coords);

            var grokRequestPayload = new
            {
                messages = new[]
    {
        new
        {
            role = "user",
            content = $@"Мені потрібно скласти до 4-х різних маршрутів у структурованому форматі JSON для події. Тип події: {userRequestDto.EventType}. Витрати: {userRequestDto.CostTier} UAH. Кількість людей: {userRequestDto.PeopleCount}. Тривалість події: {userRequestDto.EventTime}. Ось список доступних локацій:

                            {locationsJson.Take(50)/* ------limit----- */}
                            **Важливо**: Поверніть відповідь у форматі JSON наступного вигляду:
                            [
                                {{
                                    ""RouteName"": ""фільмп→прогулянка→кав'ярня"",
                                    ""BudgetBreakdown"": {{
                                        ""Expenses"": [
                                            {{
                                                ""Name"": ""Кава та торт"",
                                                ""Cost"": 100,
                                                ""Duration"": ""30 minutes"",
                                                ""Description"": ""Романтичний початок із кавою у кафе поблизу.""
                                            }}
                                        ]
                                    }},
                                    ""locations"": [
                                        {{
                                            ""Name"": ""Кав'ярня"",
                                            ""Latitude"": 48.465417,
                                            ""Longitude"": 35.053883,
                                            ""Description"": ""Кафе для романтичного початку."",
                                            ""Address"": ""вул. Мостова, 91""
                                        }}
                                    ]
                                }},
                                ...
                            ]
                            "
        }
    },
                model = "llama3-8b-8192",
                temperature = 0.2
            };


            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "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");

            var jsonPayload = JsonConvert.SerializeObject(grokRequestPayload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("https://api.groq.com/openai/v1/chat/completions", content);

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = await response.Content.ReadAsStringAsync();

                // Saving response from Grok to DB
                userRequest.Response = apiResponse;

                var parsedRoutes = ParseJsonResponse(apiResponse);
                if (parsedRoutes == null || !parsedRoutes.Any())
                {
                    return StatusCode(500, "Failed to parse the route response.");
                }

                var groupId = Guid.NewGuid();
                var routesToSave = new List<EventRoute>();

                foreach (var route in parsedRoutes)
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
                    await _context.SaveChangesAsync(); // Saving route

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
            else
            {
                return StatusCode(500, "Error occurred while contacting Grok API.");
            }
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
