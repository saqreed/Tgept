using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using System.Net.Http;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
// набахал коммантариев везде где можно. там и сам для себя делал и для вас
class Program
{
    static string token = "8193249941:AAHZEQY2tk80FCZZf6ThZIezaHNSBu-yHzg"; 
    static HttpClient httpClient = new HttpClient(); // Для API-запросов
    static Dictionary<long, string> userCurrency = new Dictionary<long, string>(); // Для хранения выбранной валюты
    static List<Reminder> reminders = new List<Reminder>(); // Список заметок с напоминаниями
    static Dictionary<long, string> userStates = new Dictionary<long, string>(); // Для хранения состояния пользователя
    static Dictionary<long, string> userDescriptions = new Dictionary<long, string>(); // Для хранения описания заметок

    static async Task Main(string[] args)
    {
        var botClient = new TelegramBotClient(token);
        using var cts = new CancellationTokenSource();

        // Обработчик обновлений
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message }
        };

        botClient.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken: cts.Token
        );

        Console.WriteLine("Бот запущен. Нажмите Ctrl+C для остановки");
        await Task.Delay(-1); // Бесконечный цикл
    }

    // Обработка сообщений
    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.Message && update.Message?.Text != null)
        {
            var chatId = update.Message.Chat.Id;
            var messageText = update.Message.Text;

            if (userStates.ContainsKey(chatId) && userStates[chatId] == "waiting_for_note_description")
            {
                // Пользователь вводит описание заметки
                await CreateNoteDescription(botClient, chatId, messageText, cancellationToken);
            }
            else if (userStates.ContainsKey(chatId) && userStates[chatId] == "waiting_for_note_time")
            {
                // Пользователь вводит время напоминания
                await CreateNoteTime(botClient, chatId, messageText, cancellationToken);
            }
            else if (userStates.ContainsKey(chatId) && userStates[chatId] == "waiting_for_currency")
            {
                // Пользователь вводит валюту
                await ChangeCurrencyInput(botClient, chatId, messageText, cancellationToken);
            }
            else
            {
                switch (messageText.ToLower())
                {
                    case "/start":
                        await ShowMenu(botClient, chatId, cancellationToken);
                        break;

                    case "погода":
                        await SendWeather(botClient, chatId, cancellationToken);
                        break;

                    case "шутка":
                        await SendJoke(botClient, chatId, cancellationToken);
                        break;

                    case "курс валют":
                        await SendCurrencyRate(botClient, chatId, cancellationToken);
                        break;

                    case "изменить валюту":
                        await ChangeCurrency(botClient, chatId, cancellationToken);
                        break;

                    case "создать заметку":
                        await StartCreatingNote(botClient, chatId, cancellationToken);
                        break;

                    default:
                        await botClient.SendTextMessageAsync(
                            chatId: chatId,
                            text: "Я вас не понял. Используйте кнопки для выбора действий.",
                            cancellationToken: cancellationToken
                        );
                        break;
                }
            }
        }
    }

    // Главное меню
    static async Task ShowMenu(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var keyboard = new Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardMarkup(new[] {
            new Telegram.Bot.Types.ReplyMarkups.KeyboardButton[] { "Погода", "Шутка" },
            new Telegram.Bot.Types.ReplyMarkups.KeyboardButton[] { "Курс валют", "Изменить валюту", "Создать заметку" }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = false
        };

        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "Выберите действие:",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken
        );
    }

    // Начало создания заметки
    static async Task StartCreatingNote(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        userStates[chatId] = "waiting_for_note_description";
        await botClient.SendTextMessageAsync(chatId, "Введите описание заметки:", cancellationToken: cancellationToken);
    }

    // Сохранение описания заметки
    static async Task CreateNoteDescription(ITelegramBotClient botClient, long chatId, string description, CancellationToken cancellationToken)
    {
        userStates[chatId] = "waiting_for_note_time";
        userDescriptions[chatId] = description; 

        await botClient.SendTextMessageAsync(chatId, "Введите время напоминания в формате 'yyyy-MM-dd HH:mm' (например, 2024-12-31 15:00):", cancellationToken: cancellationToken);
    }

    // Сохранение времени напоминания и добавление заметки
    static async Task CreateNoteTime(ITelegramBotClient botClient, long chatId, string noteTime, CancellationToken cancellationToken)
    {
        // Парсим время для напоминания
        DateTime reminderTime;
        if (DateTime.TryParse(noteTime, out reminderTime))
        {
            string description = userDescriptions[chatId]; 
            var reminder = new Reminder
            {
                ChatId = chatId,
                Description = description,
                ReminderTime = reminderTime
            };

            reminders.Add(reminder);
            await botClient.SendTextMessageAsync(chatId, $"Заметка '{description}' на {reminderTime} добавлена.", cancellationToken: cancellationToken);

            // Запускаем таймер, который будет напоминать за 10 минут до события
            var delay = reminderTime - DateTime.Now.AddMinutes(-10); 
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay);
                await botClient.SendTextMessageAsync(chatId, $"Напоминание: {reminder.Description} в {reminderTime}.", cancellationToken: cancellationToken);
            }

            // Сброс состояния
            userStates.Remove(chatId);
            userDescriptions.Remove(chatId); 
        }
        else
        {
            await botClient.SendTextMessageAsync(chatId, "Неверный формат времени.", cancellationToken: cancellationToken);
        }
    }

    // Получение погоды
    static async Task SendWeather(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        string city = "Moscow"; // Город по умолчанию
        string apiKey = "f2815d129f4e322e83bebc10b66f1ea0"; 

        try
        {
            var response = await httpClient.GetStringAsync($"https://api.openweathermap.org/data/2.5/weather?q={city}&appid={apiKey}&units=metric&lang=ru");
            dynamic weather = JsonConvert.DeserializeObject(response);
            string message = $"Погода в {weather.name}:\nТемпература: {weather.main.temp}°C\nОписание: {weather.weather[0].description}";

            await botClient.SendTextMessageAsync(chatId, message, cancellationToken: cancellationToken);
        }
        catch
        {
            await botClient.SendTextMessageAsync(chatId, "Ошибка при получении погоды.", cancellationToken: cancellationToken);
        }
    }

    // Получение случайной шутки
    static async Task SendJoke(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.GetStringAsync("https://official-joke-api.appspot.com/random_joke");
            dynamic joke = JsonConvert.DeserializeObject(response);
            string message = $"{joke.setup}\n{joke.punchline}";

            await botClient.SendTextMessageAsync(chatId, message, cancellationToken: cancellationToken);
        }
        catch
        {
            await botClient.SendTextMessageAsync(chatId, "Ошибка при получении шутки.", cancellationToken: cancellationToken);
        }
    }

    // Получение курса валют
    static async Task SendCurrencyRate(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        string currency = userCurrency.ContainsKey(chatId) ? userCurrency[chatId] : "USD";
        string apiKey = "ecd0bebdf343779ed6103d62"; 

        try
        {
            var response = await httpClient.GetStringAsync($"https://v6.exchangerate-api.com/v6/{apiKey}/latest/RUB");
            dynamic rates = JsonConvert.DeserializeObject(response);
            string rate = rates.conversion_rates[currency];

            string message = $"Курс рубля к {currency}: 1 {currency} = {rate} RUB";
            await botClient.SendTextMessageAsync(chatId, message, cancellationToken: cancellationToken);
        }
        catch
        {
            await botClient.SendTextMessageAsync(chatId, "Ошибка при получении курса валют.", cancellationToken: cancellationToken);
        }
    }

    // Изменение валюты
    static async Task ChangeCurrency(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        await botClient.SendTextMessageAsync(chatId, "Введите код валюты (например, USD, EUR, GBP):", cancellationToken: cancellationToken);
        userStates[chatId] = "waiting_for_currency"; // Ожидаем ввода валюты
    }

    // Обработка ввода валюты
    static async Task ChangeCurrencyInput(ITelegramBotClient botClient, long chatId, string currency, CancellationToken cancellationToken)
    {
        userCurrency[chatId] = currency.ToUpper(); 
        userStates.Remove(chatId);

        await botClient.SendTextMessageAsync(chatId, $"Валюта изменена на {currency.ToUpper()}.", cancellationToken: cancellationToken);
    }

    // Обработка ошибок
    static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Ошибка: {exception.Message}");
        return Task.CompletedTask;
    }
}

// Класс для заметок
class Reminder
{
    public long ChatId { get; set; }
    public string Description { get; set; }
    public DateTime ReminderTime { get; set; }
}
