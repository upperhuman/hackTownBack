using HackTownBack.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;
using HackTownBack.Controllers;

namespace HackTownBack.Services
{
    public static class GrokService
    {
        public static async Task<string> SendRequestToGrokAsync(Object grokRequestPayload)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "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");

            var jsonPayload = JsonConvert.SerializeObject(grokRequestPayload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("https://api.groq.com/openai/v1/chat/completions", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error occurred while contacting Grok API: {response.StatusCode}, Details: {errorMessage}");
            }

            return await response.Content.ReadAsStringAsync();
        }

        public static async Task<string> SendRequestWithRetriesAsync(List<LocationDetails> locations, string requestText)
        {
            int maxAttempts = 5;
            int attempt = 0;

            while (attempt < maxAttempts)
            {
                try
                {
                    var grokRequestPayload = new
                    {
                        messages = new[]
                        {
                            new
                            {
                                role = "user",
                                content = $@"Потрібно скласти до 3-х різних маршрутів у структурованому форматі JSON для події враховуючи побажання юзера: {requestText}

                                        Ось список доступних локацій(данні отримані з google maps api):
                                        {JsonConvert.SerializeObject(locations)} 
                                        **Важливо**: Поверніть відповідь у форматі JSON наступного вигляду, якщо у списку не буде підходячих локацій - поверніть пустий масив:
                                        [
                                            {{
                                                ""RouteName"": ""фільм→прогулянка→кав'ярня"",
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
                                                ""Locations"": [
                                                    {{
                                                        ""Name"": ""Кав'ярня"",
                                                        ""Latitude"": 48.465417,
                                                        ""Longitude"": 35.053883,
                                                        ""Description"": ""Кафе для романтичного початку."",
                                                        ""Address"": ""вул. Мостова, 91""
                                                    }}
                                                ]
                                            }}
                                        ]
                                        "
                            }
                        },
                        model = "llama3-8b-8192",
                        temperature = 0.2
                    };

                    return await GrokService.SendRequestToGrokAsync(grokRequestPayload);
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("rate_limit_exceeded") && locations.Count > 10)
                    {
                        locations = locations.Take(locations.Count - 10).ToList();
                        attempt++;
                        Console.WriteLine($"[Retry {attempt}/{maxAttempts}] Request too large, reducing locations to {locations.Count}...");
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            throw new Exception("Failed to send request after multiple attempts.");
        }
    }
}
