using System;

namespace TelegramProductivityBot.Models
{
    /// <summary>
    /// Модель для системы серии дней (Стрика).
    /// </summary>
    public class UserStreak
    {
        public long UserId { get; set; }
        public int CurrentStreak { get; set; }
        public int BestStreak { get; set; }
        public DateTime? LastSuccessDate { get; set; }
    }
}
