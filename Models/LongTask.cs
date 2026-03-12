using System;

namespace TelegramProductivityBot.Models
{
    /// <summary>
    /// Модель долгосрочной задачи (до 4 слотов).
    /// </summary>
    public class LongTask
    {
        public int Id { get; set; }
        public long UserId { get; set; }
        public int Slot { get; set; } // 1..4
        public string Text { get; set; } = string.Empty;
        public bool IsDone { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DoneAt { get; set; }
    }
}
