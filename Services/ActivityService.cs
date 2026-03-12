using System;

namespace TelegramProductivityBot.Services
{
    /// <summary>
    /// Сервис для логирования ежедневной активности и сбора статистики по неделям.
    /// </summary>
    public class ActivityService
    {
        private readonly TaskService _taskService;

        public ActivityService(TaskService taskService)
        {
            _taskService = taskService;
        }

        /// <summary>
        /// Логирование активности (тип: task, focus, xp)
        /// </summary>
        public void LogActivity(long userId, string type, int value)
        {
            _taskService.LogActivity(userId, type, value);
        }

        /// <summary>
        /// Возвращает статистику за последние 7 дней в виде отформатированной строки
        /// </summary>
        public string GetWeeklyStatsReport(long userId)
        {
            int tasks7d = _taskService.GetActivitySumLastDays(userId, "task", 7);
            int focus7d = _taskService.GetActivitySumLastDays(userId, "focus", 7);
            int xp7d = _taskService.GetActivitySumLastDays(userId, "xp", 7);

            string report = "Статистика за неделю:\n\n";
            report += $"Задачи: {tasks7d}\n";
            report += $"Фокус-сессии: {focus7d}\n";
            report += $"Получено XP: {xp7d}";

            return report;
        }

        /// <summary>
        /// Возвращает статистику за последние 30 дней в виде отформатированной строки
        /// </summary>
        public string GetMonthlyStatsReport(long userId)
        {
            int tasks30d = _taskService.GetActivitySumLastDays(userId, "task", 30);
            int focus30d = _taskService.GetActivitySumLastDays(userId, "focus", 30);
            int xp30d = _taskService.GetActivitySumLastDays(userId, "xp", 30);
            int plans30d = _taskService.GetCompletedDayPlansLastDays(userId, 30);

            string report = "📅 Статистика за месяц:\n\n";
            report += $"✅ Задачи: {tasks30d}\n";
            report += $"🎯 Фокус-сессии: {focus30d}\n";
            report += $"✨ Получено XP: {xp30d}\n";
            report += $"🏆 Выполнено планов дня: {plans30d}";

            return report;
        }
    }
}
