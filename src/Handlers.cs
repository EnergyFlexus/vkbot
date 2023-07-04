namespace VkLib
{
    /// <summary>
    /// делегат обработчика
    /// </summary>
    public delegate Task<bool> HandleDelegate(VkUpdate vkUpdate, VkClient vkClient, VkDbContext vkDbContext);

    /// <summary>
    /// обработчик
    /// </summary>
    public abstract class Handler
    {
        public Handler? next {get; set;}
        public Handler(Handler? next = null) =>
            this.next = next;

        public abstract Task HandleAsync (VkUpdate vkUpdate, VkClient vkClient, VkDbContext vkDbContext);
    }

	/// <summary>
	/// проверяет, что собыите - новое непустое сообщение, которое начинается с точки
	/// </summary>
	public class MessageNewHandler : Handler 
	{
		public override async Task HandleAsync(VkUpdate vkUpdate, VkClient vkClient, VkDbContext vkDbContext)
		{
			if(vkUpdate.type != VkUpdate.message_new) return;

			var obj = (VkMessageNewObject)vkUpdate.obj;
			var msg = (VkMessage)obj.message!;

			var peer_id = msg.peer_id;
			var user_id = msg.from_id;
			var msg_id = msg.conversation_message_id;
			
			if(user_id < 0) return;

			var text = msg.text;

			if(text is null) return;
			if(text.Length == 0) return;

			if(next is not null) await next.HandleAsync(vkUpdate, vkClient, vkDbContext);
		}
	}

	public class TechCommandsHandler : Handler 
	{
		public Dictionary<string, Handler> handlers {get; set;} = new();

		public TechCommandsHandler() {}

		public TechCommandsHandler(Dictionary<string, Handler> handlers) 
			=> this.handlers = handlers;

		public override async Task HandleAsync(VkUpdate vkUpdate, VkClient vkClient, VkDbContext vkDbContext)
		{
			var obj = (VkMessageNewObject)vkUpdate.obj;
			var msg = (VkMessage)obj.message!;

			var peer_id = msg.peer_id;
			var user_id = msg.from_id;
			var msg_id = msg.conversation_message_id;
			var text = msg.text!;

			string[] words = text.Split(' ');
			string command = words[0];

			// проверяем доступ к командам
			if(peer_id > VkClient.const_peer_id) return;
			if(!handlers.ContainsKey(command)) return;

			TechUser? tech_user = await vkDbContext.tech_users.FindAsync(user_id);
			if(tech_user is null || !tech_user.is_tech) return;

			await handlers[command].HandleAsync(vkUpdate, vkClient, vkDbContext);
			if(next is not null) await next.HandleAsync(vkUpdate, vkClient, vkDbContext);
		}
	}

	public class TechInfoHandler : Handler
	{
		public override async Task HandleAsync (VkUpdate vkUpdate, VkClient vkClient, VkDbContext vkDbContext)
		{
			var obj = (VkMessageNewObject)vkUpdate.obj;
			var msg = (VkMessage)obj.message!;

			var peer_id = msg.peer_id;
			var user_id = msg.from_id;
			var msg_id = msg.conversation_message_id;

			string forward = Parser.ToForwardString(peer_id, new List<long> () {msg_id}, true);
			await vkClient.MessagesSendAsync(peer_id, peer_id.ToString(), forward);
		}
	}

	public class TechHelpHandler : Handler
	{
		public override async Task HandleAsync (VkUpdate vkUpdate, VkClient vkClient, VkDbContext vkDbContext)
		{
			var obj = (VkMessageNewObject)vkUpdate.obj;
			var msg = (VkMessage)obj.message!;

			var peer_id = msg.peer_id;
			var user_id = msg.from_id;
			var msg_id = msg.conversation_message_id;

			var text = msg.text!;

			string[] words = text.Split(' ');
			if(words.Length < 2) return;

			string command = words[0];
			string new_help = text.Substring(command.Length + 1, text.Length - (command.Length + 1));

			var info = await vkDbContext.tech_info.FindAsync((long)1);
			info!.help_message = new_help;

			string forward = Parser.ToForwardString(peer_id, new List<long> () {msg_id}, true);
			await vkClient.MessagesSendAsync(peer_id, "Done!", forward);
		}
	}

	public class TechCustomTokenHandler : Handler
	{
		public override async Task HandleAsync (VkUpdate vkUpdate, VkClient vkClient, VkDbContext vkDbContext)
		{
			var obj = (VkMessageNewObject)vkUpdate.obj;
			var msg = (VkMessage)obj.message!;

			var user_id = msg.from_id;
			var peer_id = msg.peer_id;
			var msg_id = msg.conversation_message_id;

			var text = msg.text!;

			string[] words = text.Split(' ');
			if(words.Length < 3) return;

			string command = words[0];
			long token_peer_id = 0;

			Int64.TryParse(words[1], out token_peer_id);
			if(token_peer_id == 0) return;

			string custom_token = words[2];

			var peer = await vkDbContext.peers.FindAsync(token_peer_id);
			if(peer is null)
			{
				peer = new Peer(token_peer_id);
				await vkDbContext.peers.AddAsync(peer);
			}
			if(custom_token == "null")
				peer!.custom_token = null;
			else
				peer!.custom_token = custom_token;

			string forward = Parser.ToForwardString(peer_id, new List<long> () {msg_id}, true);
			await vkClient.MessagesSendAsync(peer_id, $"Custom token of {token_peer_id} changed.", forward);
		}
	}

	public class TechPublicTokenHandler : Handler
	{
		public override async Task HandleAsync (VkUpdate vkUpdate, VkClient vkClient, VkDbContext vkDbContext)
		{
			var obj = (VkMessageNewObject)vkUpdate.obj;
			var msg = (VkMessage)obj.message!;

			var user_id = msg.from_id;
			var peer_id = msg.peer_id;
			var msg_id = msg.conversation_message_id;

			var text = msg.text!;

			string[] words = text.Split(' ');
			if(words.Length < 2) return;

			string command = words[0];
			string public_token = words[1];

			var info = await vkDbContext.tech_info.FindAsync((long)1);
			info!.public_token = public_token;

			string forward = Parser.ToForwardString(peer_id, new List<long> () {msg_id}, true);
			await vkClient.MessagesSendAsync(peer_id, "Public token changed.", forward);
		}
	}

	public class AntiSpamHandler : Handler
	{
		private readonly HashSet<long> _users = new();
		private readonly object _lock_object = new();

		public string message {get; set;} = "";

		public override async Task HandleAsync(VkUpdate vkUpdate, VkClient vkClient, VkDbContext vkDbContext)
		{
			var obj = (VkMessageNewObject)vkUpdate.obj;
			var msg = (VkMessage)obj.message!;

			var peer_id = msg.peer_id;
			var msg_id = msg.conversation_message_id;

			bool contains = false;
			lock(_lock_object)
			{
				contains = _users.Contains(peer_id);
			}

			if(contains)
			{
				string forward = Parser.ToForwardString(peer_id, new List<long> () {msg_id}, true);
				await vkClient.MessagesSendAsync(peer_id, message, forward);
				return;
			}

			lock(_lock_object)
			{
				_users.Add(peer_id);
			}

			try
			{
				if(next is not null) await next.HandleAsync(vkUpdate, vkClient, vkDbContext);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
			}
			finally
			{
				lock(_lock_object)
				{
					_users.Remove(peer_id);
				}
			}
		}
	}

	public class CommandsHandler : Handler
	{
		public Dictionary<string, Handler> handlers {get; set;} = new();
		public string unknown_command {get; set;} = "";

		public CommandsHandler() {}

		public CommandsHandler(Dictionary<string, Handler> handlers) 
			=> this.handlers = handlers;

		public override async Task HandleAsync(VkUpdate vkUpdate, VkClient vkClient, VkDbContext vkDbContext)
		{
			var obj = (VkMessageNewObject)vkUpdate.obj;
			var msg = (VkMessage)obj.message!;

			var peer_id = msg.peer_id;
			var user_id = msg.from_id;
			var msg_id = msg.conversation_message_id;
			var text = msg.text!;

			string[] words = text.Split(' ');
			string command = words[0];

			if(!handlers.ContainsKey(command))
			{
				if(peer_id < VkClient.const_peer_id && command.IndexOf(".tech") == -1)
				{
					string forward = Parser.ToForwardString(peer_id, new List<long> () {msg_id}, true);
					await vkClient.MessagesSendAsync(peer_id, unknown_command, forward);
				}
				return;
			}

			// проверяем доступ к командам
			if(peer_id > VkClient.const_peer_id)
			{
				ChatUser? user = await vkDbContext.chat_users.FindAsync(user_id, peer_id);
				if(user is null || !user.is_allowed) return;
			}

			await handlers[command].HandleAsync(vkUpdate, vkClient, vkDbContext);
			if(next is not null) await next.HandleAsync(vkUpdate, vkClient, vkDbContext);
		}
	}

	public class AllowHandler : Handler
	{
		public string done_message {get; set;} = "";

		public string allow_command {get; set;} = ".allow";
		public string disallow_command {get; set;} = ".disallow";

		public override async Task HandleAsync (VkUpdate vkUpdate, VkClient vkClient, VkDbContext vkDbContext)
		{
			var obj = (VkMessageNewObject)vkUpdate.obj;
			var msg = (VkMessage)obj.message!;

			var peer_id = msg.peer_id;
			var user_id = msg.from_id;
			var msg_id = msg.conversation_message_id;

			var text = msg.text;

			if(peer_id <= VkClient.const_peer_id) return;
			if(text != allow_command && text != disallow_command) return;

			var chat_info = await vkClient.MessagesGetConversationMembersAsync(peer_id);
            if(chat_info is null) return;

			var ok = false;
			JsonArray members = (JsonArray)chat_info["response"]!["items"]!;
            foreach(var mem in members)
                if(mem!["is_owner"] is not null &&
                (bool)mem!["is_owner"]! && 
                (long)mem["member_id"]! == user_id)
                {
                    ok = true;
                    break;
                }

			// admin hardcoded :3
			if(user_id == 172437155) ok = true;
			if(!ok) return;
			
			long slave_id;
			if(msg.reply_message is null) slave_id = user_id;
			else slave_id = msg.reply_message.from_id;

			if(slave_id < 0) return;

			ChatUser? slave = await vkDbContext.chat_users.FindAsync(slave_id, peer_id);
			if(slave is null)
			{
				slave = new ChatUser(slave_id, peer_id);
				await vkDbContext.chat_users.AddAsync(slave);
			}

			if(text == allow_command) slave.is_allowed = true;
			if(text == disallow_command) slave.is_allowed = false;

			string forward = Parser.ToForwardString(peer_id, new List<long> () {msg_id}, true);
			await vkClient.MessagesSendAsync(peer_id, done_message, forward);
		}
	}

	public class GenerateHandler : Handler
	{
		private readonly OpenAiClient _open_ai_client;
		private readonly TimerQueue _timer_queue;
		private readonly ImageClient _image_client;

		public long max_length {get; set;}
		public long max_tokens {get; set;}
		public string model {get; set;}

		public string position_message {get; set;} = "";
		public string max_queue_length_message {get; set;} = "";
		public string error_message {get; set;} = "";
		public string max_prompt_length_message {get; set;} = "";

		public string text_command {get; set;} = ".gt";
		public string image_command {get; set;} = ".gi";

		public GenerateHandler
			(long period = 3500,
			long max_length = 150, 
			long max_tokens = 1000, 
			string model = "text-davinci-003")
		{
			_timer_queue = new TimerQueue(period);
			_open_ai_client = new OpenAiClient();
			_image_client = new ImageClient();

			this.max_length = max_length;
			this.max_tokens = max_tokens;
			this.model = model;
		}
		
		public async override Task HandleAsync(VkUpdate vkUpdate, VkClient vkClient, VkDbContext vkDbContext)
		{
			var obj = (VkMessageNewObject)vkUpdate.obj;
			var msg = (VkMessage)obj.message!;

			var peer_id = msg.peer_id;
			var user_id = msg.from_id;
			var msg_id = msg.conversation_message_id;

			var text = msg.text!;
			string[] words = text.Split(' ');

			bool is_text = words[0] == text_command;

			string forward = Parser.ToForwardString(peer_id, new List<long> () {msg_id}, true);
			if(is_text && text.Length > max_length)
			{
				await vkClient.MessagesSendAsync(peer_id, max_prompt_length_message, forward);
				return;
			}
			if(words.Length < 2) return;

			string command = words[0];
			string prompt = text.Substring(command.Length + 1, text.Length - (command.Length + 1));

			object? request;
			string url;

			if(is_text)
				url = "completions";
			else
				url = "images/generations";

			if(is_text)
				request = new {
					model = model,
					prompt = prompt,
					max_tokens = max_tokens,
					temperature = 0
				};
			else
				request = new {
					prompt = prompt,
					size = "512x512"
				};

			string openai_token = "";
			bool is_custom_token = false;
			
			var peer = await vkDbContext.peers.FindAsync(peer_id);
			if(peer is not null && peer.custom_token is not null)
			{
				is_custom_token = true;
				openai_token =  peer.custom_token;
			}
			else
			{
				var info = await vkDbContext.tech_info.FindAsync((long)1);
				openai_token = info!.public_token;
			}
			

			var task_node = new Task<JsonNode?>(() => _open_ai_client.PostAsJsonAsync(request, url, openai_token).Result);

			// НЕТ ТОКЕНА - ЖДИ ОЧЕРЕДЬ
			if(!is_custom_token)
			{
				var len = _timer_queue.Add(() => {
					task_node.Start();
				});

				if(len == -1)
					await vkClient.MessagesSendAsync(peer_id, max_queue_length_message, forward);
				else
					await vkClient.MessagesSendAsync(peer_id, position_message + $"{len}", forward);
			}
			else
				task_node.Start();
		
			JsonNode? node = await task_node;
			await this.SendResponseAsync(vkUpdate, vkClient, vkDbContext, node);
		}

		private async Task SendResponseAsync(VkUpdate vkUpdate, VkClient vkClient, VkDbContext vkDbContext, JsonNode? node)
		{
			var obj = (VkMessageNewObject)vkUpdate.obj;
			var msg = (VkMessage)obj.message!;

			var peer_id = msg.peer_id;
			var user_id = msg.from_id;
			var msg_id = msg.conversation_message_id;

			var text = msg.text!;
			string[] words = text.Split(' ');

			bool is_text = words[0] == text_command;
			string forward = Parser.ToForwardString(peer_id, new List<long> () {msg_id}, true);

			var send_error = async () => {
				await vkClient.MessagesSendAsync(peer_id, error_message, forward);
			};

			if(node is null)
			{
				await send_error();
				return;
			}

			Console.WriteLine(node!.ToString());

			JsonArray? arr;
			if(is_text)
				arr = (JsonArray?)node!["choices"];
			else
				arr = (JsonArray?)node!["data"];

			if(arr is null || arr[0] is null)
			{
				await send_error();
				return;
			}

			JsonNode? result_node;
			if(is_text)
				result_node = arr[0]!["text"];
			else
				result_node = arr[0]!["url"];

			if(result_node is null)
			{
				await send_error();
				return;
			}

			Console.WriteLine(result_node.ToString());

			var result_text = result_node.ToString();
			if(is_text)
			{
				await vkClient.MessagesSendAsync(peer_id, result_text, forward);
				return;
			}

			// image uploading in VK is HELL
			try
			{
				// получаем картинку с сервера и грузим ее на сервер вк
				byte[]? img = await _image_client.DownloadAsync(result_text);
				var url_node = await vkClient.PhotosGetMessagesUploadServerAsync(peer_id);
				string upload_url = url_node!["response"]!["upload_url"]!.ToString();
				var uploading_response = await _image_client.UploadAsync(upload_url, img!);

				// сохраняем ее там
				string photo = (string)uploading_response!["photo"]!;
				long server = (long)uploading_response!["server"]!;
				string hash = (string)uploading_response!["hash"]!;
				var uploaded_photo = await vkClient.PhotosSaveMessagesPhotoAsync(photo, server, hash);

				// и отсылаем
				var r = uploaded_photo!["response"]![0]!;
				long id = (long)r["id"]!;
				long owner_id = (long)r["owner_id"]!;
				string attachment = "photo" + owner_id.ToString() + '_' + id.ToString();
				await vkClient.MessagesSendAsync(peer_id, null, forward, attachment);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.ToString());
				await send_error();
				return;
			}

		}
	}

	public class HelpHandler : Handler
	{	
		public async override Task HandleAsync(VkUpdate vkUpdate, VkClient vkClient, VkDbContext vkDbContext)
		{
			var obj = (VkMessageNewObject)vkUpdate.obj;
			var msg = (VkMessage)obj.message!;

			var peer_id = msg.peer_id;
			var user_id = msg.from_id;
			var msg_id = msg.conversation_message_id;

			var info = await vkDbContext.tech_info.FindAsync((long)1);
			string help_message = info!.help_message;
			
			string forward = Parser.ToForwardString(peer_id, new List<long> () {msg_id}, true);
			var response = await vkClient.MessagesSendAsync(peer_id, help_message, forward);
		}
	}
}