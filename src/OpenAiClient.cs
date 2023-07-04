namespace OpenAi
{
	public class OpenAiClient
	{
		private readonly HttpClient _client = new();

		private const string base_url = "https://api.openai.com/v1/";

		public OpenAiClient()
		{
			_client.Timeout = TimeSpan.FromSeconds(180);
		}

		public async Task<JsonNode?> PostAsJsonAsync<T>(T request, string url, string openai_token)
		{
			var full_url = base_url + url;
			var requestMessage = new HttpRequestMessage(HttpMethod.Post, full_url);
			requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", openai_token);
			requestMessage.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
			requestMessage.Content = JsonContent.Create<T>(request);

			HttpResponseMessage response;

			// ловим таймаут
			try
			{
				response = await _client.SendAsync(requestMessage);
			}
			catch
			{
				return null;
			}

			if(!response.IsSuccessStatusCode) return null;
			return await response.Content.ReadFromJsonAsync<JsonNode?>();
		}
	}

	public class ImageClient
	{
		private readonly HttpClient _client = new();

		public async Task<byte[]?> DownloadAsync(string url)
		{
			return await _client.GetByteArrayAsync(url);
		}

		public async Task<JsonNode?> UploadAsync(string url, byte[] img)
		{
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			MultipartFormDataContent content = new MultipartFormDataContent();		
			content.Add(new ByteArrayContent(img), "photo", DateTime.Now.ToString() + ".png");

			HttpResponseMessage response = await _client.PostAsync(url, content);
			return await response.Content.ReadFromJsonAsync<JsonNode?>();
		}
	}
}