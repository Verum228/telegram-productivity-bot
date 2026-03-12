using System;

namespace TelegramProductivityBot.Models
{
    /// <summary>
    /// Модель ежедневного плана пользователя.
    /// </summary>
    public class DayPlan
    {
        public int Id { get; set; }
        public long UserId { get; set; }
        public string MainTask { get; set; } = string.Empty;
        public string MediumTask { get; set; } = string.Empty;
        public string EasyTask { get; set; } = string.Empty;
        public bool MainDone { get; set; }
        public bool MediumDone { get; set; }
        public bool EasyDone { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
