using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using TelegramProductivityBot.Models;

namespace TelegramProductivityBot.Services
{
    /// <summary>
    /// Класс для управления фокус-сессиями (Pomodoro).
    /// </summary>
    public class FocusService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly TaskService _taskService;
        private readonly StatsService _statsService;
        private readonly ConcurrentDictionary<long, FocusSession> _sessions;

        public FocusService(ITelegramBotClient botClient, TaskService taskService, StatsService statsService)
        {
            _botClient = botClient;
            _taskService = taskService;
            _statsService = statsService;
            _sessions = new ConcurrentDictionary<long, FocusSession>();
        }

        /// <summary>
        /// Запускает фокус-сессию для пользователя.
        /// </summary>
        public async Task StartFocusAsync(long userId, int minutes)
        {
            if (_sessions.TryGetValue(userId, out var existingSession) && existingSession.IsActive)
            {
                await _botClient.SendMessage(userId, "У вас уже запущена фокус-сессия. Остановите её командой /stopfocus.");
                return;
            }

            var cts = new CancellationTokenSource();
            var session = new FocusSession
            {
                UserId = userId,
                StartTime = DateTime.UtcNow,
                DurationMinutes = minutes,
                CancellationTokenSource = cts
            };

            _sessions[userId] = session;
            Console.WriteLine($"Focus started: {userId} {minutes}");

            await _botClient.SendMessage(userId, $"Начинаем фокус на {minutes} минут. Не отвлекайся.");

            // Запускаем фоновую задачу для отслеживания времени (не блокирует поток обработки команд)
            _ = Task.Run(() => RunSessionAsync(session, cts.Token), cts.Token);
        }

        /// <summary>
        /// Основной цикл сессии (отсчет времени, уведомления каждые 5 минут).
        /// </summary>
        private async Task RunSessionAsync(FocusSession session, CancellationToken token)
        {
            try
            {
                int totalMinutes = session.DurationMinutes;
                int passedMinutes = 0;

                while (passedMinutes < totalMinutes)
                {
                    int delay = Math.Min(5, totalMinutes - passedMinutes);
                    
                    // Ждем нужное количество минут (до 5) или до отмены
                    await Task.Delay(TimeSpan.FromMinutes(delay), token);
                    
                    passedMinutes += delay;
                    
                    if (passedMinutes < totalMinutes)
                    {
                        await _botClient.SendMessage(session.UserId, $"Прошло {passedMinutes} минут. Продолжай фокус.", cancellationToken: token);
                    }
                }

                // Успешное завершение сессии
                Console.WriteLine($"Focus finished: {session.UserId} {session.DurationMinutes} true");
                
                // Опционально сохраняем логи в SQLite
                _taskService.LogFocusSession(session.UserId, session.DurationMinutes, true);
                
                // Начисляем опыт (+20 XP за успешный фокус)
                _statsService.UpdateFocusCompleted(session.UserId);
                
                _sessions.TryRemove(session.UserId, out _);

                int breakMinutes = 5;
                await _botClient.SendMessage(session.UserId, $"Фокус завершён. Сделай перерыв {breakMinutes} минут. Получено +20 XP!", cancellationToken: token);
            }
            catch (TaskCanceledException)
            {
                // Сессия была прервана пользователем
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в фокус-сессии для пользователя {session.UserId}: {ex.Message}");
            }
        }

        /// <summary>
        /// Досрочная остановка фокус-сессии.
        /// </summary>
        public async Task StopFocusAsync(long userId)
        {
            if (_sessions.TryRemove(userId, out var session))
            {
                session.CancellationTokenSource.Cancel();
                
                int passedMinutes = (int)(DateTime.UtcNow - session.StartTime).TotalMinutes;
                Console.WriteLine($"Focus finished: {userId} {session.DurationMinutes} false");
                
                // Логируем как неуспешную / прерванную
                _taskService.LogFocusSession(userId, passedMinutes, false);

                await _botClient.SendMessage(userId, $"Фокус остановлен. Прошло {passedMinutes} минут.");
            }
            else
            {
                await _botClient.SendMessage(userId, "У вас нет активной фокус-сессии.");
            }
        }

        /// <summary>
        /// Показывает статус текущей сессии (если она есть).
        /// </summary>
        public async Task GetStatusAsync(long userId)
        {
            if (_sessions.TryGetValue(userId, out var session) && session.IsActive)
            {
                int passedMinutes = (int)(DateTime.UtcNow - session.StartTime).TotalMinutes;
                int remainingMinutes = session.DurationMinutes - passedMinutes;
                
                await _botClient.SendMessage(userId, $"Текущая сессия: прошло {passedMinutes} мин, осталось {remainingMinutes} мин из {session.DurationMinutes}.");
            }
            else
            {
                await _botClient.SendMessage(userId, "У вас нет активной фокус-сессии.");
            }
        }
    }
}
