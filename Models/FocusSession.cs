using System;
using System.Threading;

namespace TelegramProductivityBot.Models
{
    /// <summary>
    /// Модель сессии фокуса (Pomodoro) для пользователя.
    /// </summary>
    public class FocusSession
    {
        public long UserId { get; set; }
        public DateTime StartTime { get; set; }
        public int DurationMinutes { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; set; } = new CancellationTokenSource();
        
        public bool IsActive => !CancellationTokenSource.IsCancellationRequested;
    }
}
