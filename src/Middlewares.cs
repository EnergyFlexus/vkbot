using VkLib;

namespace Middlewares
{
    public static class MiddlewareExtensions
    {
        /// <summary>
        /// Проверяет данные и парсит их в VkUpdate
        /// </summary>
        public static IApplicationBuilder GetUpdate 
            (this IApplicationBuilder builder,
            long group_id, 
            string secret)
        {
            return builder.UseMiddleware<GetUpdateMiddleware>(group_id, secret);
        }

        /// <summary>
        /// Обработка подтверждения Callback API
        /// </summary>
        public static IApplicationBuilder Confirmation(this IApplicationBuilder builder, string confirmation_string)
        {
            return builder.UseMiddleware<ConfirmationMiddleware>(confirmation_string);
        }

        /// <summary>
        /// Обработка Handler
        /// </summary>
        public static IApplicationBuilder UseHandler(this IApplicationBuilder builder, Handler handler)
        {
            return builder.UseMiddleware<UseHandlerMiddleware>(handler);
        }

        /// <summary>
        /// сохраняет изменения в бд
        /// </summary>
        public static IApplicationBuilder SaveChanges(this IApplicationBuilder builder) 
        {
            return builder.UseMiddleware<SaveChangesMiddleware>();
        }
    }

    public class SaveChangesMiddleware
    {
        // следующий middle ware (DI)
        private readonly RequestDelegate next;

        public SaveChangesMiddleware(RequestDelegate next) 
        { 
            this.next = next;
        }

        public async Task InvokeAsync(HttpContext context, VkDbContext vkDbContext)
        {
            await vkDbContext.SaveChangesAsync();
            await next.Invoke(context);
        }
    }

    // преобразуем запрос в VkUpdate и дальше работаем с ним
    public class GetUpdateMiddleware
    {
        // следующий middle ware (DI)
        private readonly RequestDelegate next;

        // логгер (DI)
        private readonly ILogger<GetUpdateMiddleware> logger;

        // наша группа
        private readonly long group_id;

        // секретная строка
        private readonly string secret;

        public GetUpdateMiddleware(RequestDelegate next, ILogger<GetUpdateMiddleware> logger, long group_id, string secret) 
        { 
            this.next = next;
            this.logger = logger;
            this.group_id = group_id;
            this.secret = secret;
        }

        public async Task InvokeAsync(HttpContext context, VkUpdate vkUpdate)
        {
            // если нет json вообще
            if(!context.Request.HasJsonContentType())
            {
                logger.LogError("data isn't json");
                return;
            }

            // получаем json
            JsonNode? node = await context.Request.ReadFromJsonAsync<JsonNode>();

            // если он пустой
            if (node is null)
            {
                logger.LogError("Request is null");
                return;
            }

            // в логгер
            logger.LogInformation(node.ToString());

            // раскидываем по классу
            vkUpdate.type = (string)node["type"]!;
            vkUpdate.group_id = (long)node["group_id"]!;
            vkUpdate.secret = (string)node["secret"]!;
            vkUpdate.obj = node["object"];

            // если не совпал секретный ключ
            if (vkUpdate!.secret != secret)
            {
                logger.LogError("secrets aren't equal");
                return;
            }

            // если не совпала группа
            if(vkUpdate.group_id != group_id)
            {
                logger.LogError("group_ids aren't equal");
                return;
            }

            // если это не первая попытка
            if(context.Request.Headers.ContainsKey("X-Retry-Counter"))
            {
                context.Response.StatusCode = 200;
                await context.Response.WriteAsync("ok");
                return;
            }
            await next.Invoke(context);
        }
    }

    // нужен для отправки подтверждения
    public class ConfirmationMiddleware
    {
        // следующий middle ware (DI)
        private readonly RequestDelegate next;

        // логгер (DI)
        private readonly ILogger<ConfirmationMiddleware> logger;

        // строка подтверждения
        private readonly string confirmation_string;

        public ConfirmationMiddleware(RequestDelegate next, ILogger<ConfirmationMiddleware> logger, string confirmation_string)
        {
            this.next = next;
            this.logger = logger;
            this.confirmation_string = confirmation_string;
        }

        public async Task InvokeAsync(HttpContext context, VkUpdate vkUpdate)
        {
            // проверяем тип
            if(vkUpdate.type != "confirmation")
            {
                await next.Invoke(context);
                return;
            }

            // если все совпало - отправляем ответ
            context.Response.StatusCode = 200;
            await context.Response.WriteAsync(confirmation_string);
            return;
        }
    }

    // обработка апдейтов
    public class UseHandlerMiddleware
    {
        // следующий middle ware (DI)
        private readonly RequestDelegate next;

        // логгер (DI)
        private readonly ILogger<UseHandlerMiddleware> logger;

        // Обработчик (DI)
        private readonly Handler handler;

        public UseHandlerMiddleware(RequestDelegate next, 
        ILogger<UseHandlerMiddleware> logger, 
        Handler handler)
        {
            this.next = next;
            this.logger = logger;
            this.handler = handler;
        }

        // VkUpdate и Client получаем по DI
        public async Task InvokeAsync(HttpContext context, 
        VkUpdate vkUpdate, 
        VkClient vkClient,
        VkDbContext vkDbContext)
        {
            await handler.HandleAsync(vkUpdate, vkClient, vkDbContext);
            await next.Invoke(context);
        }
    }
}