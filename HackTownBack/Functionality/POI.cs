using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
class Program
{
    static async Task Main()
    {
        var pois = await OverpassService.GetFilteredPOIs();

        if (pois.Count == 0)
        {
            Console.WriteLine("Не найдено ни одной точки интереса.");
        }

        foreach (var poi in pois)
        {
            Console.WriteLine($"{poi.Name} - {poi.Type} ({poi.Lat}, {poi.Lon})");
        }
    }
}

public class POI
{
    public string Name { get; set; }
    public string Type { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }
}


public static class OverpassService
{
    private const string OverpassUrl = "http://overpass-api.de/api/interpreter";

    public static async Task<List<Location>> GetFilteredPOIs()
    {
        string query = @"
        [out:json];
        area[name=""Дніпропетровська область""]->.searchArea;
        (
          node(area.searchArea)[""amenity""];
          way(area.searchArea)[""amenity""];
          relation(area.searchArea)[""amenity""];
        );
        out center;";

        using (HttpClient client = new HttpClient())
        {
            string requestUrl = $"{OverpassUrl}?data={Uri.EscapeDataString(query)}";
            var response = await client.GetAsync(requestUrl);

            if (!response.IsSuccessStatusCode)
                throw new Exception($"Ошибка запроса: {response.StatusCode}");

            var jsonString = await response.Content.ReadAsStringAsync();
            JObject data = JObject.Parse(jsonString);

            // Достаем список мест
            var locations = data["elements"]?
                .Where(el => el["tags"]?["amenity"] != null)
                .Select(el => new Location
                {
                    Name = el["tags"]?["name:uk"]?.ToString() ?? el["tags"]?["name:ru"]?.ToString() ?? el["tags"]?["name:en"]?.ToString() ?? el["tags"]?["name"]?.ToString() ?? "Без назви",
                    Type = el["tags"]?["amenity"]?.ToString() ?? "Неизвестно",
                    Lat = el["lat"]?.ToObject<double>() ?? el["center"]?["lat"]?.ToObject<double>() ?? 0,
                    Lon = el["lon"]?.ToObject<double>() ?? el["center"]?["lon"]?.ToObject<double>() ?? 0
                })
                .Where(loc => loc.Type != "place_of_worship" && loc.Type != "pharmacy" && loc.Type != "fuel" && loc.Type != "school"
                && loc.Type != "parking" && loc.Type != "hospital" && loc.Type != "music_school" && loc.Type != "kindergarten"
                && loc.Type != "university" && loc.Type != "prison" && loc.Type != "fire_station" && loc.Type != "social_facility"
                && loc.Type != "bank" && loc.Type != "college" && loc.Type != "public_building" && loc.Type != "doctors"
                && loc.Type != "parking_space" && loc.Type != "clinic" && loc.Type != "shelter" && loc.Type != "toilets"
                && loc.Type != "atm" && loc.Type != "bench" && loc.Type != "bicycle_parking" && loc.Type != "car_wash"
                && loc.Type != "waste_disposal" && loc.Type != "veterinary" && loc.Type != "police" && loc.Type != "post_office")
                .ToList();

            return locations ?? new List<Location>();
        }
    }
}

public class Location
{
    public string Name { get; set; }
    public string Type { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }
}