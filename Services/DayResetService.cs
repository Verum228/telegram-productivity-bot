using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;

namespace TelegramProductivityBot.Services
{
    public class DayResetService
    {
        private readonly ITelegramBotClient _botClient;
        private readonly TaskService _taskService;
        private readonly DayPlanService _dayPlanService;
        private readonly StreakService _streakService;
        private readonly CancellationTokenSource _cts;

        private DateTime _lastRunDate;

        public DayResetService(ITelegramBotClient botClient, TaskService taskService, DayPlanService dayPlanService, StreakService streakService)
        {
            _botClient = botClient;
            _taskService = taskService;
            _dayPlanService = dayPlanService;
            _streakService = streakService;
            _cts = new CancellationTokenSource();
            
            _lastRunDate = DateTime.Now.Date;

            _ = Task.Run(() => ResetLoopAsync(_cts.Token), _cts.Token);
        }

        public void Stop()
        {
            _cts.Cancel();
        }

        private async Task ResetLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), token);

                    var now = DateTime.Now;
                    if (now.Date > _lastRunDate)
                    {
                        var oldPlans = _taskService.GetActivePlansFromYesterday();

                        foreach (var plan in oldPlans)
                        {
                            bool hasFailedTasks = false;

                            if (!plan.MainDone && !plan.MainFailed && !string.IsNullOrEmpty(plan.MainTask))
                            {
                                await _dayPlanService.ProcessDayPlanTaskAsync(plan, 1, false);
                                hasFailedTasks = true;
                            }
                            
                            if (!plan.MediumDone && !plan.MediumFailed && !string.IsNullOrEmpty(plan.MediumTask))
                            {
                                await _dayPlanService.ProcessDayPlanTaskAsync(plan, 2, false);
                                hasFailedTasks = true;
                            }

                            if (!plan.EasyDone && !plan.EasyFailed && !string.IsNullOrEmpty(plan.EasyTask))
                            {
                                await _dayPlanService.ProcessDayPlanTaskAsync(plan, 3, false);
                                hasFailedTasks = true;
                            }

                            if (hasFailedTasks)
                            {
                                _streakService.ResetStreak(plan.UserId);
                                await _botClient.SendMessage(chatId: plan.UserId, text: "❌ День завершён.\nНевыполненные задачи провалены, стрик сброшен.");
                            }
                            
                            _taskService.SetPlanCompleted(plan.UserId);
                        }

                        _lastRunDate = now.Date;
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка в DayResetService: {ex.Message}");
            }
        }
    }
}
