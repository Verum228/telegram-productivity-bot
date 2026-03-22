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
        private readonly StatsService _statsService;

        public DayPlanService(ITelegramBotClient botClient, TaskService taskService, StreakService streakService, StatsService statsService)
        {
            _botClient = botClient;
            _taskService = taskService;
            _streakService = streakService;
            _statsService = statsService;
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

        public async Task ProcessDayPlanTaskAsync(long userId, int taskType, bool markAsDone)
        {
            var plan = GetTodayPlan(userId);
            if (plan == null) return;
            await ProcessDayPlanTaskAsync(plan, taskType, markAsDone);
        }

        public async Task ProcessDayPlanTaskAsync(Models.DayPlan plan, int taskType, bool markAsDone)
        {
            long userId = plan.UserId;

            if (plan.IsPlanCompleted) {
                await _botClient.SendMessage(chatId: userId, text: "План дня завершён 🔥 Изменения больше не принимаются.");
                return;
            }

            bool currentDone = false;
            bool currentFailed = false;
            switch (taskType) {
                case 1: currentDone = plan.MainDone; currentFailed = plan.MainFailed; break;
                case 2: currentDone = plan.MediumDone; currentFailed = plan.MediumFailed; break;
                case 3: currentDone = plan.EasyDone; currentFailed = plan.EasyFailed; break;
            }
            
            if (markAsDone && currentDone) {
                await _botClient.SendMessage(chatId: userId, text: "Статус уже установлен");
                return;
            }
            if (!markAsDone && currentFailed) {
                await _botClient.SendMessage(chatId: userId, text: "Статус уже установлен");
                return;
            }
            
            int xpChange = 0;
            if (markAsDone) {
                xpChange = currentFailed ? 15 : 10;
            } else {
                xpChange = currentDone ? -15 : -5;
            }
            
            _taskService.SetDayPlanTaskStatus(plan.Id, taskType, markAsDone, !markAsDone);
            _statsService.AddXP(userId, xpChange);
            
            // Перечитываем актуальное состояние конкретно этого плана, если нужно, 
            // но поскольку мы обновили один статус, можно обновить объект локально для прогресса
            switch (taskType) {
                case 1: plan.MainDone = markAsDone; plan.MainFailed = !markAsDone; break;
                case 2: plan.MediumDone = markAsDone; plan.MediumFailed = !markAsDone; break;
                case 3: plan.EasyDone = markAsDone; plan.EasyFailed = !markAsDone; break;
            }

            int doneCount = (plan.MainDone ? 1 : 0) + (plan.MediumDone ? 1 : 0) + (plan.EasyDone ? 1 : 0);
            
            string xpPrefix = xpChange > 0 ? "+" : "";
            string icon = markAsDone ? "✔" : "❌";
            string actionWord = markAsDone ? "выполнена" : "провалена";

            await _botClient.SendMessage(
                chatId: userId, 
                text: $"{icon} {GetTaskTypeName(taskType)} {actionWord}\n{xpPrefix}{xpChange} XP\n\nПрогресс: {doneCount} / 3");

            if (plan.MainDone && plan.MediumDone && plan.EasyDone && !plan.IsPlanCompleted) {
                _taskService.SetPlanCompleted(userId);
                bool streakIncreased = _streakService.RecordDaySuccess(userId);
                await _botClient.SendMessage(chatId: userId, text: "План дня завершён 🔥");
                if (streakIncreased) {
                    var streak = _streakService.GetStreak(userId);
                    await _botClient.SendMessage(
                        chatId: userId,
                        text: $"🔥 Твой стрик теперь составляет {streak.CurrentStreak} дней!");
                }
            }
        }

        private string GetTaskTypeName(int type) => type switch {
            1 => "Главная",
            2 => "Средняя",
            3 => "Лёгкая",
            _ => "Задача"
        };


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
                await _botClient.SendMessage(chatId: userId, text: "На сегодня плана не было. Невозможно построить отчёт.");
                return;
            }

            int tasksDoneToday = _taskService.GetTasksCompletedToday(userId);
            int focusDoneToday = _taskService.GetFocusSessionsCompletedToday(userId);

            string mainStatus = plan.MainDone ? "✔" : plan.MainFailed ? "❌" : "⏳";
            string mediumStatus = plan.MediumDone ? "✔" : plan.MediumFailed ? "❌" : "⏳";
            string easyStatus = plan.EasyDone ? "✔" : plan.EasyFailed ? "❌" : "⏳";

            string report = "Отчёт за сегодня:\n\n";
            report += $"🔥 Главная задача ({mainStatus}):\n{plan.MainTask}\n\n";
            report += $"⚙️ Средняя задача ({mediumStatus}):\n{plan.MediumTask}\n\n";
            report += $"🟢 Лёгкая задача ({easyStatus}):\n{plan.EasyTask}\n\n";
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

            await _botClient.SendMessage(chatId: userId, text: report);
        }
    }
}
