using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace GagBot.Services
{
    public class HandleUpdateService
    {
        private static readonly ConcurrentDictionary<long, DateTime> s_cumcumberedMembers = new();

        private readonly ITelegramBotClient _botClient;
        private readonly ILogger<HandleUpdateService> _logger;

        public HandleUpdateService(ITelegramBotClient botClient, ILogger<HandleUpdateService> logger)
        {
            _botClient = botClient;
            _logger = logger;
        }

        public async Task EchoAsync(Update update)
        {
            if (update.Message is null)
            {
                return;
            }

            if (update.Message?.Date < DateTime.UtcNow.AddHours(-1))
            {
                return;
            }

            if (update.Message?.Text?.StartsWith('/') is null or false)
            {
                await HandleMessageAsync(_botClient, update.Message!);
                return;
            }

            var handler = update.Type switch
            {
                UpdateType.Message => BotOnMessageReceived(update.Message!),
                UpdateType.EditedMessage => BotOnMessageReceived(update.Message!),
                _ => UnknownUpdateHandlerAsync(update)
            };

            try
            {
                await handler;
            }
            catch (Exception exception)
            {
                await HandleErrorAsync(exception);
            }
        }

        private async Task BotOnMessageReceived(Message message)
        {
            _logger.LogInformation("Receive message type: {messageType}", message.Type);
            if (message.Type != MessageType.Text)
            {
                return;
            }

            var action = message.Text!.Split(' ').First() switch
            {
                "/cumcumber" => SendCumcumber(_botClient, message),
                "/uncumcumber" => SendUncumcumber(_botClient, message),
                "/cumcumber@cumcumber_gag_bot" => SendCumcumber(_botClient, message),
                "/uncumcumber@cumcumber_gag_bot" => SendUncumcumber(_botClient, message),
                _ => Usage(_botClient, message)
            };

            Message sentMessage = await action;
            _logger.LogInformation("The message was send with id: {sendMessageId}", sentMessage.MessageId);
        }

        private Task UnknownUpdateHandlerAsync(Update update)
        {
            _logger.LogInformation("Unknown update type: {updateType}", update.Type);
            return Task.CompletedTask;
        }

        private Task HandleErrorAsync(Exception exception)
        {
            var errorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            _logger.LogInformation("HandleError: {errorMessage}", errorMessage);
            return Task.CompletedTask;
        }

        private static async Task HandleMessageAsync(ITelegramBotClient bot, Message message)
        {
            var senderId = message.From!.Id;
            if (!s_cumcumberedMembers.ContainsKey(senderId) || s_cumcumberedMembers[senderId] <= DateTime.UtcNow)
            {
                s_cumcumberedMembers.TryRemove(senderId, out _);
                return;
            }

            await bot.DeleteMessageAsync(message.Chat.Id, message.MessageId);
        }

        private static async Task<Message> SendCumcumber(ITelegramBotClient bot, Message message)
        {
            if (message.Entities is null or {Length: < 1} || message.ReplyToMessage is null)
            {
                return await CumcumberUsage(bot, message);
            }

            if (IsToSelfOrBotReply(message))
            {
                return await CumcumberUsage(bot, message);
            }

            var userId = message.ReplyToMessage.From!.Id;

            var userName = message.ReplyToMessage.From!.Username;

            var hasEnoughRights = await CheckRights(bot, message);

            if (!hasEnoughRights)
            {
                return await NotEnoughRights(bot, message);
            }

            if (IsCumcumbered(userId))
            {
                return await AlreadyCumcumbered(bot, message);
            }

            var timespanText = String.Join(' ', message.Text!.Split(' ')[1..]);

            if (!TryCalculateTimeSpan(timespanText, out TimeSpan ts))
            {
                return await Usage(bot, message);
            }

            if (ts < TimeSpan.FromMinutes(1) || ts > TimeSpan.FromDays(365))
            {
                ts = TimeSpan.FromMinutes(2);
            }
            

            var text = $"@{userName} is cumcumbered for {ts.ToString("g", new CultureInfo("en-US"))}";

            var untilDate = DateTime.UtcNow.Add(ts);

            s_cumcumberedMembers[userId] = untilDate;

            return await bot.SendTextMessageAsync(
                message.Chat.Id, 
                text,
                replyToMessageId: message.MessageId);
        }

        private static async Task<Message> SendUncumcumber(ITelegramBotClient bot, Message message)
        {
            if (message.Entities is null or { Length: < 1 } || message.ReplyToMessage is null)
            {
                return await UncumcumberUsage(bot, message);
            }

            if (IsToSelfOrBotReply(message))
            {
                return await UncumcumberUsage(bot, message);
            }

            var userId = message.ReplyToMessage.From!.Id;

            var userName = message.ReplyToMessage.From!.Username;

            var hasEnoughRights = await CheckRights(bot, message);

            if (!hasEnoughRights)
            {
                return await NotEnoughRights(bot, message);
            }

            if (!IsCumcumbered(userId))
            {
                return await NotCumcumbered(bot, message);
            }

            s_cumcumberedMembers.Remove(userId, out _);
            string text = $"@{userName} was uncumcumbered.";

            return await bot.SendTextMessageAsync(chatId: message.Chat.Id,
                                                  text: text,
                                                  replyToMessageId: message.MessageId);
        }

        private static async Task<Message> CumcumberUsage(ITelegramBotClient bot, Message message)
        {
            const string usage = "/cumcumber must be sent as a reply to a person's message you want to be cumcumbered. You cannot cumcumber bot or yourself.";

            return await bot.SendTextMessageAsync(chatId: message.Chat.Id,
                                                  text: usage,
                                                  replyToMessageId: message.MessageId,
                                                  replyMarkup: new ReplyKeyboardRemove());
        }

        private static async Task<Message> UncumcumberUsage(ITelegramBotClient bot, Message message)
        {
            const string usage = "/uncumcumber must be sent as a reply to a person's message you want to be uncumcumbered.";

            return await bot.SendTextMessageAsync(chatId: message.Chat.Id,
                                                  text: usage,
                                                  replyToMessageId: message.MessageId,
                                                  replyMarkup: new ReplyKeyboardRemove());
        }

        private static async Task<Message> Usage(ITelegramBotClient bot, Message message)
        {
            const string usage = "Usage:\n" +
                                 "/cumcumber [timespan] - put a cucumber in a someone's mouth\n" +
                                 "/uncumcumber - take a cucumber from a someone's mouth";

            return await bot.SendTextMessageAsync(chatId: message.Chat.Id,
                                                  text: usage,
                                                  replyToMessageId: message.MessageId,
                                                  replyMarkup: new ReplyKeyboardRemove());
        }

        private static async Task<Message> NotEnoughRights(ITelegramBotClient bot, Message message)
        {
            const string text = "Куди тобі, кмете з нижнього Інтернету?";

            return await bot.SendTextMessageAsync(chatId: message.Chat.Id,
                                                  text: text,
                                                  replyToMessageId: message.MessageId);
        }

        private static async Task<Message> AlreadyCumcumbered(ITelegramBotClient bot, Message message)
        {
            var userName = message.ReplyToMessage!.From!.Username;

            string text =
                $"@{userName} is already cumcumbered. Don't you think it would be too hard to have 2 cucumbers in the mouth?";

            return await bot.SendTextMessageAsync(chatId: message.Chat.Id,
                                                  text: text,
                                                  replyToMessageId: message.MessageId);
        }

        private static async Task<Message> NotCumcumbered(ITelegramBotClient bot, Message message)
        {
            var userName = message.ReplyToMessage!.From!.Username;

            string text = $"@{userName} is not cumcumbered. Cannot uncumcumber uncumcumbered.";

            return await bot.SendTextMessageAsync(chatId: message.Chat.Id,
                                                  text: text,
                                                  replyToMessageId: message.MessageId);
        }

        private static async Task<bool> CheckRights(ITelegramBotClient bot, Message message)
        {
            var members = await bot.GetChatAdministratorsAsync(message.Chat.Id);

            var administrators = members.OfType<ChatMemberAdministrator>()
                .Where(a => a.CanRestrictMembers);

            var id = message.ReplyToMessage!.From!.Id;

            var owner = members.OfType<ChatMemberOwner>().First();

            var senderId = message.From!.Id;

            return (administrators.Any(a => a.User.Id == senderId) && id != owner.User.Id) || owner.User.Id == senderId;
        }

        private static bool TryCalculateTimeSpan(string timespanText, out TimeSpan timeSpan)
        {
            timeSpan = TimeSpan.Zero;

            var regexOptions = RegexOptions.IgnoreCase;

            var patterns = new []
            {
                @"([1-9][0-9]*\s*s)",
                @"([1-9][0-9]*\s*m)",
                @"([1-9][0-9]*\s*h)",
                @"([1-9][0-9]*\s*d)",
                @"([1-9][0-9]*\s*w)",
                @"([1-9][0-9]*\s*M)",
                @"([1-9][0-9]*\s*y)"
            }.ToList();

            MatchCollection seconds = Regex.Matches(timespanText, patterns[0], regexOptions);
            MatchCollection minutes = Regex.Matches(timespanText, patterns[1]);
            MatchCollection hours = Regex.Matches(timespanText, patterns[2], regexOptions);
            MatchCollection days = Regex.Matches(timespanText, patterns[3], regexOptions);
            MatchCollection weeks = Regex.Matches(timespanText, patterns[4], regexOptions);
            MatchCollection months = Regex.Matches(timespanText, patterns[5]);
            MatchCollection years = Regex.Matches(timespanText, patterns[6], regexOptions);

            patterns.ForEach(pattern =>
            {
                timespanText = Regex.Replace(timespanText, pattern, String.Empty, regexOptions);
            });

            if (timespanText.Trim() is not "")
            {
                return false;
            }

            timeSpan = AddMatches(seconds, timeSpan, "s", TimeSpan.FromSeconds);
            timeSpan = AddMatches(minutes, timeSpan, "m", TimeSpan.FromMinutes);
            timeSpan = AddMatches(hours, timeSpan, "h", TimeSpan.FromHours);
            timeSpan = AddMatches(days, timeSpan, "d", TimeSpan.FromDays);
            timeSpan = AddMatches(weeks, timeSpan, "w", (weeks) => TimeSpan.FromDays(weeks * 7));
            timeSpan = AddMatches(months, timeSpan, "M", (months) => TimeSpan.FromDays(months * 30));
            timeSpan = AddMatches(years, timeSpan, "y", (years) => TimeSpan.FromDays(years * 365));

            return true;
        }

        private static TimeSpan AddMatches(MatchCollection matches,
                                           TimeSpan current, 
                                           string splitText, 
                                           Func<double, TimeSpan> add)
        {
            foreach (Match match in matches)
            {
                ulong value = UInt64.Parse(match.Value.Split(splitText).First().Trim());

                current = current.Add(add(value));
            }

            return current;
        }

        private static bool IsToSelfOrBotReply(Message message)
        {
            var senderId = message.From!.Id;

            return senderId == message.ReplyToMessage!.From!.Id || (message.ReplyToMessage!.From!.IsBot && !message.ReplyToMessage!.From!.FirstName.Equals("Channel", StringComparison.InvariantCultureIgnoreCase));
        }

        private static bool IsCumcumbered(long id)
        {
            return s_cumcumberedMembers.ContainsKey(id) && s_cumcumberedMembers[id] > DateTime.UtcNow;
        }
    }
}
