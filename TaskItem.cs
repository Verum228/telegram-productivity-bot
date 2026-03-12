using System;

namespace TelegramProductivityBot
{
    /// <summary>
    /// Модель задачи, отображающая структуру в базе данных.
    /// </summary>
    public class TaskItem
    {
        public int Id { get; set; }
        public long UserId { get; set; }
        public string Text { get; set; } = string.Empty;
        public bool IsDone { get; set; }
        public DateTime CreatedDate { get; set; }
    }
}
