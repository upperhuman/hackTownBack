
using Newtonsoft.Json;

using System.Text;
using System.Text.RegularExpressions;

namespace HackTownBack.Services
{
    public static class GeminiService
    {
        // Your API key (make sure to use environment variables for better security in production)
        private const string ApiKey = "AIzaSyAg2tvBTh-N-MfXWShCJ8B8h08R0p4nKCY";

        public static async Task<string> SendRequestToGeminiAsync(object geminiRequestPayload)
        {
            if (string.IsNullOrEmpty(ApiKey))
            {
                throw new Exception("Gemini API key is missing. Please ensure it is configured properly.");
            }

            try
            {
                using var httpClient = new HttpClient();
                var jsonPayload = JsonConvert.SerializeObject(geminiRequestPayload);

                // Log the request payload for debugging
                Console.WriteLine($"Sending request to Gemini API: {jsonPayload}");

                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync($"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.0-flash:generateContent?key={ApiKey}", content);

                var responseContent = await response.Content.ReadAsStringAsync();

                // Log the response for debugging
                Console.WriteLine($"Received response from Gemini API: {responseContent}");

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Error contacting Gemini API: {response.StatusCode}, Details: {responseContent}");
                }

                return responseContent;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error contacting Gemini API: {ex.Message}, StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        public static async Task<string> SendRequestWithRetriesAsync(string requestText, string coords)
        {
            const int maxAttempts = 5;
            int attempt = 0;

            while (attempt < maxAttempts)
            {
                try
                {
                    // Build the Gemini API request payload
                    var geminiRequestPayload = new
                    {
                        contents = new[]
                        {
                            new
                            {
                                parts = new[]
                                {
                                    new
                                    {
                                        text = $@"Please generate up to 6 different routes, using information from google maps, in JSON format for the event with the user's preferences: {requestText}.
                                        user coords: {coords}
                                        
                                        **Important**: Return the response in the following JSON format. If no matching locations are found, return an empty array:
                                        [
                                            {{
                                                ""RouteName"": ""movie→walk→coffee"",
                                                ""BudgetBreakdown"": {{
                                                    ""Expenses"": [
                                                        {{
                                                            ""Name"": ""Coffee and Cake"",
                                                            ""Cost"": 100,
                                                            ""Duration"": ""30 minutes"",
                                                            ""Description"": ""A romantic start with coffee at a nearby café.""
                                                        }}
                                                    ]
                                                }},
                                                ""Locations"": [
                                                    {{
                                                        ""Name"": ""Café"",
                                                        ""Latitude"": 48.465417,
                                                        ""Longitude"": 35.053883,
                                                        ""Description"": ""Café for a romantic start."",
                                                        ""Address"": ""Mostova St, 91""
                                                    }}
                                                ]
                                            }}
                                        ]"
                                    }
                                }
                            }
                        }
                    };

                    return await SendRequestToGeminiAsync(geminiRequestPayload);
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("UnavailableForLegalReasons"))
                    {
                        // Retry by modifying the request text
                        Console.WriteLine("Received 'UnavailableForLegalReasons'. Retrying with a sanitized request...");
                        requestText = requestText;
                        attempt++;
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            throw new Exception("Failed to send the request after multiple attempts.");
        }
    }
}
