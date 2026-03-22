using System;

namespace TelegramProductivityBot.Services
{
    /// <summary>
    /// Сервис для генерации советов на основе статистики пользователя.
    /// </summary>
    public class AdviceService
    {
        private readonly TaskService _taskService;
        private readonly StreakService _streakService;

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

            // Дефолтный совет
            return LocalizationService.T("stats_advice_default", lang);
        }
    }
}
