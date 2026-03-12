using System;
using TelegramProductivityBot.Models;

namespace TelegramProductivityBot.Services
{
    /// <summary>
    /// Сервис для управления серией дней (Streak)
    /// </summary>
    public class StreakService
    {
        private readonly TaskService _taskService;

        public StreakService(TaskService taskService)
        {
            _taskService = taskService;
        }

        /// <summary>
        /// Вызывается, когда пользователь хочет посмотреть свой стрик.
        /// Проверяет, не сбросился ли стрик из-за пропуска дней, и возвращает актуальный.
        /// </summary>
        public UserStreak GetStreak(long userId)
        {
            var streak = _taskService.GetUserStreak(userId);
            return AdjustStreakForMissedDays(streak);
        }

        /// <summary>
        /// Вызывается, когда пользователь успешно выполняет все задачи плана дня.
        /// </summary>
        public bool RecordDaySuccess(long userId)
        {
            var streak = GetStreak(userId);
            DateTime today = DateTime.UtcNow.Date;

            // Если сегодня уже успешно, не начисляем дважды
            if (streak.LastSuccessDate.HasValue && streak.LastSuccessDate.Value.Date == today)
            {
                return false; 
            }

            // Увеличиваем стрик
            streak.CurrentStreak++;
            if (streak.CurrentStreak > streak.BestStreak)
            {
                streak.BestStreak = streak.CurrentStreak;
            }
            streak.LastSuccessDate = today;

            _taskService.UpdateUserStreak(streak);
            Console.WriteLine($"[{DateTime.Now}] Streak: Пользователь {userId} увеличил стрик до {streak.CurrentStreak}.");
            return true;
        }

        /// <summary>
        /// Логика обнуления стрика при пропуске более чем одного дня
        /// </summary>
        private UserStreak AdjustStreakForMissedDays(UserStreak streak)
        {
            if (!streak.LastSuccessDate.HasValue)
                return streak;

            DateTime today = DateTime.UtcNow.Date;
            DateTime last = streak.LastSuccessDate.Value.Date;
            
            // Если разница больше 1 дня (например, вчера не выполнил), стрик прерывается
            if ((today - last).TotalDays > 1)
            {
                streak.CurrentStreak = 0;
                // LastSuccessDate не обнуляем, чтобы знать, когда был последний успех
                _taskService.UpdateUserStreak(streak);
                Console.WriteLine($"[{DateTime.Now}] Streak: Пользователь {streak.UserId} пропустил день. Стрик обнулен.");
            }

            return streak;
        }
    }
}
