using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace HackTownBack.Functionality
{
    public static class Locations
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
                            var locations = results.Select(result => new
                            {
                                Name = result["name"]?.ToString(),
                                Address = result["vicinity"]?.ToString(),
                                Rating = result["rating"]?.ToString(),
                                Type = string.Join(", ", result["types"]?.Select(type => type.ToString()) ?? new List<string>()),
                                PriceLevel = result["price_level"]?.ToString(),
                                Geometry = result["geometry"]?["location"]?.ToString()
                            }).ToList();

                            allLocations.AddRange(locations);
                        }

                        nextPageToken = googleResponse["next_page_token"]?.ToString();

                        // Google API requires a short delay before using the next_page_token
                        if (!string.IsNullOrEmpty(nextPageToken))
                        {
                            await Task.Delay(2000);
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
    }
}
