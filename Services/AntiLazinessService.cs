using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;

namespace TelegramProductivityBot.Services
{
    /// <summary>
    /// Сервис для обработки "анти-лень" (напоминания в 12:00 и 18:00) 
    /// и "жёсткого (hardmode)" режима (проверки активности днем).
    /// </summary>
    public class AntiLazinessService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly TaskService _taskService;
        private readonly StatsService _statsService;
        private readonly CancellationTokenSource _cts;

        // Память пропущенных чеков для hard mode
        private readonly ConcurrentDictionary<long, int> _missedChecks;

        private readonly string[] _hardModeQuestions = new[]
        {
            "hardmode_question_0",
            "hardmode_question_1",
            "hardmode_question_2",
            "hardmode_question_3",
            "hardmode_question_4",
            "hardmode_question_5",
            "hardmode_question_6"
        };

        public AntiLazinessService(ITelegramBotClient botClient, TaskService taskService, StatsService statsService)
        {
            _botClient = botClient;
            _taskService = taskService;
            _statsService = statsService;
            _cts = new CancellationTokenSource();
            _missedChecks = new ConcurrentDictionary<long, int>();

            // Запуск фоновых циклов
            _ = Task.Run(() => AntiLenLoopAsync(_cts.Token), _cts.Token);
            _ = Task.Run(() => HardModeLoopAsync(_cts.Token), _cts.Token);
        }

        public void Stop()
        {
            _cts.Cancel();
        }

        public async Task SetAntiLenAsync(long userId, bool enabled, string lang)
        {
            _taskService.SetSetting(userId, "antilen", enabled ? "1" : "0");
            string status = enabled ? LocalizationService.T("mode_enabled", lang) : LocalizationService.T("mode_disabled", lang);
            string response = LocalizationService.T("antilen_status", lang).Replace("{status}", status);
            await _botClient.SendMessage(chatId: userId, text: response);
        }

        public bool IsAntiLenActive(long userId)
        {
            var users = _taskService.GetUsersWithSetting("antilen", "1");
            return users.Contains(userId);
        }

        public bool IsHardModeActive(long userId)
        {
            var users = _taskService.GetUsersWithSetting("hardmode", "1");
            return users.Contains(userId);
        }

        public async Task SetHardModeAsync(long userId, bool enabled, string lang)
        {
            _taskService.SetSetting(userId, "hardmode", enabled ? "1" : "0");
            if (enabled) _missedChecks[userId] = 0; // Сбрасываем счётчик пропущенных
            string status = enabled ? LocalizationService.T("mode_enabled", lang) : LocalizationService.T("mode_disabled", lang);
            string response = LocalizationService.T("hardmode_status", lang).Replace("{status}", status);
            await _botClient.SendMessage(chatId: userId, text: response);
        }

        /// <summary>
        /// Вызывается при любой активности пользователя (сообщения к боту).
        /// </summary>
        public void RecordUserActivity(long userId)
        {
            if (_missedChecks.ContainsKey(userId))
            {
                _missedChecks[userId] = 0; // Сбрасываем чеки, так как пользователь жив
            }
        }

        /// <summary>
        /// Ежедневные проверки в 12:00 и 18:00 на выполнение минимум одной задачи.
        /// </summary>
        private async Task AntiLenLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var now = DateTime.Now;
                    
                    if (now.Hour == 12 && now.Minute == 0)
                    {
                        var users = _taskService.GetUsersWithSetting("antilen", "1");
                        foreach (var userId in users)
                        {
                            if (_taskService.GetTasksCompletedToday(userId) == 0)
                            {
                                var lang = _taskService.GetUserLanguage(userId);
                                await _botClient.SendMessage(chatId: userId, text: LocalizationService.T("anti_reminder_12", lang), cancellationToken: token);
                            }
                        }
                        // Ждём 1 минуту, чтобы не спамить в 12:00
                        await Task.Delay(TimeSpan.FromMinutes(1), token);
                    }
                    else if (now.Hour == 18 && now.Minute == 0)
                    {
                        var users = _taskService.GetUsersWithSetting("antilen", "1");
                        foreach (var userId in users)
                        {
                            if (_taskService.GetTasksCompletedToday(userId) == 0)
                            {
                                var lang = _taskService.GetUserLanguage(userId);
                                await _botClient.SendMessage(chatId: userId, text: LocalizationService.T("anti_reminder_18", lang), cancellationToken: token);
                            }
                        }
                        await Task.Delay(TimeSpan.FromMinutes(1), token);
                    }
                    else
                    {
                        // Проверяем время каждые 20 секунд
                        await Task.Delay(TimeSpan.FromSeconds(20), token);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в AntiLenLoopAsync: {ex.Message}");
            }
        }

        /// <summary>
        /// Фоновая задача для hardmode. Рассылка каждые 30 минут в рабочее время (9-21)
        /// </summary>
        private async Task HardModeLoopAsync(CancellationToken token)
        {
            var rnd = new Random();
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var now = DateTime.Now;

                    // Рабочее время
                    if (now.Hour >= 9 && now.Hour < 21)
                    {
                        await Task.Delay(TimeSpan.FromMinutes(30), token);

                        var users = _taskService.GetUsersWithSetting("hardmode", "1");
                        foreach (var userId in users)
                        {
                            // Увеличиваем счётчик непрочитанных (по умолчанию 0, если ключа нет)
                            int missed = _missedChecks.AddOrUpdate(userId, 1, (_, current) => current + 1);

                            if (missed >= 3)
                            {
                                // Наказываем -15 XP вместо задачи
                                _statsService.AddXP(userId, -15);
                                var lang = _taskService.GetUserLanguage(userId);
                                await _botClient.SendMessage(
                                    chatId: userId,
                                    text: LocalizationService.T("hardmode_penalty", lang),
                                    cancellationToken: token);
                                    
                                _missedChecks[userId] = 0; // Сбрасываем после штрафа
                            }
                            else
                            {
                                // Выбираем случайный вопрос
                                string questionKey = _hardModeQuestions[rnd.Next(_hardModeQuestions.Length)];
                                var lang = _taskService.GetUserLanguage(userId);
                                await _botClient.SendMessage(
                                    chatId: userId,
                                    text: LocalizationService.T(questionKey, lang),
                                    cancellationToken: token);
                            }
                        }
                    }
                    else
                    {
                        // Спим час в нерабочее время
                        await Task.Delay(TimeSpan.FromHours(1), token);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в HardModeLoopAsync: {ex.Message}");
            }
        }
    }
}
