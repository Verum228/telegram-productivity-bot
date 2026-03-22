using System;
using System.Collections.Concurrent;

namespace TelegramProductivityBot.Services
{
    /// <summary>
    /// Сервис для генерации случайных советов по продуктивности.
    /// </summary>
    public class AdviceService
    {
        private readonly TaskService _taskService;
        private readonly StreakService _streakService;

        // Память последнего совета для каждого пользователя (anti-repeat)
        private readonly ConcurrentDictionary<long, int> _lastAdvice = new();

        private const int AdviceCount = 15;

        public AdviceService(TaskService taskService, StreakService streakService)
        {
            _taskService = taskService;
            _streakService = streakService;
        }

        /// <summary>
        /// Возвращает случайный совет (всегда разный, без повторов подряд).
        /// </summary>
        public string GetAdvice(long userId, string lang)
        {
            int last = _lastAdvice.GetOrAdd(userId, -1);
            int index;

            do
            {
                index = Random.Shared.Next(1, AdviceCount + 1);
            }
            while (index == last);

            _lastAdvice[userId] = index;

            return LocalizationService.T($"stats_advice_{index}", lang);
        }
    }
}
