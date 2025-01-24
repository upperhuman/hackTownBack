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
    }
}
