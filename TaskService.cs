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
            string[] alterations = new[]
            {
                "ALTER TABLE day_plans ADD COLUMN MainFailed INTEGER DEFAULT 0;",
                "ALTER TABLE day_plans ADD COLUMN MediumFailed INTEGER DEFAULT 0;",
                "ALTER TABLE day_plans ADD COLUMN EasyFailed INTEGER DEFAULT 0;",
                "ALTER TABLE day_plans ADD COLUMN IsPlanCompleted INTEGER DEFAULT 0;",
                "ALTER TABLE day_plans ADD COLUMN MainDeadline TEXT NULL;",
                "ALTER TABLE day_plans ADD COLUMN MediumDeadline TEXT NULL;",
                "ALTER TABLE day_plans ADD COLUMN EasyDeadline TEXT NULL;",
                "ALTER TABLE day_plans ADD COLUMN MainReminderStatus INTEGER DEFAULT 0;",
                "ALTER TABLE day_plans ADD COLUMN MediumReminderStatus INTEGER DEFAULT 0;",
                "ALTER TABLE day_plans ADD COLUMN EasyReminderStatus INTEGER DEFAULT 0;",
                "ALTER TABLE day_plans ADD COLUMN MainOverdueNotified INTEGER DEFAULT 0;",
                "ALTER TABLE day_plans ADD COLUMN MediumOverdueNotified INTEGER DEFAULT 0;",
                "ALTER TABLE day_plans ADD COLUMN EasyOverdueNotified INTEGER DEFAULT 0;"
            };

            foreach (var alt in alterations)
            {
                try
                {
                    var alterCmd = connection.CreateCommand();
                    alterCmd.CommandText = alt;
                    alterCmd.ExecuteNonQuery();
                }
                catch { } // Игнорируем ошибки (значит колонка уже существует)
            }
        }

        // --- ДЕДЛАЙНЫ ---

        public void SetTaskDeadline(long userId, int taskType, string? time)
        {
            var plan = GetTodayPlan(userId);
            if (plan == null) return;

            string dlColumn = taskType switch { 1 => "MainDeadline", 2 => "MediumDeadline", _ => "EasyDeadline" };
            string remColumn = taskType switch { 1 => "MainReminderStatus", 2 => "MediumReminderStatus", _ => "EasyReminderStatus" };
            string overColumn = taskType switch { 1 => "MainOverdueNotified", 2 => "MediumOverdueNotified", _ => "EasyOverdueNotified" };

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            
            // Сбрасываем флаги напоминаний при изменении дедлайна
            command.CommandText = $"UPDATE day_plans SET {dlColumn} = $time, {remColumn} = 0, {overColumn} = 0 WHERE Id = $id";
            command.Parameters.AddWithValue("$time", time != null ? (object)time : DBNull.Value);
            command.Parameters.AddWithValue("$id", plan.Id);
            command.ExecuteNonQuery();
        }

        public void SetTaskReminderStatus(long userId, int taskType, int status)
        {
            var plan = GetTodayPlan(userId);
            if (plan == null) return;
            string col = taskType switch { 1 => "MainReminderStatus", 2 => "MediumReminderStatus", _ => "EasyReminderStatus" };

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = $"UPDATE day_plans SET {col} = $status WHERE Id = $id";
            command.Parameters.AddWithValue("$status", status);
            command.Parameters.AddWithValue("$id", plan.Id);
            command.ExecuteNonQuery();
        }

        public void SetTaskOverdueNotified(long userId, int taskType)
        {
            var plan = GetTodayPlan(userId);
            if (plan == null) return;
            string col = taskType switch { 1 => "MainOverdueNotified", 2 => "MediumOverdueNotified", _ => "EasyOverdueNotified" };

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = $"UPDATE day_plans SET {col} = 1 WHERE Id = $id";
            command.Parameters.AddWithValue("$id", plan.Id);
            command.ExecuteNonQuery();
        }

        public List<DayPlan> GetActivePlansWithDeadlines()
        {
            var plans = new List<DayPlan>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            var command = connection.CreateCommand();
            string todayPattern = DateTime.UtcNow.ToString("yyyy-MM-dd") + "%";
            
            // Ищем активные планы, где есть хоть один дедлайн
            command.CommandText = @"
                SELECT Id, UserId, MainTask, MediumTask, EasyTask, MainDone, MediumDone, EasyDone, MainFailed, MediumFailed, EasyFailed, IsPlanCompleted, CreatedDate,
                       MainDeadline, MediumDeadline, EasyDeadline, MainReminderStatus, MediumReminderStatus, EasyReminderStatus, MainOverdueNotified, MediumOverdueNotified, EasyOverdueNotified
                FROM day_plans 
                WHERE CreatedDate LIKE $todayPattern 
                  AND IsPlanCompleted = 0 
                  AND (MainDeadline IS NOT NULL OR MediumDeadline IS NOT NULL OR EasyDeadline IS NOT NULL)";
            
            command.Parameters.AddWithValue("$todayPattern", todayPattern);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                plans.Add(MapDayPlan(reader));
            }
            return plans;
        }

        private DayPlan MapDayPlan(SqliteDataReader reader)
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
                CreatedDate = DateTime.Parse(reader.GetString(12)),
                MainDeadline = reader.IsDBNull(13) ? null : reader.GetString(13),
                MediumDeadline = reader.IsDBNull(14) ? null : reader.GetString(14),
                EasyDeadline = reader.IsDBNull(15) ? null : reader.GetString(15),
                MainReminderStatus = reader.GetInt32(16),
                MediumReminderStatus = reader.GetInt32(17),
                EasyReminderStatus = reader.GetInt32(18),
                MainOverdueNotified = reader.GetInt32(19) == 1,
                MediumOverdueNotified = reader.GetInt32(20) == 1,
                EasyOverdueNotified = reader.GetInt32(21) == 1
            };
        }

        // --- ОСТАЛЬНОЙ КОД ---

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

        public List<DayPlan> GetPast7DaysPlans(long userId)
        {
            var plans = new List<DayPlan>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, UserId, MainTask, MainDone, MediumTask, MediumDone, EasyTask, EasyDone, CreatedDate, IsPlanCompleted,
                       MainDeadline, MediumDeadline, EasyDeadline
                FROM day_plans
                WHERE UserId = $userId AND CreatedDate >= date('now', '-7 days')
                ORDER BY CreatedDate ASC";
            command.Parameters.AddWithValue("$userId", userId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                plans.Add(new DayPlan
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt64(1),
                    MainTask = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    MainDone = reader.GetInt32(3) == 1,
                    MediumTask = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    MediumDone = reader.GetInt32(5) == 1,
                    EasyTask = reader.IsDBNull(6) ? "" : reader.GetString(6),
                    EasyDone = reader.GetInt32(7) == 1,
                    CreatedDate = DateTime.Parse(reader.GetString(8)), // Changed from Date to CreatedDate
                    IsPlanCompleted = !reader.IsDBNull(9) && reader.GetInt32(9) == 1,
                    MainDeadline = reader.IsDBNull(10) ? null : reader.GetString(10),
                    MediumDeadline = reader.IsDBNull(11) ? null : reader.GetString(11),
                    EasyDeadline = reader.IsDBNull(12) ? null : reader.GetString(12)
                });
            }

            return plans;
        }

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
                INSERT INTO day_plans (UserId, MainTask, MediumTask, EasyTask, MainDone, MediumDone, EasyDone, MainFailed, MediumFailed, EasyFailed, IsPlanCompleted, CreatedDate, 
                                       MainDeadline, MediumDeadline, EasyDeadline, MainReminderStatus, MediumReminderStatus, EasyReminderStatus, MainOverdueNotified, MediumOverdueNotified, EasyOverdueNotified)
                VALUES ($userId, $mainTask, $mediumTask, $easyTask, 0, 0, 0, 0, 0, 0, 0, $createdDate, 
                        $mainDl, $medDl, $easyDl, 0, 0, 0, 0, 0, 0);
            ";
            insertCommand.Parameters.AddWithValue("$userId", plan.UserId);
            insertCommand.Parameters.AddWithValue("$mainTask", plan.MainTask);
            insertCommand.Parameters.AddWithValue("$mediumTask", plan.MediumTask);
            insertCommand.Parameters.AddWithValue("$easyTask", plan.EasyTask);
            insertCommand.Parameters.AddWithValue("$createdDate", DateTime.UtcNow.ToString("o"));
            insertCommand.Parameters.AddWithValue("$mainDl", plan.MainDeadline != null ? (object)plan.MainDeadline : DBNull.Value);
            insertCommand.Parameters.AddWithValue("$medDl", plan.MediumDeadline != null ? (object)plan.MediumDeadline : DBNull.Value);
            insertCommand.Parameters.AddWithValue("$easyDl", plan.EasyDeadline != null ? (object)plan.EasyDeadline : DBNull.Value);
            insertCommand.ExecuteNonQuery();
        }

        public DayPlan? GetTodayPlan(long userId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, UserId, MainTask, MediumTask, EasyTask, MainDone, MediumDone, EasyDone, MainFailed, MediumFailed, EasyFailed, IsPlanCompleted, CreatedDate,
                       MainDeadline, MediumDeadline, EasyDeadline, MainReminderStatus, MediumReminderStatus, EasyReminderStatus, MainOverdueNotified, MediumOverdueNotified, EasyOverdueNotified
                FROM day_plans 
                WHERE UserId = $userId AND CreatedDate LIKE $todayPattern 
                ORDER BY Id DESC LIMIT 1";
            string todayString = DateTime.UtcNow.ToString("yyyy-MM-dd") + "%";
            command.Parameters.AddWithValue("$userId", userId);
            command.Parameters.AddWithValue("$todayPattern", todayString);

            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                return MapDayPlan(reader);
            }
            return null;
        }

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

        // --- ЛОГИ И СТАТИСТИКА ---

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

        public int GetActivitySumLastDays(long userId, string type, int days)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
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

        public List<long> GetAllUniqueUsers()
        {
            var users = new HashSet<long>();
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT DISTINCT UserId FROM user_stats";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                users.Add(reader.GetInt64(0));
            }
            return new List<long>(users);
        }

        private void EnsureUserExistsInStats(long userId)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = "INSERT OR IGNORE INTO user_stats (UserId, XP, Level, TasksCompleted, FocusSessions) VALUES ($userId, 0, 1, 0, 0)";
            command.Parameters.AddWithValue("$userId", userId);
            command.ExecuteNonQuery();
        }

        public void AddXPToUser(long userId, int amount)
        {
            EnsureUserExistsInStats(userId);

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

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

        public void IncrementUserStat(long userId, string columnName)
        {
            EnsureUserExistsInStats(userId);

            if (columnName != "TasksCompleted" && columnName != "FocusSessions")
                return;

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = $"UPDATE user_stats SET {columnName} = {columnName} + 1 WHERE UserId = $userId";
            command.Parameters.AddWithValue("$userId", userId);
            command.ExecuteNonQuery();
        }

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
