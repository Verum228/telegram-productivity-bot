using System;

namespace TelegramProductivityBot.Models
{
    /// <summary>
    /// Модель профиля пользователя со статистикой опыта.
    /// </summary>
    public class UserProfile
    {
        public long UserId { get; set; }
        public int XP { get; set; }
        public int Level { get; set; }
        public int TasksCompleted { get; set; }
        public int FocusSessions { get; set; }
    }
}
