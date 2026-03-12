using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;

namespace TelegramProductivityBot.Services
{
    /// <summary>
    /// Сервис для автоматических утренних (9:00) и вечерних (21:00) напоминаний.
    /// </summary>
    public class ReminderService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly TaskService _taskService;

        public ReminderService(ITelegramBotClient botClient, TaskService taskService)
        {
            _botClient = botClient;
            _taskService = taskService;
        }

        /// <summary>
        /// Основной фоновый цикл для проверки времени и отправки напоминаний.
        /// </summary>
        public async Task StartBackgroundLoopAsync(CancellationToken cancellationToken)
        {
            // Флаги, чтобы не отправлять сообщения несколько раз за 1 минуту
            bool morningSentToday = false;
            bool eveningSentToday = false;
            int lastDay = -1;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var now = DateTime.Now; // Локальное время сервера/компьютера

                    // Сброс флагов при наступлении нового дня
                    if (now.Day != lastDay)
                    {
                        morningSentToday = false;
                        eveningSentToday = false;
                        lastDay = now.Day;
                    }

                    // Проверка на утреннее напоминание: 9:00
                    if (now.Hour == 9 && now.Minute == 0 && !morningSentToday)
                    {
                        await SendMorningRemindersAsync(cancellationToken);
                        morningSentToday = true;
                    }

                    // Проверка на вечернее напоминание: 21:00
                    if (now.Hour == 21 && now.Minute == 0 && !eveningSentToday)
                    {
                        await SendEveningRemindersAsync(cancellationToken);
                        eveningSentToday = true;
                    }

                    // Ждем 60 секунд перед следующей проверкой
                    await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // Игнорируем штатную отмену
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ReminderService] Ошибка в цикле напоминаний: {ex.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken); // ждем минуту и пытаемся снова
                }
            }
        }

        private async Task SendMorningRemindersAsync(CancellationToken cancellationToken)
        {
            string message = "Доброе утро.\nНе забудь создать план на сегодня:\n/plan";
            var users = _taskService.GetAllUniqueUsers();
            foreach (var userId in users)
            {
                try
                {
                    await _botClient.SendMessage(userId, message, cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ReminderService] Не удалось отправить утро 'user {userId}': {ex.Message}");
                }
            }
        }

        private async Task SendEveningRemindersAsync(CancellationToken cancellationToken)
        {
            string message = "Подведи итог дня:\n/report";
            var users = _taskService.GetAllUniqueUsers();
            foreach (var userId in users)
            {
                try
                {
                    await _botClient.SendMessage(userId, message, cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ReminderService] Не удалось отправить вечер 'user {userId}': {ex.Message}");
                }
            }
        }
    }
}
