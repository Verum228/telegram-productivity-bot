using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramProductivityBot.Models;
using TelegramProductivityBot.Services;
namespace TelegramProductivityBot
{
    /// <summary>
    /// Класс для непосредственной обработки текстовых команд пользователя.
    /// </summary>
    public class CommandHandler
    {
        private readonly ITelegramBotClient _botClient;
        private readonly TaskService _taskService;
        private readonly FocusService _focusService;
        private readonly AntiLazinessService _antiLazinessService;
        private readonly StatsService _statsService;
        private readonly DayPlanService _dayPlanService;
        private readonly ActivityService _activityService;
        private readonly LongTaskService _longTaskService;
        private readonly StreakService _streakService;
        private readonly AdviceService _adviceService;

        // FSM для команды /plan: Хранит состояние заполнения формы для каждого пользователя
        private readonly ConcurrentDictionary<long, PlanFormState> _planStates;
        private readonly ConcurrentDictionary<long, EditPlanState> _editPlanStates;
        private readonly ConcurrentDictionary<long, int> _awaitingLongTaskSlot;

        public CommandHandler(
            ITelegramBotClient botClient, 
            TaskService taskService, 
            FocusService focusService, 
            AntiLazinessService antiLazinessService,
            StatsService statsService,
            DayPlanService dayPlanService,
            ActivityService activityService,
            LongTaskService longTaskService,
            StreakService streakService,
            AdviceService adviceService)
        {
            _botClient = botClient;
            _taskService = taskService;
            _focusService = focusService;
            _antiLazinessService = antiLazinessService;
            _statsService = statsService;
            _dayPlanService = dayPlanService;
            _activityService = activityService;
            _longTaskService = longTaskService;
            _streakService = streakService;
            _adviceService = adviceService;
            _planStates = new ConcurrentDictionary<long, PlanFormState>();
            _editPlanStates = new ConcurrentDictionary<long, EditPlanState>();
            _awaitingLongTaskSlot = new ConcurrentDictionary<long, int>();
        }

        /// <summary>
        /// Основной метод обработки нового сообщения.
        /// </summary>
        public async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
        {
            var chatId = message.Chat.Id;
            var text = message.Text?.Trim();

            // Логируем входящее сообщение в консоль
            Console.WriteLine($"[{DateTime.Now}] Получено сообщение '{text}' от пользователя в чате {chatId}.");

            // Регистрируем любую активность пользователя (для hardmode)
            _antiLazinessService.RecordUserActivity(chatId);

            if (string.IsNullOrEmpty(text))
                return;

            // Если пользователь находится в процессе заполнения плана на день, обрабатываем шаги
            if (_planStates.TryGetValue(chatId, out var planState))
            {
                await HandlePlanStepAsync(chatId, text, planState, cancellationToken);
                return;
            }

            // Если пользователь находится в процессе редактирования залачи плана на день
            if (_editPlanStates.TryGetValue(chatId, out var editState))
            {
                await HandleEditPlanStepAsync(chatId, text, editState, cancellationToken);
                return;
            }

            // FSM для долгосрочных задач (ввод текста)
            if (_awaitingLongTaskSlot.TryGetValue(chatId, out var slotToFill))
            {
                if (text == "Отмена" || text == "⬅ Назад" || text == "⬅️ Назад")
                {
                    _awaitingLongTaskSlot.TryRemove(chatId, out _);
                    await SendTasksMenuAsync(chatId, cancellationToken);
                    return;
                }
                
                // Сохраняем/обновляем задачу в слот
                await _longTaskService.AddOrUpdateLongTaskAsync(chatId, slotToFill, text);
                _awaitingLongTaskSlot.TryRemove(chatId, out _);

                await _botClient.SendMessage(chatId, $"Задача успешно сохранена в слот {slotToFill}.", cancellationToken: cancellationToken);
                await HandleLongTasksMenuAsync(chatId, cancellationToken);
                return;
            }

            // Обработка текстовых кнопок меню долгосрочных задач
            if (text == "📋 Мои долгосрочные задачи") { await HandleLongTasksMenuAsync(chatId, cancellationToken); return; }
            if (text.StartsWith("➕ Добавить задачу "))
            {
                int slot = int.Parse(text.Replace("➕ Добавить задачу ", ""));
                var existing = await _longTaskService.GetLongTaskAsync(chatId, slot);
                if (existing != null && !existing.IsDone)
                {
                    await _botClient.SendMessage(chatId, $"Слот {slot} уже занят: {existing.Text}.\n\nНапиши новый текст для долгосрочной задачи {slot} (старая будет удалена):", cancellationToken: cancellationToken);
                }
                else
                {
                    await _botClient.SendMessage(chatId, $"Напиши текст для долгосрочной задачи {slot}:", cancellationToken: cancellationToken);
                }
                _awaitingLongTaskSlot[chatId] = slot;
                return;
            }
            if (text.StartsWith("✅ Выполнить задачу "))
            {
                int slot = int.Parse(text.Replace("✅ Выполнить задачу ", ""));
                await _longTaskService.SetLongTaskDoneAsync(chatId, slot, true);
                _statsService.AddXP(chatId, 30); // Больше опыта за сложную задачу
                _activityService.LogActivity(chatId, "task", 1);
                await _botClient.SendMessage(chatId, $"Отлично! Долгосрочная задача {slot} выполнена. Начислено 30 XP 🔥", cancellationToken: cancellationToken);
                await HandleLongTasksMenuAsync(chatId, cancellationToken);
                return;
            }
            if (text.StartsWith("🗑 Удалить задачу "))
            {
                int slot = int.Parse(text.Replace("🗑 Удалить задачу ", ""));
                await _longTaskService.DeleteLongTaskAsync(chatId, slot);
                await _botClient.SendMessage(chatId, $"Задача из слота {slot} удалена.", cancellationToken: cancellationToken);
                await HandleLongTasksMenuAsync(chatId, cancellationToken);
                return;
            }

            // Обработка текстовых кнопок (меню)
            if (text == "📋 Задачи") { await SendTasksMenuAsync(chatId, cancellationToken); return; }
            if (text == "🎯 Фокус") { await SendFocusMenuAsync(chatId, cancellationToken); return; }
            if (text == "📅 План дня") { await SendPlanMenuAsync(chatId, cancellationToken); return; }
            if (text == "📊 Статистика") { await SendStatsMenuAsync(chatId, cancellationToken); return; }
            if (text == "⚙️ Настройки") { await SendSettingsMenuAsync(chatId, cancellationToken); return; }
            if (text == "⬅ Назад" || text == "⬅️ Назад") { await SendMainMenuAsync(chatId, "Главное меню", cancellationToken); return; }

            // Подсказки для команд с аргументами
            if (text == "➕ Добавить задачу") { await _botClient.SendMessage(chatId, "Для добавления отправьте:\n/addtask [текст задачи]", cancellationToken: cancellationToken); return; }
            if (text == "✅ Выполнить задачу") { await _botClient.SendMessage(chatId, "Для выполнения отправьте:\n/done [номер задачи]", cancellationToken: cancellationToken); return; }
            if (text == "🗑 Удалить задачу") { await _botClient.SendMessage(chatId, "Для удаления отправьте:\n/delete [номер задачи] (в разработке)", cancellationToken: cancellationToken); return; }
            if (text == "🔔 Анти-лень" || text == "💀 Hard mode" || text == "🔕 Выключить напоминания") { await _botClient.SendMessage(chatId, $"Настройка. Для включения используйте команды /antilen on/off или /hardmode on/off", cancellationToken: cancellationToken); return; }
            
            // Кнопки управления планом
            if (text == "✔ Выполнить главную" || text == "✔ Выполнить среднюю" || text == "✔ Выполнить лёгкую")
            {
                int taskType = text.Contains("главную") ? 1 : text.Contains("среднюю") ? 2 : 3;
                await _dayPlanService.MarkDayPlanTaskDoneAsync(chatId, taskType);
                await HandleTodayCommandAsync(chatId, cancellationToken);
                return;
            }
            if (text == "🗑 Удалить план")
            {
                _dayPlanService.DeleteDayPlan(chatId);
                await _botClient.SendMessage(chatId, "План на сегодня удалён.", cancellationToken: cancellationToken);
                await SendMainMenuAsync(chatId, "Главное меню", cancellationToken);
                return;
            }
            if (text == "✏ Изменить задачу" || text == "✏ Изменить")
            {
                _editPlanStates[chatId] = new EditPlanState { Step = 1 };
                await _botClient.SendMessage(chatId, "Какую задачу изменить?\n1 Главную\n2 Среднюю\n3 Лёгкую", cancellationToken: cancellationToken);
                return;
            }

            // Маппинг остальных текстовых кнопок в команды
            if (text == "📋 Список задач") text = "/tasks";
            else if (text == "⏱ Начать фокус (25)") text = "/focus 25";
            else if (text == "⏱ Начать фокус (50)") text = "/focus 50";
            else if (text == "⏹ Остановить фокус") text = "/stopfocus";
            else if (text == "📊 Статус фокуса") text = "/status";
            else if (text == "📝 Создать план") text = "/plan";
            else if (text == "📅 План на сегодня") text = "/today";
            else if (text == "📊 Отчёт дня") text = "/report";
            else if (text == "👤 Профиль" || text == "📊 Профиль") text = "/profile";
            else if (text == "📈 Неделя") text = "/week";
            else if (text == "🔥 Стрик") text = "/streak";
            else if (text == "📅 Месяц") text = "/month";
            else if (text == "💡 Совет") text = "/advice";

            // Выбор команды
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var command = words.Length > 0 ? words[0].ToLower() : "";

            switch (command)
            {
                case "/start":
                    await HandleStartCommandAsync(chatId, cancellationToken);
                    break;
                case "/help":
                    await HandleHelpCommandAsync(chatId, cancellationToken);
                    break;
                case "/addtask":
                    await HandleAddTaskCommandAsync(chatId, text, cancellationToken);
                    break;
                case "/tasks":
                    await HandleTasksCommandAsync(chatId, cancellationToken);
                    break;
                case "/done":
                    await HandleDoneCommandAsync(chatId, text, cancellationToken);
                    break;
                case "/focus":
                    // Если отправлено просто /focus (например с кнопки), ставим 25 минут
                    if (words.Length == 1) text = "/focus 25";
                    await HandleFocusCommandAsync(chatId, text, cancellationToken);
                    break;
                case "/stopfocus":
                    await _focusService.StopFocusAsync(chatId);
                    break;
                case "/status":
                    await _focusService.GetStatusAsync(chatId);
                    break;
                case "/antilen":
                    await HandleAntiLenCommandAsync(chatId, text, cancellationToken);
                    break;
                case "/hardmode":
                    await HandleHardModeCommandAsync(chatId, text, cancellationToken);
                    break;
                case "/level":
                    await HandleLevelCommandAsync(chatId, cancellationToken);
                    break;
                case "/profile":
                    await HandleProfileCommandAsync(chatId, cancellationToken);
                    break;
                case "/plan":
                    await HandlePlanCommandAsync(chatId, cancellationToken);
                    break;
                case "/today":
                    await HandleTodayCommandAsync(chatId, cancellationToken);
                    break;
                case "/report":
                    await _dayPlanService.GenerateDailyReportAsync(chatId);
                    break;
                case "/week":
                    await HandleWeekCommandAsync(chatId, cancellationToken);
                    break;
                case "/streak":
                    var streak = _streakService.GetStreak(chatId);
                    string streakResponse = $"🔥 Streak: {streak.CurrentStreak} дней\nЛучший результат: {streak.BestStreak} дней";
                    await _botClient.SendMessage(chatId, streakResponse, cancellationToken: cancellationToken);
                    break;
                case "/month":
                    string monthReport = _activityService.GetMonthlyStatsReport(chatId);
                    await _botClient.SendMessage(chatId, monthReport, cancellationToken: cancellationToken);
                    break;
                case "/advice":
                    string advice = _adviceService.GetAdvice(chatId);
                    await _botClient.SendMessage(chatId, advice, cancellationToken: cancellationToken);
                    break;
                default:
                    // Если получена неизвестная команда
                    if (text.StartsWith("/"))
                    {
                        await _botClient.SendMessage(
                            chatId: chatId,
                            text: "Извините, я не понимаю эту команду. Используйте /help для списка команд.",
                            cancellationToken: cancellationToken);
                    }
                    break;
            }
        }

        /// <summary>
        /// Действие при получении команды /start
        /// </summary>
        private async Task HandleStartCommandAsync(long chatId, CancellationToken cancellationToken)
        {
            string startMessage = "Привет. Я бот продуктивности.\nЯ помогу тебе не лениться.";
            await SendMainMenuAsync(chatId, startMessage, cancellationToken);
        }

        /// <summary>
        /// Отправляет главное кнопочное меню
        /// </summary>
        private async Task SendMainMenuAsync(long chatId, string text, CancellationToken cancellationToken)
        {
            var replyKeyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "📋 Задачи", "🎯 Фокус" },
                new KeyboardButton[] { "📅 План дня", "📊 Статистика" },
                new KeyboardButton[] { "👤 Профиль", "⚙️ Настройки" }
            })
            {
                ResizeKeyboard = true
            };

            await _botClient.SendMessage(
                chatId: chatId,
                text: text,
                replyMarkup: replyKeyboardMarkup,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Отправляет меню Задач
        /// </summary>
        private async Task SendTasksMenuAsync(long chatId, CancellationToken cancellationToken)
        {
            var replyKeyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "➕ Добавить задачу", "📋 Список задач" },
                new KeyboardButton[] { "✅ Выполнить задачу", "🗑 Удалить задачу" },
                new KeyboardButton[] { "📋 Мои долгосрочные задачи" },
                new KeyboardButton[] { "⬅ Назад" }
            })
            {
                ResizeKeyboard = true
            };

            await _botClient.SendMessage(chatId, "Меню задач:", replyMarkup: replyKeyboardMarkup, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Отправляет клавиатуру со слотами и статусом Мои Долгосрочные Задачи
        /// </summary>
        private async Task HandleLongTasksMenuAsync(long chatId, CancellationToken cancellationToken)
        {
            var tasks = await _longTaskService.GetLongTasksAsync(chatId);
            
            string response = "📋 Ваши долгосрочные задачи (4 слота):\n\n";
            var buttons = new List<KeyboardButton[]>();

            for (int slot = 1; slot <= 4; slot++)
            {
                var task = tasks.Find(t => t.Slot == slot);
                if (task == null)
                {
                    response += $"{slot}. [Пусто]\n";
                    buttons.Add(new KeyboardButton[] { $"➕ Добавить задачу {slot}" });
                }
                else
                {
                    string status = task.IsDone ? "✅" : "⏳";
                    response += $"{slot}. {status} {task.Text}\n";
                    
                    if (!task.IsDone)
                    {
                        buttons.Add(new KeyboardButton[] { $"✅ Выполнить задачу {slot}", $"🗑 Удалить задачу {slot}" });
                    }
                    else
                    {
                        buttons.Add(new KeyboardButton[] { $"➕ Добавить задачу {slot}", $"🗑 Удалить задачу {slot}" });
                    }
                }
            }

            buttons.Add(new KeyboardButton[] { "⬅ Назад" });

            var replyKeyboardMarkup = new ReplyKeyboardMarkup(buttons)
            {
                ResizeKeyboard = true
            };

            await _botClient.SendMessage(chatId, response, replyMarkup: replyKeyboardMarkup, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Отправляет меню Фокуса
        /// </summary>
        private async Task SendFocusMenuAsync(long chatId, CancellationToken cancellationToken)
        {
            var replyKeyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "⏱ Начать фокус (25)", "⏱ Начать фокус (50)" },
                new KeyboardButton[] { "⏹ Остановить фокус", "📊 Статус фокуса" },
                new KeyboardButton[] { "⬅ Назад" }
            })
            {
                ResizeKeyboard = true
            };

            await _botClient.SendMessage(chatId, "Меню фокуса:", replyMarkup: replyKeyboardMarkup, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Отправляет меню Плана дня
        /// </summary>
        private async Task SendPlanMenuAsync(long chatId, CancellationToken cancellationToken)
        {
            var replyKeyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "📝 Создать план", "📅 План на сегодня" },
                new KeyboardButton[] { "📊 Отчёт дня", "⬅ Назад" }
            })
            {
                ResizeKeyboard = true
            };

            await _botClient.SendMessage(chatId, "Меню плана дня:", replyMarkup: replyKeyboardMarkup, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Отправляет меню Статистики
        /// </summary>
        private async Task SendStatsMenuAsync(long chatId, CancellationToken cancellationToken)
        {
            var replyKeyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "📊 Профиль", "📈 Неделя" },
                new KeyboardButton[] { "📅 Месяц", "🔥 Стрик" },
                new KeyboardButton[] { "💡 Совет", "⬅ Назад" }
            })
            {
                ResizeKeyboard = true
            };

            await _botClient.SendMessage(chatId, "Статистика:", replyMarkup: replyKeyboardMarkup, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Отправляет меню Настроек
        /// </summary>
        private async Task SendSettingsMenuAsync(long chatId, CancellationToken cancellationToken)
        {
            var replyKeyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "🔔 Анти-лень", "💀 Hard mode" },
                new KeyboardButton[] { "🔕 Выключить напоминания", "⬅ Назад" }
            })
            {
                ResizeKeyboard = true
            };

            await _botClient.SendMessage(chatId, "Настройки:", replyMarkup: replyKeyboardMarkup, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Действие при получении команды /help
        /// </summary>
        private async Task HandleHelpCommandAsync(long chatId, CancellationToken cancellationToken)
        {
            string helpMessage = "Я бот продуктивности. Доступные команды:\n\n" +
                                 "📅 Планирование\n" +
                                 "/plan - составить план на сегодня\n" +
                                 "/today - показать план на сегодня\n" +
                                 "/report - отчёт продуктивности за день\n\n" +
                                 "🎯 Фокус\n" +
                                 "/focus [минуты] - запустить таймер Pomodoro\n" +
                                 "/stopfocus - досрочно остановить таймер\n" +
                                 "/status - статус текущей сессии фокуса\n\n" +
                                 "📋 Задачи\n" +
                                 "/addtask [текст] - добавить задачу\n" +
                                 "/tasks - список задач\n" +
                                 "/done [номер] - отметить задачу выполненной\n\n" +
                                 "📌 Долгосрочные задачи:\n" +
                                 "/longtasks - показать долгосрочные задачи\n" +
                                 "/addlongtask - добавить долгосрочную задачу\n" +
                                 "/donelongtask - отметить долгосрочную задачу выполненной\n" +
                                 "/deletelongtask - удалить долгосрочную задачу\n\n" +
                                 "📊 Статистика\n" +
                                 "/level - узнать свой текущий уровень\n" +
                                 "/profile - показать статистику профиля\n" +
                                 "/week - статистика за последние 7 дней\n" +
                                 "/month - статистика за последние 30 дней\n" +
                                 "/streak - серия дней (сколько дней подряд выполнен план)\n\n" +
                                 "💡 Помощь\n" +
                                 "/help - справка по командам\n" +
                                 "/advice - умный совет по продуктивности";

            await _botClient.SendMessage(
                chatId: chatId,
                text: helpMessage,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Действие при получении команды /addtask
        /// </summary>
        private async Task HandleAddTaskCommandAsync(long chatId, string fullText, CancellationToken cancellationToken)
        {
            // Убираем саму команду /addtask из текста
            string taskText = fullText.Substring("/addtask".Length).Trim();
            
            if (string.IsNullOrEmpty(taskText))
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "Пожалуйста, укажите текст задачи. Пример: /addtask Сделать лабораторную",
                    cancellationToken: cancellationToken);
                return;
            }

            _taskService.AddTask(chatId, taskText);
            
            await _botClient.SendMessage(
                chatId: chatId,
                text: "Задача добавлена.",
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Действие при получении команды /tasks
        /// </summary>
        private async Task HandleTasksCommandAsync(long chatId, CancellationToken cancellationToken)
        {
            var tasks = _taskService.GetTasks(chatId);
            
            if (tasks.Count == 0)
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "У вас пока нет задач.",
                    cancellationToken: cancellationToken);
                return;
            }

            string responseList = "Ваши задачи:\n\n";
            for (int i = 0; i < tasks.Count; i++)
            {
                var task = tasks[i];
                string statusIcon = task.IsDone ? "✔" : "❌";
                responseList += $"{task.Id}. {task.Text} {statusIcon}\n";
            }

            await _botClient.SendMessage(
                chatId: chatId,
                text: responseList,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Действие при получении команды /done
        /// </summary>
        private async Task HandleDoneCommandAsync(long chatId, string fullText, CancellationToken cancellationToken)
        {
            string numberStr = fullText.Substring("/done".Length).Trim();
            
            if (int.TryParse(numberStr, out int taskId))
            {
                bool success = _taskService.MarkTaskDone(chatId, taskId);
                if (success)
                {
                    // Начисляем опыт за выполненную задачу
                    _statsService.UpdateTaskCompleted(chatId);
                    
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "Задача выполнена. Получено +10 XP!",
                        cancellationToken: cancellationToken);
                }
                else
                {
                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: "Задача с таким номером не найдена или уже выполнена.",
                        cancellationToken: cancellationToken);
                }
            }
            else
            {
                await _botClient.SendMessage(
                    chatId: chatId,
                    text: "Пожалуйста, укажите корректный номер задачи. Пример: /done 1",
                    cancellationToken: cancellationToken);
            }
        }
        /// <summary>
        /// Обработка возврата команды /focus
        /// </summary>
        private async Task HandleFocusCommandAsync(long chatId, string fullText, CancellationToken cancellationToken)
        {
            var parts = fullText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            int minutes = 25; // По умолчанию
            if (parts.Length > 1 && int.TryParse(parts[1], out int parsedMinutes))
            {
                minutes = parsedMinutes;
            }

            await _focusService.StartFocusAsync(chatId, minutes);
        }

        /// <summary>
        /// Обработка включения/выключения анти-лени
        /// </summary>
        private async Task HandleAntiLenCommandAsync(long chatId, string fullText, CancellationToken cancellationToken)
        {
            var parts = fullText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1 && parts[1].ToLower() == "on")
            {
                await _antiLazinessService.SetAntiLenAsync(chatId, true);
            }
            else if (parts.Length > 1 && parts[1].ToLower() == "off")
            {
                await _antiLazinessService.SetAntiLenAsync(chatId, false);
            }
            else
            {
                await _botClient.SendMessage(chatId, "Использование: /antilen on или /antilen off", cancellationToken: cancellationToken);
            }
        }

        /// <summary>
        /// Обработка включения/выключения hardmode
        /// </summary>
        private async Task HandleHardModeCommandAsync(long chatId, string fullText, CancellationToken cancellationToken)
        {
            var parts = fullText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1 && parts[1].ToLower() == "on")
            {
                await _antiLazinessService.SetHardModeAsync(chatId, true);
            }
            else if (parts.Length > 1 && parts[1].ToLower() == "off")
            {
                await _antiLazinessService.SetHardModeAsync(chatId, false);
            }
            else
            {
                await _botClient.SendMessage(chatId, "Использование: /hardmode on или /hardmode off", cancellationToken: cancellationToken);
            }
        }

        /// <summary>
        /// Обработка команды /level
        /// </summary>
        private async Task HandleLevelCommandAsync(long chatId, CancellationToken cancellationToken)
        {
            var profile = _statsService.GetProfile(chatId);
            int nextLevelXp = profile.Level * 100;
            
            string response = $"Твой уровень: {profile.Level}\nXP: {profile.XP} / {nextLevelXp}";
            
            await _botClient.SendMessage(chatId, response, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Обработка команды /profile
        /// </summary>
        private async Task HandleProfileCommandAsync(long chatId, CancellationToken cancellationToken)
        {
            var profile = _statsService.GetProfile(chatId);
            
            string response = $"Статистика:\n\n" +
                              $"Уровень: {profile.Level}\n" +
                              $"XP: {profile.XP}\n" +
                              $"Выполнено задач: {profile.TasksCompleted}\n" +
                              $"Фокус-сессий: {profile.FocusSessions}";
            
            await _botClient.SendMessage(chatId, response, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Начинает процедуру создания плана на день (/plan)
        /// </summary>
        private async Task HandlePlanCommandAsync(long chatId, CancellationToken cancellationToken)
        {
            var existingPlan = _dayPlanService.GetTodayPlan(chatId);
            if (existingPlan != null)
            {
                await _botClient.SendMessage(chatId, "У тебя уже есть план на сегодня. Используй меню «План на сегодня» для изменения или удаления.", cancellationToken: cancellationToken);
                await HandleTodayCommandAsync(chatId, cancellationToken);
                return;
            }

            var newState = new PlanFormState { Step = 1 };
            _planStates[chatId] = newState;

            await _botClient.SendMessage(chatId, "Главная задача дня (самая важная)", cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Показывает план на сегодня (/today)
        /// </summary>
        private async Task HandleTodayCommandAsync(long chatId, CancellationToken cancellationToken)
        {
            var plan = _dayPlanService.GetTodayPlan(chatId);
            if (plan == null)
            {
                await _botClient.SendMessage(chatId, "На сегодня план ещё не составлен. Составьте его через меню.", cancellationToken: cancellationToken);
                return;
            }

            string mainStatus = plan.MainDone ? "✔" : "❌";
            string mediumStatus = plan.MediumDone ? "✔" : "❌";
            string easyStatus = plan.EasyDone ? "✔" : "❌";

            string response = $"Текущий план дня:\n\n" +
                              $"🔥 Главная задача ({mainStatus}):\n{plan.MainTask}\n\n" +
                              $"⚙️ Средняя задача ({mediumStatus}):\n{plan.MediumTask}\n\n" +
                              $"🟢 Лёгкая задача ({easyStatus}):\n{plan.EasyTask}";

            var replyKeyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "✔ Выполнить главную" },
                new KeyboardButton[] { "✔ Выполнить среднюю", "✔ Выполнить лёгкую" },
                new KeyboardButton[] { "✏ Изменить задачу", "🗑 Удалить план" },
                new KeyboardButton[] { "⬅ Назад" }
            })
            {
                ResizeKeyboard = true
            };

            await _botClient.SendMessage(chatId, response, replyMarkup: replyKeyboardMarkup, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Обрабатывает шаги формы /plan
        /// </summary>
        private async Task HandlePlanStepAsync(long chatId, string text, PlanFormState state, CancellationToken cancellationToken)
        {
            switch (state.Step)
            {
                case 1:
                    state.DraftPlan.MainTask = text;
                    state.Step = 2;
                    await _botClient.SendMessage(chatId, "Средняя задача дня", cancellationToken: cancellationToken);
                    break;

                case 2:
                    state.DraftPlan.MediumTask = text;
                    state.Step = 3;
                    await _botClient.SendMessage(chatId, "Лёгкая задача дня", cancellationToken: cancellationToken);
                    break;

                case 3:
                    state.DraftPlan.EasyTask = text;
                    state.DraftPlan.UserId = chatId;
                    
                    // Сохраняем в БД
                    _dayPlanService.CreatePlan(state.DraftPlan);
                    
                    // Завершаем форму
                    _planStates.TryRemove(chatId, out _);

                    await _botClient.SendMessage(chatId, "План на сегодня сохранён.", cancellationToken: cancellationToken);
                    break;
            }
        }

        /// <summary>
        /// Обработка команды /week для вывода статистики за 7 дней.
        /// </summary>
        private async Task HandleWeekCommandAsync(long chatId, CancellationToken cancellationToken)
        {
            string report = _activityService.GetWeeklyStatsReport(chatId);
            await _botClient.SendMessage(
                chatId: chatId,
                text: report,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Обрабатывает FSM редактирования плана
        /// </summary>
        private async Task HandleEditPlanStepAsync(long chatId, string text, EditPlanState state, CancellationToken cancellationToken)
        {
            if (state.Step == 1)
            {
                if (text == "1" || text == "2" || text == "3")
                {
                    state.TaskTypeToEdit = int.Parse(text);
                    state.Step = 2;
                    string name = state.TaskTypeToEdit == 1 ? "Главную" : state.TaskTypeToEdit == 2 ? "Среднюю" : "Лёгкую";
                    await _botClient.SendMessage(chatId, $"Введи новый текст для {name} задачи:", cancellationToken: cancellationToken);
                }
                else
                {
                    await _botClient.SendMessage(chatId, "Пожалуйста, выбери номер задачи (1, 2 или 3). Если хочешь выйти из редактирования, напиши 0.", cancellationToken: cancellationToken);
                    if (text == "0" || text == "Отмена")
                    {
                        _editPlanStates.TryRemove(chatId, out _);
                        await HandleTodayCommandAsync(chatId, cancellationToken);
                    }
                }
                return;
            }

            if (state.Step == 2)
            {
                _dayPlanService.UpdateDayPlanTask(chatId, state.TaskTypeToEdit, text);
                _editPlanStates.TryRemove(chatId, out _);
                await HandleTodayCommandAsync(chatId, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Вспомогательный класс для хранения состояния формы планирования
    /// </summary>
    class PlanFormState
    {
        public int Step { get; set; } = 1;
        public DayPlan DraftPlan { get; set; } = new DayPlan();
    }

    class EditPlanState
    {
        public int Step { get; set; } = 1;
        public int TaskTypeToEdit { get; set; }
    }
}
