using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;

namespace TelegramProductivityBot.Services
{
    /// <summary>
    /// Фоновый сервис для контроля дедлайнов задач Day Plan.
    /// Отправляет напоминания за 30 и 10 минут, а также автоматически
    /// проваливает задачи, если дедлайн пропущен.
    /// </summary>
    public class DeadlineService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly TaskService _taskService;
        private readonly DayPlanService _dayPlanService;
        private readonly StatsService _statsService;
        private readonly CancellationTokenSource _cts;

        public DeadlineService(ITelegramBotClient botClient, TaskService taskService, DayPlanService dayPlanService, StatsService statsService)
        {
            _botClient = botClient;
            _taskService = taskService;
            _dayPlanService = dayPlanService;
            _statsService = statsService;
            _cts = new CancellationTokenSource();

            _ = Task.Run(() => CheckDeadlinesLoopAsync(_cts.Token), _cts.Token);
        }

        public void Stop()
        {
            _cts.Cancel();
        }

        private async Task CheckDeadlinesLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // Цикл проверки каждую 1 минуту
                    await Task.Delay(TimeSpan.FromSeconds(60), token);

                    var activePlans = _taskService.GetActivePlansWithDeadlines();
                    var now = DateTime.Now;

                    foreach (var plan in activePlans)
                    {
                        await ProcessTaskDeadlineAsync(1, plan, plan.MainDeadline, plan.MainDone, plan.MainFailed, plan.MainReminderStatus, plan.MainOverdueNotified);
                        await ProcessTaskDeadlineAsync(2, plan, plan.MediumDeadline, plan.MediumDone, plan.MediumFailed, plan.MediumReminderStatus, plan.MediumOverdueNotified);
                        await ProcessTaskDeadlineAsync(3, plan, plan.EasyDeadline, plan.EasyDone, plan.EasyFailed, plan.EasyReminderStatus, plan.EasyOverdueNotified);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в DeadlineService: {ex.Message}");
            }
        }

        private async Task ProcessTaskDeadlineAsync(int taskType, Models.DayPlan plan, string? deadlineStr, bool isDone, bool isFailed, int reminderStatus, bool isNotified)
        {
            if (string.IsNullOrEmpty(deadlineStr) || isDone || isFailed) return;

            string taskText = taskType switch {
                1 => plan.MainTask ?? "Главная",
                2 => plan.MediumTask ?? "Средняя",
                _ => plan.EasyTask ?? "Лёгкая"
            };

            if (!TimeSpan.TryParse(deadlineStr, out TimeSpan deadlineTimeSpan)) return;
            var now = DateTime.Now;
            var deadlineTime = now.Date.Add(deadlineTimeSpan);

            var diff = deadlineTime - now;

            // 1. OVERDUE CHECK (FIRST)
            if (now >= deadlineTime && !isDone && !isFailed && !isNotified)
            {
                await _dayPlanService.ProcessDayPlanTaskAsync(plan.UserId, taskType, false);
                _taskService.SetTaskOverdueNotified(plan.UserId, taskType);
                await _botClient.SendMessage(chatId: plan.UserId, text: $"❌ Дедлайн пропущен: {taskText}\n-5 XP");
                return;
            }

            // 2. 10 MINUTES REMINDER
            if (diff.TotalMinutes <= 10 && diff.TotalMinutes > 0 && reminderStatus < 2)
            {
                await _botClient.SendMessage(chatId: plan.UserId, text: $"⚠️ 10 минут до дедлайна: {taskText}");
                _taskService.SetTaskReminderStatus(plan.UserId, taskType, 2);
                return;
            }

            // 3. 30 MINUTES REMINDER
            if (diff.TotalMinutes <= 30 && diff.TotalMinutes > 0 && reminderStatus < 1)
            {
                await _botClient.SendMessage(chatId: plan.UserId, text: $"⏰ Через 30 минут дедлайн: {taskText}");
                _taskService.SetTaskReminderStatus(plan.UserId, taskType, 1);
                return;
            }
        }
    }
}
