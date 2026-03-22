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
        public string GetWeeklyStatsReport(long userId, string lang)
        {
            var plans = _taskService.GetPast7DaysPlans(userId);
            int main = 0, medium = 0, easy = 0;
            foreach(var p in plans) {
                if(p.MainDone) main++;
                if(p.MediumDone) medium++;
                if(p.EasyDone) easy++;
            }
            int total = main + medium + easy;

            int xp7d = _taskService.GetActivitySumLastDays(userId, "xp", 7);

            return LocalizationService.T("report_stats_week", lang) +
                   LocalizationService.T("stats_completed", lang).Replace("{total}", total.ToString()) +
                   LocalizationService.T("stats_main", lang).Replace("{main}", main.ToString()) +
                   LocalizationService.T("stats_medium", lang).Replace("{medium}", medium.ToString()) +
                   LocalizationService.T("stats_easy", lang).Replace("{easy}", easy.ToString()) +
                   LocalizationService.T("stats_xp", lang).Replace("{xp}", xp7d.ToString());
        }

        public string GetMonthlyStatsReport(long userId, string lang)
        {
            var plans = _taskService.GetPastDaysPlans(userId, 30);
            int main = 0, medium = 0, easy = 0;
            foreach(var p in plans) {
                if(p.MainDone) main++;
                if(p.MediumDone) medium++;
                if(p.EasyDone) easy++;
            }
            int total = main + medium + easy;

            int xp30d = _taskService.GetActivitySumLastDays(userId, "xp", 30);

            return LocalizationService.T("report_stats_month", lang) +
                   LocalizationService.T("stats_completed", lang).Replace("{total}", total.ToString()) +
                   LocalizationService.T("stats_main", lang).Replace("{main}", main.ToString()) +
                   LocalizationService.T("stats_medium", lang).Replace("{medium}", medium.ToString()) +
                   LocalizationService.T("stats_easy", lang).Replace("{easy}", easy.ToString()) +
                   LocalizationService.T("stats_xp", lang).Replace("{xp}", xp30d.ToString());
        }
    }
}
