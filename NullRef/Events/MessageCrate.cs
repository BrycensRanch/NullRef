using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

[GatewayEvent(nameof(GatewayClient.MessageCreate))]
public class MessageCreateHandler(ILogger<MessageCreateHandler> logger) : IGatewayEventHandler<Message>
{
    public async ValueTask HandleAsync(Message message)
    {
        logger.LogInformation("{}", message.Content);
        await message.ReplyAsync("FEAR ME");
    }
}
