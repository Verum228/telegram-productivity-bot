using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using TelegramProductivityBot.Models;

namespace TelegramProductivityBot.Services
{
    /// <summary>
    /// Сервис для управления долгосрочными задачами (4 слота = 4 LongTask)
    /// </summary>
    public class LongTaskService
    {
        private readonly string _connectionString = "Data Source=tasks.db";

        public LongTaskService()
        {
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS long_tasks (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    Slot INTEGER NOT NULL,
                    Text TEXT NOT NULL,
                    IsDone INTEGER NOT NULL DEFAULT 0,
                    CreatedAt TEXT,
                    DoneAt TEXT,
                    UNIQUE(UserId, Slot)
                );
            ";
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Возвращает все задачи для указанного пользователя (как выполненные, так и нет).
        /// </summary>
        public async Task<List<LongTask>> GetLongTasksAsync(long userId)
        {
            var tasks = new List<LongTask>();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, UserId, Slot, Text, IsDone, CreatedAt, DoneAt FROM long_tasks WHERE UserId = $userId ORDER BY Slot ASC";
            command.Parameters.AddWithValue("$userId", userId);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                tasks.Add(new LongTask
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt64(1),
                    Slot = reader.GetInt32(2),
                    Text = reader.GetString(3),
                    IsDone = reader.GetInt32(4) == 1,
                    CreatedAt = DateTime.Parse(reader.GetString(5)),
                    DoneAt = reader.IsDBNull(6) ? (DateTime?)null : DateTime.Parse(reader.GetString(6))
                });
            }
            return tasks;
        }

        /// <summary>
        /// Возвращает задачу для конкретного слота 1-4.
        /// </summary>
        public async Task<LongTask?> GetLongTaskAsync(long userId, int slot)
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, UserId, Slot, Text, IsDone, CreatedAt, DoneAt FROM long_tasks WHERE UserId = $userId AND Slot = $slot";
            command.Parameters.AddWithValue("$userId", userId);
            command.Parameters.AddWithValue("$slot", slot);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new LongTask
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt64(1),
                    Slot = reader.GetInt32(2),
                    Text = reader.GetString(3),
                    IsDone = reader.GetInt32(4) == 1,
                    CreatedAt = DateTime.Parse(reader.GetString(5)),
                    DoneAt = reader.IsDBNull(6) ? (DateTime?)null : DateTime.Parse(reader.GetString(6))
                };
            }
            return null;
        }

        /// <summary>
        /// Создает или перезаписывает задачу в выбранном слоте.
        /// </summary>
        public async Task AddOrUpdateLongTaskAsync(long userId, int slot, string text)
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO long_tasks (UserId, Slot, Text, IsDone, CreatedAt)
                VALUES ($userId, $slot, $text, 0, $createdAt)
                ON CONFLICT(UserId, Slot) DO UPDATE SET Text = $text, IsDone = 0, DoneAt = NULL;
            ";
            command.Parameters.AddWithValue("$userId", userId);
            command.Parameters.AddWithValue("$slot", slot);
            command.Parameters.AddWithValue("$text", text);
            command.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("o"));

            await command.ExecuteNonQueryAsync();
            Console.WriteLine($"[{DateTime.Now}] LongTask: Пользователь {userId} обновил слот {slot} ({text}).");
        }

        /// <summary>
        /// Отмечает долгосрочную задачу выполненной или восстанавливает её.
        /// </summary>
        public async Task SetLongTaskDoneAsync(long userId, int slot, bool done)
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "UPDATE long_tasks SET IsDone = $done, DoneAt = $doneAt WHERE UserId = $userId AND Slot = $slot";
            command.Parameters.AddWithValue("$done", done ? 1 : 0);
            command.Parameters.AddWithValue("$doneAt", done ? DateTime.UtcNow.ToString("o") : (object)DBNull.Value);
            command.Parameters.AddWithValue("$userId", userId);
            command.Parameters.AddWithValue("$slot", slot);

            await command.ExecuteNonQueryAsync();
            Console.WriteLine($"[{DateTime.Now}] LongTask: Слот {slot} юзера {userId} помечен как done={done}.");
        }

        /// <summary>
        /// Удаляет задачу из слота. Слот становится доступным.
        /// </summary>
        public async Task DeleteLongTaskAsync(long userId, int slot)
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM long_tasks WHERE UserId = $userId AND Slot = $slot";
            command.Parameters.AddWithValue("$userId", userId);
            command.Parameters.AddWithValue("$slot", slot);

            await command.ExecuteNonQueryAsync();
            Console.WriteLine($"[{DateTime.Now}] LongTask: Пользователь {userId} удалил слот {slot}.");
        }
    }
}
