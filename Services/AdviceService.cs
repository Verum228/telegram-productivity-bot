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
        public string GetAdvice(long userId)
        {
            int tasks7d = _taskService.GetActivitySumLastDays(userId, "task", 7);
            int focus7d = _taskService.GetActivitySumLastDays(userId, "focus", 7);
            var streak = _streakService.GetStreak(userId);

            // 1. Похвала за высокий стрик
            if (streak.CurrentStreak >= 3)
            {
                return "🔥 Твоя серия дней просто огонь! Продолжай в том же духе, ты выработал отличную привычку. Главное — не сбивать темп!";
            }

            // 2. Мало фокус-сессий
            if (focus7d < 2)
            {
                return "💡 Я заметил, что ты редко используешь таймер Pomodoro. Попробуй включить /focus 25 сегодня — это поможет не отвлекаться и сделать дела быстрее.";
            }

            // 3. Мало выполненных задач
            if (tasks7d < 5)
            {
                return "📉 За последнюю неделю выполнено маловато задач. Возможно, твой план на день слишком амбициозен? Попробуй ставить более мелкие и легкие задачи в план, чтобы втянуться.";
            }

            // Дефолтный совет
            return "✨ Ты молодец! Совет дня: если задача кажется неподъемной, разбей её на 3 мелких шага и сделай первый.";
        }
    }
}
