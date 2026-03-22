using System;
using System.Collections.Concurrent;

namespace TelegramProductivityBot.Services
{
    /// <summary>
    /// Сервис для генерации советов на основе статистики пользователя.
    /// </summary>
    public class AdviceService
    {
        private readonly TaskService _taskService;
        private readonly StreakService _streakService;
        private readonly Random _random = new Random();

        // Память последнего совета для каждого пользователя (anti-repeat)
        private readonly ConcurrentDictionary<long, int> _lastAdviceIndex = new();

        private const int AdviceCount = 15;

        public AdviceService(TaskService taskService, StreakService streakService)
        {
            _taskService = taskService;
            _streakService = streakService;
        }

        /// <summary>
        /// Генерирует совет на основе активности за последние 7 дней и текущего стрика.
        /// </summary>
        public string GetAdvice(long userId, string lang)
        {
            int tasks7d = _taskService.GetActivitySumLastDays(userId, "task", 7);
            int focus7d = _taskService.GetActivitySumLastDays(userId, "focus", 7);
            var streak = _streakService.GetStreak(userId);

            // 1. Похвала за высокий стрик
            if (streak.CurrentStreak >= 3)
            {
                return LocalizationService.T("stats_advice_streak", lang);
            }

            // 2. Мало фокус-сессий
            if (focus7d < 2)
            {
                return LocalizationService.T("report_stats_advice", lang);
            }

            // 3. Мало выполненных задач
            if (tasks7d < 5)
            {
                return LocalizationService.T("stats_advice_tasks", lang);
            }

            // 4. Случайный совет из 15 (с защитой от повтора)
            int index = _random.Next(1, AdviceCount + 1);

            // Anti-repeat: если совпадает с прошлым — перегенерировать
            if (_lastAdviceIndex.TryGetValue(userId, out int lastIndex) && lastIndex == index)
            {
                index = (index % AdviceCount) + 1; // сдвигаем на 1
            }

            _lastAdviceIndex[userId] = index;

            string key = $"stats_advice_{index}";
            return LocalizationService.T(key, lang);
        }
    }
}
