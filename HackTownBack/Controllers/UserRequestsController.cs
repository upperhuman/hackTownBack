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
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RouteApiResponce))]
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
                EventTime = userRequestDto.EventTime?.ToUniversalTime(),
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

            string locationsJson = Locations.GetLocations(userRequestDto.Coords);

            var grokRequestPayload = new
            {
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = $@"Мені потрібно скласти маршрут у структурованому форматі JSON для події. Тип події: {userRequestDto.EventType}. Витрати: {userRequestDto.CostTier} UAH. Кількість людей: {userRequestDto.PeopleCount}. Тривалість події: {userRequestDto.EventTime?.ToString("hh:mm")}. Ось список доступних локацій:
                                
                                {locationsJson}

                                **Важливо**: Поверніть відповідь у форматі JSON наступного вигляду:

                                {{
                                    ""RouteName"": ""Назва маршруту"",
                                    ""BudgetBreakdown"": {{
                                        ""Expenses"": [
                                            {{
                                                ""Name"": ""Кава та торт"",
                                                ""Cost"": 100,
                                                ""Duration"": ""30 minutes"",
                                                ""Description"": ""Романтичний початок із кавою у кафе поблизу.""
                                            }},
                                            {{
                                                ""Name"": ""Прогулянка"",
                                                ""Cost"": 0,
                                                ""Duration"": ""30 minutes"",
                                                ""Description"": ""Прогулянка околицями та спілкування.""
                                            }},
                                            {{
                                                ""Name"": ""Дегустація вина та сиру"",
                                                ""Cost"": 500,
                                                ""Duration"": ""45 minutes"",
                                                ""Description"": ""Розслаблення у винному барі зі смачною дегустацією.""
                                            }},
                                            {{
                                                ""Name"": ""Романтична вечеря"",
                                                ""Cost"": 1000,
                                                ""Duration"": ""1 hour"",
                                                ""Description"": ""Вечеря в затишному ресторані поблизу.""
                                            }}
                                        ]
                                    }},
                                    ""locations"": [
                                        {{
                                            ""Name"": ""Кав'ярня"",
                                            ""Latitude"": 48.465417,
                                            ""Longitude"": 35.053883,
                                            ""Description"": ""Кафе для романтичного початку.""
                                            ""Address"": ""вул. Мостова, 91""
                                        }},
                                        {{
                                            ""Name"": ""Прогулянка"",
                                            ""Latitude"": 48.465500,
                                            ""Longitude"": 35.054000,
                                            ""Description"": ""Прогулянка красивими місцями навколо.""
                                            ""Address"": ""вул. Січових Стрільців, 1""
                                        }},
                                        {{
                                            ""Name"": ""Винний бар"",
                                            ""Latitude"": 48.465600,
                                            ""Longitude"": 35.054500,
                                            ""Description"": ""Винний бар для дегустації.""
                                            ""Address"": ""вул. Олеся Гончара, 2""
                                        }},
                                        {{
                                            ""Name"": ""Ресторан"",
                                            ""Latitude"": 48.465700,
                                            ""Longitude"": 35.055000,
                                            ""Description"": ""Ресторан для вечері.""
                                            ""Address"": ""вул. Січеславська, 2""
                                        }}
                                    ]
                                }}
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

                // Saving responce from grok to db
                userRequest.Response = apiResponse;

                //Parsing responce from grok
                var parsedLocations = ParseJsonResponse(apiResponse);
                if (parsedLocations == null)
                {
                    return StatusCode(500, "Failed to parse the route response.");
                }

                // Creating route
                var eventRoute = new EventRoute
                {
                    RouteName = $"{userRequest.EventType} Route",
                    CreatedAt = DateTime.UtcNow,
                    StepsCount = parsedLocations.Locations.Count
                };

                _context.EventRoutes.Add(eventRoute);
                await _context.SaveChangesAsync(); //Saving route

                //Creating locations
                foreach (var (location, index) in parsedLocations.Locations.Select((loc, idx) => (loc, idx)))
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
                //Saving locations
                await _context.SaveChangesAsync();

                var result = new RouteApiResponce
                {
                    RouteId = eventRoute.Id,
                    RouteName = eventRoute.RouteName,
                };

                return Ok(result);
            }
            else
            {
                return StatusCode(500, "Error occurred while contacting Grok API.");
            }
        }

        private RouteResponse ParseJsonResponse(string jsonResponse)
        {
            try
            {
                // Load JSON into JObject to work with its structure
                var jsonObject = JObject.Parse(jsonResponse);

                // Jump to the desired part of the JSON
                var content = jsonObject["choices"]?[0]?["message"]?["content"]?.ToString();

                if (string.IsNullOrEmpty(content))
                {
                    Console.WriteLine("Content is null or empty.");
                    return null;
                }
                var match = Regex.Match(content, @"```([\s\S]*?)```");
                string cleanedJson = match.Groups[1].Value.Trim();

                // Deserializing content in RouteResponse
                return JsonConvert.DeserializeObject<RouteResponse>(cleanedJson);
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
