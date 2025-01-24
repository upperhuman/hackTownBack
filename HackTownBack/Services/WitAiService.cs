using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace HackTownBack.Services
{
    public class WitAiService
    {
        private readonly string _witAiToken;

        public WitAiService(string witAiToken)
        {
            _witAiToken = witAiToken;
        }

        public async Task<string> TranscribeAudioAsync(byte[] audioBytes)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_witAiToken}");

            using var content = new ByteArrayContent(audioBytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/ogg");

            var response = await client.PostAsync("https://api.wit.ai/speech", content);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Wit.ai API call failed: {response.StatusCode}");
            }

            var jsonResponse = await response.Content.ReadAsStringAsync();
            dynamic result = JsonConvert.DeserializeObject(jsonResponse);
            return result.text;
        }
    }
}
