using System;
using TelegramProductivityBot.Models;

namespace TelegramProductivityBot.Services
{
    /// <summary>
    /// Сервис для управления опытом (XP), уровнями и статистикой пользователя.
    /// </summary>
    public class StatsService
    {
        private readonly TaskService _taskService;
        private readonly ActivityService _activityService;

        public StatsService(TaskService taskService, ActivityService activityService)
        {
            _taskService = taskService;
            _activityService = activityService;
        }

        /// <summary>
        /// Добавляет опыт пользователю и обновляет уровень.
        /// </summary>
        public void AddXP(long userId, int amount)
        {
            _taskService.AddXPToUser(userId, amount);
            _activityService.LogActivity(userId, "xp", amount);
        }

        /// <summary>
        /// Увеличивает счетчик выполненных задач.
        /// </summary>
        public void UpdateTaskCompleted(long userId)
        {
            _taskService.IncrementUserStat(userId, "TasksCompleted");
            _activityService.LogActivity(userId, "task", 1);
            AddXP(userId, 10); // Выполнение задачи: +10 XP
        }

        /// <summary>
        /// Увеличивает счетчик выполненных Pomodoro сессий.
        /// </summary>
        public void UpdateFocusCompleted(long userId)
        {
            _taskService.IncrementUserStat(userId, "FocusSessions");
            _activityService.LogActivity(userId, "focus", 1);
            AddXP(userId, 20); // Завершение Pomodoro: +20 XP
        }

        /// <summary>
        /// Получает профиль текущего пользователя.
        /// </summary>
        public UserProfile GetProfile(long userId)
        {
            return _taskService.GetUserProfile(userId);
        }
    }
}
