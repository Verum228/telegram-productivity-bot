using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TelegramProductivityBot.Services;

namespace TelegramProductivityBot
{
    /// <summary>
    /// Сервис, который отвечает за управление циклом работы бота.
    /// Он инициирует соединение и перенаправляет входящие сообщения.
    /// </summary>
    public class BotService
    {
        private readonly TelegramBotClient _botClient;
        private readonly CommandHandler _commandHandler;
        private readonly TaskService _taskService;
        private readonly FocusService _focusService;
        private readonly AntiLazinessService _antiLazinessService;
        private readonly StatsService _statsService;
        private readonly DayPlanService _dayPlanService;
        private readonly ActivityService _activityService;
        private readonly ReminderService _reminderService;
        private readonly LongTaskService _longTaskService;
        private readonly StreakService _streakService;
        private readonly AdviceService _adviceService;
        private readonly CancellationTokenSource _cts;

        public BotService(string botToken)
        {
            // Создаем клиент Telegram бота с указанным токеном
            _botClient = new TelegramBotClient(botToken);
            
            // Инициализируем сервисы
            _taskService = new TaskService();
            _activityService = new ActivityService(_taskService);
            _statsService = new StatsService(_taskService, _activityService);
            _focusService = new FocusService(_botClient, _taskService, _statsService);
            _antiLazinessService = new AntiLazinessService(_botClient, _taskService);
            _streakService = new StreakService(_taskService);
            _dayPlanService = new DayPlanService(_botClient, _taskService, _streakService);
            _reminderService = new ReminderService(_botClient, _taskService);
            _longTaskService = new LongTaskService();
            _adviceService = new AdviceService(_taskService, _streakService);
            
            // Инициализируем обработчик команд, передавая ему клиент бота и сервисы
            _commandHandler = new CommandHandler(_botClient, _taskService, _focusService, _antiLazinessService, _statsService, _dayPlanService, _activityService, _longTaskService, _streakService, _adviceService);
            
            // Источник токена отмены для корректной остановки бота
            _cts = new CancellationTokenSource();
        }

        /// <summary>
        /// Запуск получения обновлений от Telegram (Long Polling).
        /// </summary>
        public async Task StartAsync()
        {
            // Настройка параметров получения обновлений (получаем всё, что можно)
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>() // Получать все типы обновлений
            };

            // Запускаем процесс long polling
            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                errorHandler: HandleErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: _cts.Token
            );

            // Получаем информацию о боте для вывода в консоль
            var me = await _botClient.GetMe();
            Console.WriteLine($"Бот @{me.Username} успешно запущен.");

            // Запускаем фоновые задачи
            _ = Task.Run(() => _reminderService.StartBackgroundLoopAsync(_cts.Token));
        }

        /// <summary>
        /// Остановка получения обновлений и фоновых задач.
        /// </summary>
        public void Stop()
        {
            _cts.Cancel();
            _antiLazinessService.Stop();
            Console.WriteLine("Бот остановлен.");
        }

        /// <summary>
        /// Метод для обработки входящих обновлений от Telegram.
        /// </summary>
        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            // Нас интересуют только сообщения, содержащие текст
            if (update.Type == UpdateType.Message && update.Message?.Text != null)
            {
                // Если пришло текстовое сообщение, передаем его в CommandHandler
                await _commandHandler.HandleMessageAsync(update.Message, cancellationToken);
            }
        }

        /// <summary>
        /// Метод для обработки ошибок связи с API Telegram.
        /// </summary>
        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, Telegram.Bot.Polling.HandleErrorSource source, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Произошла ошибка ({source}) при работе с API Telegram: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}
