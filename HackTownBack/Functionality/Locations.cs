namespace HackTownBack.Functionality
{
    public static class Locations
    {
        public static string GetLocations(string? coords)
        {
            coords = coords ?? "48.465417,35.053883";
            try
            {

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to fetch locations from Google Maps API: {ex.Message}");
            }
            return "";
        }
    }
    
}
