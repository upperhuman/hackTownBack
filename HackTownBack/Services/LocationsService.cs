using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace HackTownBack.Services
{
    public static class LocationsService
    {
        private const string ApiKey = "AIzaSyCE5WTbBE1wj6sOibVurOLXsPwlVqAQP5U";

        public static async Task<string> GetLocations(string? coords)
        {
            coords = coords ?? "48.465417,35.053883";
            int radius = 1500;
            List<object> allLocations = new List<object>();
            string? nextPageToken = null;

            try
            {
                do
                {
                    string requestUri = $"https://maps.googleapis.com/maps/api/place/nearbysearch/json?location={coords}&radius={radius}&key={ApiKey}"
                                       + (nextPageToken != null ? $"&pagetoken={nextPageToken}" : "");

                    var httpClient = new HttpClient();
                    var response = await httpClient.GetAsync(requestUri);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        var googleResponse = JObject.Parse(responseContent);

                        var results = googleResponse["results"];
                        if (results != null)
                        {
                            foreach (var result in results)
                            {
                                string placeId = result["place_id"]?.ToString();

                                var locationDetails = new LocationDetails
                                {
                                    Place_id = placeId,
                                    Name = result["name"]?.ToString(),
                                    Address = result["vicinity"]?.ToString(),
                                    Rating = result["rating"]?.ToString(),
                                    Type = string.Join(", ", result["types"]?.Select(type => type.ToString()) ?? new List<string>()),
                                    PriceLevel = result["price_level"]?.ToString(),
                                    Geometry = result["geometry"]?["location"]?.ToString(),
                                    Overview = "",
                                    Reviews = new List<object>()
                                };

                                if (!string.IsNullOrEmpty(placeId))
                                {
                                    string placeDetailsJson = await GetPlaceDetails(placeId);
                                    var placeDetails = JsonConvert.DeserializeObject<List<dynamic>>(placeDetailsJson)?.FirstOrDefault();

                                    if (placeDetails != null)
                                    {
                                        locationDetails.Overview = placeDetails.Overview;
                                        locationDetails.Reviews = placeDetails.Reviews.ToObject<List<object>>();
                                    }
                                }

                                allLocations.Add(locationDetails);
                            }
                        }

                        nextPageToken = googleResponse["next_page_token"]?.ToString();

                        if (!string.IsNullOrEmpty(nextPageToken))
                        {
                            await Task.Delay(100);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Failed to fetch locations: {response.StatusCode}");
                        break;
                    }
                } while (!string.IsNullOrEmpty(nextPageToken) && allLocations.Count < 60);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to fetch locations from Google Maps API: {ex.Message}");
            }

            return JsonConvert.SerializeObject(allLocations);
        }

        public static async Task<string> GetPlaceDetails(string placeId)
        {
            List<object> placeDetails = new List<object>();

            try
            {
                string requestUri = $"https://maps.googleapis.com/maps/api/place/details/json?key={ApiKey}&place_id={placeId}";

                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetAsync(requestUri);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent = await response.Content.ReadAsStringAsync();
                        var googleResponse = JObject.Parse(responseContent);

                        var result = googleResponse["result"];
                        if (result != null)
                        {
                            var details = new
                            {
                                Overview = result["editorial_summary"]?["overview"]?.ToString(),
                                Reviews = result["reviews"]?
                                    .Take(2)                            /// -----------------------LIMIT---------------------------------
                                    .Select(review => new
                                    {
                                        Rating = review["rating"]?.ToString(),
                                        Text = review["text"]?.ToString(),
                                        TimeDescription = review["relative_time_description"]?.ToString()
                                    })
                                    .ToList()
                            };

                            placeDetails.Add(details);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Failed to fetch place details: {response.StatusCode}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to fetch place details from Google Maps API: {ex.Message}");
            }

            return JsonConvert.SerializeObject(placeDetails);
        }
    }
    public class LocationDetails
    {
        public string Place_id { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public string Rating { get; set; }
        public string Type { get; set; }
        public string PriceLevel { get; set; }
        public string Geometry { get; set; }
        public string Overview { get; set; }
        public List<object> Reviews { get; set; } = new List<object>();
    }
}
