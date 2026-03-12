using System;
using System.Threading.Tasks;
using Telegram.Bot;
using TelegramProductivityBot.Models;

namespace TelegramProductivityBot.Services
{
    /// <summary>
    /// Сервис для управления ежедневным планированием (утро/вечер).
    /// </summary>
    public class DayPlanService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly TaskService _taskService;
        private readonly StreakService _streakService;

        public DayPlanService(ITelegramBotClient botClient, TaskService taskService, StreakService streakService)
        {
            _botClient = botClient;
            _taskService = taskService;
            _streakService = streakService;
        }

        /// <summary>
        /// Записывает сформированный план в БД
        /// </summary>
        public void CreatePlan(DayPlan plan)
        {
            _taskService.SaveTodayPlan(plan);
        }

        /// <summary>
        /// Получает план на сегодня
        /// </summary>
        public DayPlan? GetTodayPlan(long userId)
        {
            return _taskService.GetTodayPlan(userId);
        }

        public void UpdateDayPlanTask(long userId, int taskType, string newText)
        {
            _taskService.UpdateDayPlanTask(userId, taskType, newText);
        }

        public async Task MarkDayPlanTaskDoneAsync(long userId, int taskType)
        {
            _taskService.MarkDayPlanTaskDone(userId, taskType);
            
            // Проверяем, не выполнены ли все 3 задачи
            var plan = GetTodayPlan(userId);
            if (plan != null && plan.MainDone && plan.MediumDone && plan.EasyDone)
            {
                bool streakIncreased = _streakService.RecordDaySuccess(userId);
                if (streakIncreased)
                {
                    var streak = _streakService.GetStreak(userId);
                    await _botClient.SendMessage(
                        chatId: userId,
                        text: $"🎉 Отличная работа! Ты полностью выполнил план на день.\n🔥 Твой стрик теперь составляет {streak.CurrentStreak} дней!");
                }
            }
        }

        public void DeleteDayPlan(long userId)
        {
            _taskService.DeleteDayPlan(userId);
        }

        /// <summary>
        /// Генерирует и отправляет вечерний отчет продуктивности.
        /// </summary>
        public async Task GenerateDailyReportAsync(long userId)
        {
            var plan = GetTodayPlan(userId);
            if (plan == null)
            {
                await _botClient.SendMessage(userId, "На сегодня плана не было. Невозможно построить отчёт.");
                return;
            }

            int tasksDoneToday = _taskService.GetTasksCompletedToday(userId);
            int focusDoneToday = _taskService.GetFocusSessionsCompletedToday(userId);

            string report = "Отчёт за сегодня:\n\n";
            report += $"🔥 Главная задача:\n{plan.MainTask}\n\n";
            report += $"⚙️ Средняя задача:\n{plan.MediumTask}\n\n";
            report += $"🟢 Лёгкая задача:\n{plan.EasyTask}\n\n";
            report += $"Всего выполнено задач:\n{tasksDoneToday}\n\n";
            report += $"Фокус-сессий: {focusDoneToday}\n\n";

            // Расчет процента. Так как в плане 3 задачи, считаем 3 выполненные задачи за 100%
            float averagePercent = Math.Min(100f, (tasksDoneToday / 3f) * 100f);

            string mood = "";
            if (averagePercent >= 100)
            {
                mood = "Отличный день 🔥";
            }
            else if (averagePercent >= 66)
            {
                mood = "Хороший день 👍";
            }
            else if (averagePercent >= 33)
            {
                mood = "Нормальный день";
            }
            else
            {
                mood = "Нужно улучшить дисциплину";
            }

            report += $"Выполнение плана (≈ {Math.Round(averagePercent)}%)\n";
            report += $"Оценка продуктивности:\n{mood}";

            await _botClient.SendMessage(userId, report);
        }
    }
}
