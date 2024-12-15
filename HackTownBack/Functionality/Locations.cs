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

            try
            {
                string requestUri = $"https://maps.googleapis.com/maps/api/place/nearbysearch/json?location={coords}&radius={radius}&key=AIzaSyCE5WTbBE1wj6sOibVurOLXsPwlVqAQP5U";

                var httpClient = new HttpClient();
                var responce = await httpClient.GetAsync(requestUri);

                if (responce.IsSuccessStatusCode)
                {
                    var responceContent = await responce.Content.ReadAsStringAsync();
                    var googleResponce = JObject.Parse(responceContent);
                    var result = googleResponce["results"];
                    if(result != null)
                    {
                        var locations = result.Select(result => new
                        {
                            Name = result["name"]?.ToString(),
                            Adress = result["vicinity"]?.ToString(),
                            Rating = result["rating"]?.ToString(),
                            Type = string.Join(", ", result["types"]?.Select(type => type.ToString()) ?? new string[0]),
                            PriceLevel = result["price_level"]?.ToString()
                        }
                        ).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to fetch locations from Google Maps API: {ex.Message}");
            }
            return "";
        }
    }
    
}
