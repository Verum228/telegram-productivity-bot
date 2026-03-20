using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using TelegramProductivityBot.Models;

namespace TelegramProductivityBot
{
    /// <summary>
    /// Сервис для управления задачами: сохранение, получение, обновление в SQLite.
    /// </summary>
    public class TaskService
    {
        private readonly string _connectionString = "Data Source=tasks.db";

        public TaskService()
        {
            InitializeDatabase();
        }

        /// <summary>
        /// Инициализация базы данных и создание таблиц.
        /// </summary>
        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                DROP TABLE IF EXISTS tasks;
                CREATE TABLE IF NOT EXISTS focus_logs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER,
                    Duration INTEGER,
                    IsSuccess INTEGER,
                    FinishedAt TEXT
                );
                CREATE TABLE IF NOT EXISTS user_settings (
                    UserId INTEGER,
                    SettingKey TEXT,
                    SettingValue TEXT,
                    PRIMARY KEY (UserId, SettingKey)
                );
                CREATE TABLE IF NOT EXISTS user_stats (
                    UserId INTEGER PRIMARY KEY,
                    XP INTEGER DEFAULT 0,
                    Level INTEGER DEFAULT 1,
                    TasksCompleted INTEGER DEFAULT 0,
                    FocusSessions INTEGER DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS streaks (
                    UserId INTEGER PRIMARY KEY,
                    CurrentStreak INTEGER DEFAULT 0,
                    BestStreak INTEGER DEFAULT 0,
                    LastSuccessDate TEXT
                );
                CREATE TABLE IF NOT EXISTS day_plans (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER,
                    MainTask TEXT,
                    MediumTask TEXT,
                    EasyTask TEXT,
                    MainDone INTEGER DEFAULT 0,
                    MediumDone INTEGER DEFAULT 0,
                    EasyDone INTEGER DEFAULT 0,
                    MainFailed INTEGER DEFAULT 0,
                    MediumFailed INTEGER DEFAULT 0,
                    EasyFailed INTEGER DEFAULT 0,
                    IsPlanCompleted INTEGER DEFAULT 0,
                    CreatedDate TEXT
                );
                CREATE TABLE IF NOT EXISTS activity_logs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER,
                    Type TEXT,
                    Value INTEGER,
                    CreatedDate TEXT
                );
            ";
            command.ExecuteNonQuery();

            // Пытаемся добавить новые колонки к day_plans, если они не существуют
            try
            {
                var alter1 = connection.CreateCommand();
                alter1.CommandText = "ALTER TABLE day_plans ADD COLUMN MainFailed INTEGER DEFAULT 0;";
                alter1.ExecuteNonQuery();
            } catch { }
            try {
                var alter2 = connection.CreateCommand();
                alter2.CommandText = "ALTER TABLE day_plans ADD COLUMN MediumFailed INTEGER DEFAULT 0;";
                alter2.ExecuteNonQuery();
            } catch { }
            try {
                var alter3 = connection.CreateCommand();
                alter3.CommandText = "ALTER TABLE day_plans ADD COLUMN EasyFailed INTEGER DEFAULT 0;";
                alter3.ExecuteNonQuery();
            } catch { }
            try {
                var alter4 = connection.CreateCommand();
                alter4.CommandText = "ALTER TABLE day_plans ADD COLUMN IsPlanCompleted INTEGER DEFAULT 0;";
                alter4.ExecuteNonQuery();
            } catch { }
        }



        /// <summary>
        /// Логирует результат прошедшей фокус-сессии.
        /// </summary>
        public void LogFocusSession(long userId, int duration, bool isSuccess)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO focus_logs (UserId, Duration, IsSuccess, FinishedAt)
                VALUES ($userId, $duration, $isSuccess, $finishedAt);
            ";
            command.Parameters.AddWithValue("$userId", userId);
            command.Parameters.AddWithValue("$duration", duration);
            command.Parameters.AddWithValue("$isSuccess", isSuccess ? 1 : 0);
            command.Parameters.AddWithValue("$finishedAt", DateTime.UtcNow.ToString("o"));

            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Сохраняет или обновляет настройку пользователя.
        /// </summary>
        public void SetSetting(long userId, string key, string value)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO user_settings (UserId, SettingKey, SettingValue)
                VALUES ($userId, $key, $value)
                ON CONFLICT(UserId, SettingKey) DO UPDATE SET SettingValue = $value;
            ";
            command.Parameters.AddWithValue("$userId", userId);
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);

            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Возвращает список ID пользователей, у которых установлена нужная настройка.
        /// </summary>
        public List<long> GetUsersWithSetting(string key, string value)
        {
            var userIds = new List<long>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT UserId FROM user_settings WHERE SettingKey = $key AND SettingValue = $value";
            command.Parameters.AddWithValue("$key", key);
            command.Parameters.AddWithValue("$value", value);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                userIds.Add(reader.GetInt64(0));
            }
            return userIds;
        }

        /// <summary>
        /// Подсчитывает количество выполненных задач у пользователя за сегодняшний день (UTC) через План Дня.
        /// </summary>
        public int GetTasksCompletedToday(long userId)
        {
            var plan = GetTodayPlan(userId);
            if (plan == null) return 0;
            int count = 0;
            if (plan.MainDone) count++;
            if (plan.MediumDone) count++;
            if (plan.EasyDone) count++;
            return count;
        }

        /// <summary>
        /// Подсчитывает количество выполненных фокус-сессий за сегодняшний день (UTC).
        /// </summary>
        public int GetFocusSessionsCompletedToday(long userId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM focus_logs WHERE UserId = $userId AND IsSuccess = 1 AND FinishedAt LIKE $todayPattern";
            string todayString = DateTime.UtcNow.ToString("yyyy-MM-dd") + "%";
            command.Parameters.AddWithValue("$userId", userId);
            command.Parameters.AddWithValue("$todayPattern", todayString);

            var result = command.ExecuteScalar();
            return Convert.ToInt32(result);
        }

        // --- STREAKS ---

        /// <summary>
        /// Возвращает или создает стрик для пользователя.
        /// </summary>
        public UserStreak GetUserStreak(long userId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT CurrentStreak, BestStreak, LastSuccessDate FROM streaks WHERE UserId = $userId";
            command.Parameters.AddWithValue("$userId", userId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new UserStreak
                {
                    UserId = userId,
                    CurrentStreak = reader.GetInt32(0),
                    BestStreak = reader.GetInt32(1),
                    LastSuccessDate = reader.IsDBNull(2) ? (DateTime?)null : DateTime.Parse(reader.GetString(2))
                };
            }

            // Создаем новую запись, если нет
            var insertCmd = connection.CreateCommand();
            insertCmd.CommandText = "INSERT INTO streaks (UserId) VALUES ($userId)";
            insertCmd.Parameters.AddWithValue("$userId", userId);
            insertCmd.ExecuteNonQuery();

            return new UserStreak
            {
                UserId = userId,
                CurrentStreak = 0,
                BestStreak = 0,
                LastSuccessDate = null
            };
        }

        /// <summary>
        /// Обновляет стрик пользователя в базе.
        /// </summary>
        public void UpdateUserStreak(UserStreak streak)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                UPDATE streaks 
                SET CurrentStreak = $current, BestStreak = $best, LastSuccessDate = $lastDate 
                WHERE UserId = $userId";
            
            command.Parameters.AddWithValue("$current", streak.CurrentStreak);
            command.Parameters.AddWithValue("$best", streak.BestStreak);
            command.Parameters.AddWithValue("$lastDate", streak.LastSuccessDate.HasValue ? streak.LastSuccessDate.Value.ToString("o") : (object)DBNull.Value);
            command.Parameters.AddWithValue("$userId", streak.UserId);

            command.ExecuteNonQuery();
        }

        // --- DAY PLANS ---

        /// <summary>
        /// Сохраняет или обновляет план на сегодня (заменяет, если уже есть).
        /// </summary>
        public void SaveTodayPlan(DayPlan plan)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Удаляем старый план на сегодня (чтобы не дублировать)
            var deleteCommand = connection.CreateCommand();
            deleteCommand.CommandText = "DELETE FROM day_plans WHERE UserId = $userId AND CreatedDate LIKE $todayPattern";
            string todayString = DateTime.UtcNow.ToString("yyyy-MM-dd") + "%";
            deleteCommand.Parameters.AddWithValue("$userId", plan.UserId);
            deleteCommand.Parameters.AddWithValue("$todayPattern", todayString);
            deleteCommand.ExecuteNonQuery();

            // Вставляем новый
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = @"
                INSERT INTO day_plans (UserId, MainTask, MediumTask, EasyTask, MainDone, MediumDone, EasyDone, MainFailed, MediumFailed, EasyFailed, IsPlanCompleted, CreatedDate)
                VALUES ($userId, $mainTask, $mediumTask, $easyTask, 0, 0, 0, 0, 0, 0, 0, $createdDate);
            ";
            insertCommand.Parameters.AddWithValue("$userId", plan.UserId);
            insertCommand.Parameters.AddWithValue("$mainTask", plan.MainTask);
            insertCommand.Parameters.AddWithValue("$mediumTask", plan.MediumTask);
            insertCommand.Parameters.AddWithValue("$easyTask", plan.EasyTask);
            insertCommand.Parameters.AddWithValue("$createdDate", DateTime.UtcNow.ToString("o"));
            insertCommand.ExecuteNonQuery();
        }

        /// <summary>
        /// Получает план на сегодняшний день. Если нет, возвращает null.
        /// </summary>
        public DayPlan? GetTodayPlan(long userId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, UserId, MainTask, MediumTask, EasyTask, MainDone, MediumDone, EasyDone, MainFailed, MediumFailed, EasyFailed, IsPlanCompleted, CreatedDate FROM day_plans WHERE UserId = $userId AND CreatedDate LIKE $todayPattern ORDER BY Id DESC LIMIT 1";
            string todayString = DateTime.UtcNow.ToString("yyyy-MM-dd") + "%";
            command.Parameters.AddWithValue("$userId", userId);
            command.Parameters.AddWithValue("$todayPattern", todayString);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new DayPlan
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt64(1),
                    MainTask = reader.GetString(2),
                    MediumTask = reader.GetString(3),
                    EasyTask = reader.GetString(4),
                    MainDone = reader.GetInt32(5) == 1,
                    MediumDone = reader.GetInt32(6) == 1,
                    EasyDone = reader.GetInt32(7) == 1,
                    MainFailed = reader.GetInt32(8) == 1,
                    MediumFailed = reader.GetInt32(9) == 1,
                    EasyFailed = reader.GetInt32(10) == 1,
                    IsPlanCompleted = reader.GetInt32(11) == 1,
                    CreatedDate = DateTime.Parse(reader.GetString(12))
                };
            }
            return null;
        }

        /// <summary>
        /// Обновляет текст конкретной задачи в плане на день
        /// </summary>
        public void UpdateDayPlanTask(long userId, int taskType, string newText)
        {
            var plan = GetTodayPlan(userId);
            if (plan == null) return;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            string column = taskType switch
            {
                1 => "MainTask",
                2 => "MediumTask",
                3 => "EasyTask",
                _ => "MainTask"
            };

            var command = connection.CreateCommand();
            command.CommandText = $"UPDATE day_plans SET {column} = $newText WHERE Id = $id";
            command.Parameters.AddWithValue("$newText", newText);
            command.Parameters.AddWithValue("$id", plan.Id);

            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Обновляет статус конкретной задачи (выполнено или провалено) в плане на день
        /// </summary>
        public void SetDayPlanTaskStatus(long userId, int taskType, bool isDone, bool isFailed)
        {
            var plan = GetTodayPlan(userId);
            if (plan == null) return;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            string doneColumn = taskType switch
            {
                1 => "MainDone",
                2 => "MediumDone",
                3 => "EasyDone",
                _ => "MainDone"
            };

            string failedColumn = taskType switch
            {
                1 => "MainFailed",
                2 => "MediumFailed",
                3 => "EasyFailed",
                _ => "MainFailed"
            };

            var command = connection.CreateCommand();
            command.CommandText = $"UPDATE day_plans SET {doneColumn} = $done, {failedColumn} = $failed WHERE Id = $id";
            command.Parameters.AddWithValue("$done", isDone ? 1 : 0);
            command.Parameters.AddWithValue("$failed", isFailed ? 1 : 0);
            command.Parameters.AddWithValue("$id", plan.Id);

            command.ExecuteNonQuery();
        }

        public void SetPlanCompleted(long userId)
        {
            var plan = GetTodayPlan(userId);
            if (plan == null) return;
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = $"UPDATE day_plans SET IsPlanCompleted = 1 WHERE Id = $id";
            command.Parameters.AddWithValue("$id", plan.Id);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Удаляет сегодняшний план пользователя
        /// </summary>
        public void DeleteDayPlan(long userId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM day_plans WHERE UserId = $userId AND CreatedDate LIKE $todayPattern";
            string todayString = DateTime.UtcNow.ToString("yyyy-MM-dd") + "%";
            command.Parameters.AddWithValue("$userId", userId);
            command.Parameters.AddWithValue("$todayPattern", todayString);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Возвращает количество полностью выполненных планов за последние N дней.
        /// </summary>
        public int GetCompletedDayPlansLastDays(long userId, int days)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            DateTime sinceDate = DateTime.UtcNow.Date.AddDays(-days);

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COUNT(*) 
                FROM day_plans 
                WHERE UserId = $userId 
                  AND MainDone = 1 AND MediumDone = 1 AND EasyDone = 1
                  AND CreatedDate >= $sinceDate";
            
            command.Parameters.AddWithValue("$userId", userId);
            command.Parameters.AddWithValue("$sinceDate", sinceDate.ToString("o"));

            var result = command.ExecuteScalar();
            return Convert.ToInt32(result);
        }

        /// <summary>
        /// Логирует активность пользователя (задача оценена, focus завершен, XP получен).
        /// </summary>
        public void LogActivity(long userId, string type, int value)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO activity_logs (UserId, Type, Value, CreatedDate)
                VALUES ($userId, $type, $value, $date)
            ";
            command.Parameters.AddWithValue("$userId", userId);
            command.Parameters.AddWithValue("$type", type);
            command.Parameters.AddWithValue("$value", value);
            command.Parameters.AddWithValue("$date", DateTime.UtcNow.ToString("o"));
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Возвращает сумму Value для указанного типа активности за последние N дней
        /// </summary>
        public int GetActivitySumLastDays(long userId, string type, int days)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            // Получаем дату N дней назад в формате ISO8601
            string dateThreshold = DateTime.UtcNow.AddDays(-days).ToString("o");

            command.CommandText = "SELECT SUM(Value) FROM activity_logs WHERE UserId = $userId AND Type = $type AND CreatedDate >= $date";
            command.Parameters.AddWithValue("$userId", userId);
            command.Parameters.AddWithValue("$type", type);
            command.Parameters.AddWithValue("$date", dateThreshold);

            var result = command.ExecuteScalar();
            if (result != DBNull.Value && result != null)
                return Convert.ToInt32(result);
            return 0;
        }

        /// <summary>
        /// Возвращает список всех уникальных пользователей, которые когда-либо взаимодействовали с ботом.
        /// </summary>
        public List<long> GetAllUniqueUsers()
        {
            var users = new HashSet<long>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            // Можно смотреть по user_stats, так как туда попадают все при первой стате
            command.CommandText = "SELECT DISTINCT UserId FROM user_stats";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                users.Add(reader.GetInt64(0));
            }
            return new List<long>(users);
        }

        /// <summary>
        /// Добавляет пользователя в статистику, если его там нет.
        /// </summary>
        private void EnsureUserExistsInStats(long userId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "INSERT OR IGNORE INTO user_stats (UserId, XP, Level, TasksCompleted, FocusSessions) VALUES ($userId, 0, 1, 0, 0)";
            command.Parameters.AddWithValue("$userId", userId);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Добавляет XP пользователю и обновляет уровень (100 XP = 1 лвл).
        /// </summary>
        public void AddXPToUser(long userId, int amount)
        {
            EnsureUserExistsInStats(userId);

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            // Читаем текущий XP
            var getCommand = connection.CreateCommand();
            getCommand.CommandText = "SELECT XP FROM user_stats WHERE UserId = $userId";
            getCommand.Parameters.AddWithValue("$userId", userId);
            var currentXp = Convert.ToInt32(getCommand.ExecuteScalar());

            int newXp = currentXp + amount;
            if (newXp < 0) newXp = 0;
            int newLevel = (newXp / 100) + 1;

            var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = "UPDATE user_stats SET XP = $newXp, Level = $newLevel WHERE UserId = $userId";
            updateCommand.Parameters.AddWithValue("$userId", userId);
            updateCommand.Parameters.AddWithValue("$newXp", newXp);
            updateCommand.Parameters.AddWithValue("$newLevel", newLevel);
            updateCommand.ExecuteNonQuery();
        }

        /// <summary>
        /// Увеличивает счетчик задачи или фокус-сессии.
        /// </summary>
        public void IncrementUserStat(long userId, string columnName)
        {
            EnsureUserExistsInStats(userId);

            // Защита от SQL-инъекций (используем только разрешенные колонки)
            if (columnName != "TasksCompleted" && columnName != "FocusSessions")
                return;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = $"UPDATE user_stats SET {columnName} = {columnName} + 1 WHERE UserId = $userId";
            command.Parameters.AddWithValue("$userId", userId);
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Получает профиль текущего пользователя со статистикой (модель UserProfile).
        /// </summary>
        public UserProfile GetUserProfile(long userId)
        {
            EnsureUserExistsInStats(userId);

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT UserId, XP, Level, TasksCompleted, FocusSessions FROM user_stats WHERE UserId = $userId";
            command.Parameters.AddWithValue("$userId", userId);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return new UserProfile
                {
                    UserId = reader.GetInt64(0),
                    XP = reader.GetInt32(1),
                    Level = reader.GetInt32(2),
                    TasksCompleted = reader.GetInt32(3),
                    FocusSessions = reader.GetInt32(4)
                };
            }
            return new UserProfile { UserId = userId, XP = 0, Level = 1, TasksCompleted = 0, FocusSessions = 0 };
        }
    }
}
