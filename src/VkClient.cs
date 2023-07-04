namespace VkLib
{
    public class VkClient
    {
        public const long const_peer_id = 2000000000;
        private readonly HttpClient _client = new HttpClient();
        private readonly string _api = "https://api.vk.com/method/";

        public string access_token {get; init;}
        public string version {get; init;}
        public long group_id {get; set;}

        public VkClient(string access_token, string version, long group_id)
        {
            this.access_token = access_token;
            this.version = version;
            this.group_id = group_id;
        }

        /// <summary>
        /// build a query string in URL
        /// </summary>
        private static string DictToQuery(Dictionary<string, string> dict)
        {
            var builder = new StringBuilder("?");
            foreach (var item in dict)
            {
                builder.
                Append(item.Key).Append('=').
                Append(item.Value).Append('&');
            }
            return builder.Remove(builder.Length - 1, 1).ToString();
        }

        /// <summary>
        /// create query value string from list
        /// </summary>
        private static string ListToQueryValue<T>(List<T> list)
        {
            var builder = new StringBuilder();
            foreach (var id in list)
            {
                builder.Append(id?.ToString());
                builder.Append(",");
            }
            return builder.ToString();
        }

        /// <summary>
        /// calls VK API method
        /// </summary>
        public async Task<JsonNode?> GetMethodAsync(string method, Dictionary<string, string> query)
        {
            // query string build
            query.Add("access_token", access_token);
            query.Add("v", version!);

            // uri build
            var builder = new UriBuilder(_api);
            builder.Path += method;
            builder.Query = DictToQuery(query);
            var uri = builder.Uri;

            // Get response 
            JsonNode? response = await _client.GetFromJsonAsync<JsonNode>(uri);
            return response;
        }

        /// <summary>
        /// messages.send
        /// </summary>
        public async Task<JsonNode?> MessagesSendAsync 
            (long peer_id, 
            string? message, 
            string? forward = null,
            string? attachment = null,
            long disable_mentions = 1, 
            long random_id = 0)
        {
            if(random_id == 0) random_id = new Random().Next();

			string replacement = "?#%&+-*";

            // query build
            var query = new Dictionary<string, string>();
            query.Add("peer_id", peer_id.ToString());
            if(message is not null)
			{
				StringBuilder new_message = new StringBuilder();
				for(int i = 0; i < message.Length; i++) 
					if(replacement.Contains(message[i]))
						new_message.Append("%").Append(((int)message[i]).ToString("X"));
					else
						new_message.Append(message[i]);
						
				query.Add("message", new_message.ToString());
			}
            if(forward is not null) query.Add("forward", forward);
            if(attachment is not null) query.Add("attachment", attachment);
            query.Add("disable_mentions", disable_mentions.ToString());
            query.Add("random_id", random_id.ToString());
            
            // set method name
            string method = "messages.send";

            // Get response 
            var response = await GetMethodAsync(method, query);
            return response;          
        }		

        /// <summary>
        /// messages.removehatUser
        /// </summary>
        public async Task<JsonNode?> MessagesRemoveChatUserAsync (long chat_id, long user_id)
        {
            // query build
            var query = new Dictionary<string, string>();
            query.Add("chat_id", chat_id.ToString());
            query.Add("user_id", user_id.ToString());
            
            // set method name
            string method = "messages.removeChatUser";

            // Get response 
            var response = await GetMethodAsync(method, query);
            return response;
        }

        /// <summary>
        /// messages.delete
        /// </summary>
        public async Task<JsonNode?> MessagesDeleteAsync (long peer_id, 
        List<long>? message_ids = null,
        List<long>? cmids = null,
        long delete_for_all = 1)
        {
            // query build
            var query = new Dictionary<string, string>();
            query.Add("peer_id", peer_id.ToString());
            query.Add("delete_for_all", delete_for_all.ToString());

            if(message_ids is not null)
                query.Add("message_ids", ListToQueryValue(message_ids));

            if(cmids is not null)
                query.Add("cmids", ListToQueryValue(cmids));
            
            // set method name
            string method = "messages.delete";

            // Get response 
            var response = await GetMethodAsync(method, query);
            return response;
        }
    
        /// <summary>
        /// messages.getChat
        /// </summary>
        public async Task<JsonNode?> MessagesGetConversationMembersAsync (long peer_id)
        {
            // query build
            var query = new Dictionary<string, string>();
            query.Add("peer_id", peer_id.ToString());

            // set method name
            string method = "messages.getConversationMembers";

            // Get response 
            var response = await GetMethodAsync(method, query);
            return response;
        }

		public async Task<JsonNode?> PhotosGetMessagesUploadServerAsync (long peer_id)
		{
			// query build
            var query = new Dictionary<string, string>();
            query.Add("peer_id", peer_id.ToString());

            // set method name
            string method = "photos.getMessagesUploadServer";

            // Get response 
            var response = await GetMethodAsync(method, query);
            return response;
		}

		public async Task<JsonNode?> PhotosSaveMessagesPhotoAsync (string photo, long server, string hash)
		{
			// query build
            var query = new Dictionary<string, string>();
            query.Add("photo", photo);
			query.Add("server", server.ToString());
			query.Add("hash", hash);

            // set method name
            string method = "photos.saveMessagesPhoto";

            // Get response 
            var response = await GetMethodAsync(method, query);
            return response;
		}

		

        public async Task<JsonNode?> ExecuteAsync (string code)
        {
            // query build
            var query = new Dictionary<string, string>();
            query.Add("code", code);

            // set method name
            string method = "execute";

            // Get response 
            var response = await GetMethodAsync(method, query);
            return response;
        }

        public async Task<JsonNode?> ExecuteAsync (List<IExecuteCode> codes)
        {
            // счетчик запросов к API
            long i = 0;
            var sb = new StringBuilder();

            foreach(var code in codes)
            {
                sb.Append(code.ToCode());
                i++;
                
                // каждые 24 запроса отправляем код
                if(i > 24 || code == codes.Last())
                {
                    i = 0;
                    await ExecuteAsync(sb.ToString());
                    sb.Clear();
                }
            }
            return null;
        }
    }

    public interface IExecuteCode
    {
        public string ToCode();
    }

    public class MessageExecuteCode : IExecuteCode
    {
        public string message {get; set;}
        public long peer_id {get; set;}
        
        public MessageExecuteCode(string message, long peer_id)
        {
            this.message = message;
            this.peer_id = peer_id;
        }

        public string ToCode()
        {
            int random_id = new Random().Next();
            var sb = new StringBuilder();

            sb.Append("API.messages.send")
                .Append("({")
                .Append($"\"peer_id\":{peer_id},")
                .Append($"\"random_id\":{random_id},")
                .Append($"\"message\":\"{message}\"")
                .Append("});");
            return sb.ToString();
        }
    }

    public class KickExecuteCode : IExecuteCode
    {
        public long chat_id {get; set;}
        public long user_id {get; set;}
        
        public KickExecuteCode(long chat_id, long user_id)
        {
            this.chat_id = chat_id;
            this.user_id = user_id;
        }

        public string ToCode()
        {
            var sb = new StringBuilder();

            sb.Append("API.messages.removeChatUser")
                .Append("({")
                .Append($"\"chat_id\":{chat_id},")
                .Append($"\"user_id\":{user_id},")
                .Append("});");
            return sb.ToString();
        }
    }
}
