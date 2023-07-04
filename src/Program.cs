global using System.Text.Json.Nodes;
global using System.Text;
global using System.Net.Http.Headers;
global using System.Net.Http.Json;

global using VkLib;
global using Middlewares;
global using OpenAi;

var builder = WebApplication.CreateBuilder(args);

// добавляем конфигурацию
builder.Configuration.AddJsonFile("config.json");

long group_id =                 Int32.Parse(builder.Configuration["group_id"]!);
string? confirmation_string =    builder.Configuration["confirmation_string"];
string? secret =                 builder.Configuration["secret"];
string? access_token =           builder.Configuration["access_token"];
string? version =                builder.Configuration["version"];
string? url =                    builder.Configuration["url"];
string? connection_string =      builder.Configuration["connection_string"];
string? openai_token =			 builder.Configuration["openai_token"];

// создаем клиент для отправки запросов к API
var vkClient = new VkClient(access_token!, version!, group_id);

// DI
builder.Services.AddScoped<VkUpdate>();
builder.Services.AddSingleton<VkClient>(vkClient);
builder.Services.AddMemoryCache();
builder.Services.AddDbContextPool<VkDbContext>(options => {
    options.UseMySql(connection_string, ServerVersion.AutoDetect(connection_string));});

var app = builder.Build();

if(app.Environment.IsProduction())
    app.Urls.Add(url!);

if(app.Environment.IsDevelopment())
    app.Urls.Add("http://0.0.0.0:5000");

// обрабатываем запрос сервера
app.GetUpdate(group_id, secret!);

// обрабатываем, если нужно подтверждение
app.Confirmation(confirmation_string!);

var commandsHandlerChain = new MessageNewHandler();
var commandsHandler = new CommandsHandler();
commandsHandler.unknown_command = "Неизвестная команда. Напиши .help для справки.";

// .gt
var generateHandler = new GenerateHandler();
generateHandler.max_queue_length_message = "В очереди слишком много запросов. Попробуйте позже.";
generateHandler.position_message = "В очереди! Позиция: ";
generateHandler.error_message = "Что-то пошло не так...";
generateHandler.max_prompt_length_message = "Слишком длинный запрос.";

var antiSpamHandler = new AntiSpamHandler();
antiSpamHandler.message = "Дождитесь результата предыдущего запроса.";

// .help
var helpHandler = new HelpHandler();

// .tech_info - тех информация для выдачи токенов, поэтому она здесь
var techInfoHandler = new TechInfoHandler();

commandsHandler.handlers.Add(".gt", antiSpamHandler);
commandsHandler.handlers.Add(".gi", antiSpamHandler);
commandsHandler.handlers.Add(".help", helpHandler);
commandsHandler.handlers.Add(".tech_info", techInfoHandler);

// commandsHandlerChain -> commandsHandler -> (antiSpamHandler -> generateTextHandler) / helpHandler
commandsHandlerChain.next = commandsHandler;
antiSpamHandler.next = generateHandler;


// .allow
var allowHandlerChain = new MessageNewHandler();
var allowHandler = new AllowHandler();
allowHandler.done_message = "Сделано!";

// allowHandlerChain -> allowHandler
allowHandlerChain.next = allowHandler;

// .tech
var techCommandsHandlerChain = new MessageNewHandler();
var techCommandsHandler = new TechCommandsHandler();

var techChangeHelpHandler = new TechHelpHandler();
var techCustomTokenHandler = new TechCustomTokenHandler();
var techPublicTokenHandler = new TechPublicTokenHandler();

techCommandsHandler.handlers.Add(".tech_help", techChangeHelpHandler);
techCommandsHandler.handlers.Add(".tech_custom_token", techCustomTokenHandler);
techCommandsHandler.handlers.Add(".tech_public_token", techPublicTokenHandler);

// techCommandsHandlerChain -> techCommandsHandler -> techChangeHelpHandler
techCommandsHandlerChain.next = techCommandsHandler;


app.UseHandler(commandsHandlerChain);
app.UseHandler(techCommandsHandlerChain);
app.UseHandler(allowHandlerChain);

// сохраняем изменения в бд
app.SaveChanges();

app.Run(async (context) => 
{
    context.Response.StatusCode = 200;
    await context.Response.WriteAsync("ok");
});
app.Run();