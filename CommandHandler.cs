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
        private readonly StatisticsService _statisticsService;

        // FSM для команды /plan: Хранит состояние заполнения формы для каждого пользователя
        private readonly ConcurrentDictionary<long, PlanFormState> _planStates;
        private readonly ConcurrentDictionary<long, EditPlanState> _editPlanStates;
        private readonly ConcurrentDictionary<long, int> _awaitingLongTaskSlot;
        private readonly ConcurrentDictionary<long, EditDeadlineState> _editDeadlineStates;

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
            AdviceService adviceService,
            StatisticsService statisticsService)
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
            _statisticsService = statisticsService;
            _planStates = new ConcurrentDictionary<long, PlanFormState>();
            _editPlanStates = new ConcurrentDictionary<long, EditPlanState>();
            _awaitingLongTaskSlot = new ConcurrentDictionary<long, int>();
            _editDeadlineStates = new ConcurrentDictionary<long, EditDeadlineState>();
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

            // Проверяем FSM редактирования плана
            if (_editPlanStates.TryGetValue(chatId, out var editState))
            {
                await HandleEditPlanStepAsync(chatId, text, editState, cancellationToken);
                return;
            }

            // Проверяем FSM редактирования дедлайна
            if (_editDeadlineStates.TryGetValue(chatId, out var deadlineState))
            {
                await HandleEditDeadlineStepAsync(chatId, text, deadlineState, cancellationToken);
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
            var lang = _taskService.GetUserLanguage(chatId) ?? "ru";
            if (text == "🎯 Фокус") { await SendFocusMenuAsync(chatId, cancellationToken); return; }
            if (text != null && text.Contains(LocalizationService.T("btn_back", lang))) { await SendMainMenuAsync(chatId, LocalizationService.T("main_menu", lang), cancellationToken); return; }

            if (text == "🇷🇺 Русский" || text == "🇬🇧 English") {
                string selectedLang = text == "🇷🇺 Русский" ? "ru" : "en";
                string msg = text == "🇷🇺 Русский" ? "Язык изменён на Русский" : "Language changed to English";
                
                var oldLang = _taskService.GetUserLanguage(chatId);
                _taskService.SetUserLanguage(chatId, selectedLang);
                lang = selectedLang; // Update local lang cache

                if (string.IsNullOrEmpty(oldLang)) {
                    await _botClient.SendMessage(chatId, lang == "ru" ? LocalizationService.T("lang_set_ru", "ru") : LocalizationService.T("lang_set_en", "en"), cancellationToken: cancellationToken);
                } else {
                    await _botClient.SendMessage(chatId, msg, cancellationToken: cancellationToken);
                }
                
                lang = _taskService.GetUserLanguage(chatId) ?? "ru";
                await SendMainMenuAsync(chatId, LocalizationService.T("main_menu", lang), cancellationToken);
                return;
            }

            // Подсказки для команд с аргументами
            if (text == "➕ Добавить задачу") { await _botClient.SendMessage(chatId, "Для добавления отправьте:\n/addtask [текст задачи]", cancellationToken: cancellationToken); return; }
            if (text == "✅ Выполнить задачу") { await _botClient.SendMessage(chatId, "Для выполнения отправьте:\n/done [номер задачи]", cancellationToken: cancellationToken); return; }
            if (text == "🗑 Удалить задачу") { await _botClient.SendMessage(chatId, "Для удаления отправьте:\n/delete [номер задачи] (в разработке)", cancellationToken: cancellationToken); return; }
            if (text == "🔔 Анти-лень" || text == "💀 Hard mode" || text == "🔕 Выключить напоминания") { await _botClient.SendMessage(chatId, $"Настройка. Для включения используйте команды /antilen on/off или /hardmode on/off", cancellationToken: cancellationToken); return; }
            
            if (text == LocalizationService.T("settings_antilen_on", lang) || text == "Анти-лень ВКЛ") { await _antiLazinessService.SetAntiLenAsync(chatId, true); await SendSettingsMenuAsync(chatId, cancellationToken); return; }
            if (text == LocalizationService.T("settings_antilen_off", lang) || text == "Анти-лень ВЫКЛ") { await _antiLazinessService.SetAntiLenAsync(chatId, false); await SendSettingsMenuAsync(chatId, cancellationToken); return; }
            if (text == LocalizationService.T("settings_hardmode_on", lang) || text == "Hard mode ВКЛ") { await _antiLazinessService.SetHardModeAsync(chatId, true); await SendSettingsMenuAsync(chatId, cancellationToken); return; }
            if (text == LocalizationService.T("settings_hardmode_off", lang) || text == "Hard mode ВЫКЛ") { await _antiLazinessService.SetHardModeAsync(chatId, false); await SendSettingsMenuAsync(chatId, cancellationToken); return; }
            if (text == LocalizationService.T("settings_language", lang) || text == "🌍 Language")
            {
                var langKeyboard = new Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardMarkup(new[]
                {
                    new Telegram.Bot.Types.ReplyMarkups.KeyboardButton[] { "🇷🇺 Русский", "🇬🇧 English" }
                }) { ResizeKeyboard = true };
                
                await _botClient.SendMessage(chatId, LocalizationService.T("lang_select", lang), replyMarkup: langKeyboard, cancellationToken: cancellationToken);
                return;
            }
            
            // Кнопки управления планом
            if (text != null && text.Contains(LocalizationService.T("btn_main_done", lang))) { await _dayPlanService.ProcessDayPlanTaskAsync(chatId, 1, true); await SendMainMenuAsync(chatId, LocalizationService.T("main_menu", lang), cancellationToken); return; }
            if (text != null && text.Contains(LocalizationService.T("btn_main_fail", lang))) { await _dayPlanService.ProcessDayPlanTaskAsync(chatId, 1, false); await SendMainMenuAsync(chatId, LocalizationService.T("main_menu", lang), cancellationToken); return; }
            if (text != null && text.Contains(LocalizationService.T("btn_med_done", lang))) { await _dayPlanService.ProcessDayPlanTaskAsync(chatId, 2, true); await SendMainMenuAsync(chatId, LocalizationService.T("main_menu", lang), cancellationToken); return; }
            if (text != null && text.Contains(LocalizationService.T("btn_med_fail", lang))) { await _dayPlanService.ProcessDayPlanTaskAsync(chatId, 2, false); await SendMainMenuAsync(chatId, LocalizationService.T("main_menu", lang), cancellationToken); return; }
            if (text != null && text.Contains(LocalizationService.T("btn_easy_done", lang))) { await _dayPlanService.ProcessDayPlanTaskAsync(chatId, 3, true); await SendMainMenuAsync(chatId, LocalizationService.T("main_menu", lang), cancellationToken); return; }
            if (text != null && text.Contains(LocalizationService.T("btn_easy_fail", lang))) { await _dayPlanService.ProcessDayPlanTaskAsync(chatId, 3, false); await SendMainMenuAsync(chatId, LocalizationService.T("main_menu", lang), cancellationToken); return; }
            if (text != null && text.Contains(LocalizationService.T("btn_main_deadline", lang))) { _editDeadlineStates[chatId] = new EditDeadlineState { TaskType = 1 }; await _botClient.SendMessage(chatId, LocalizationService.T("plan_prompt_main_dl", lang), replyMarkup: GetDeadlineKeyboard(lang), cancellationToken: cancellationToken); return; }
            if (text != null && text.Contains(LocalizationService.T("btn_med_deadline", lang))) { _editDeadlineStates[chatId] = new EditDeadlineState { TaskType = 2 }; await _botClient.SendMessage(chatId, LocalizationService.T("plan_prompt_main_dl", lang), replyMarkup: GetDeadlineKeyboard(lang), cancellationToken: cancellationToken); return; }
            if (text != null && text.Contains(LocalizationService.T("btn_easy_deadline", lang))) { _editDeadlineStates[chatId] = new EditDeadlineState { TaskType = 3 }; await _botClient.SendMessage(chatId, LocalizationService.T("plan_prompt_main_dl", lang), replyMarkup: GetDeadlineKeyboard(lang), cancellationToken: cancellationToken); return; }
            if (text == "📊 График" || (text != null && text.Trim() == "📊 График") || (text != null && text.Contains("График")))
            {
                try 
                {
                    var data = _statisticsService.GetLast7DaysStats(chatId);
                    using var stream = _statisticsService.GenerateGraphImage(data, lang);
                    var photo = Telegram.Bot.Types.InputFile.FromStream(stream, "graph.png");
                    await _botClient.SendPhoto(chatId, photo, caption: LocalizationService.T("stats_graph_caption", lang), cancellationToken: cancellationToken);
                }
                catch (Exception ex)
                {
                    await _botClient.SendMessage(chatId, $"Ошибка генерации графика: {ex.Message}", cancellationToken: cancellationToken);
                }
                return;
            }
            if (text != null && text.Contains(LocalizationService.T("btn_delete_plan", lang)))
            {
                _dayPlanService.DeleteDayPlan(chatId);
                await _botClient.SendMessage(chatId, LocalizationService.T("plan_deleted", lang), cancellationToken: cancellationToken);
                await SendMainMenuAsync(chatId, LocalizationService.T("main_menu", lang), cancellationToken);
                return;
            }
            if (text != null && text.Contains(LocalizationService.T("btn_edit_task", lang)))
            {
                _editPlanStates[chatId] = new EditPlanState { Step = 1 };
                await _botClient.SendMessage(chatId, LocalizationService.T("edit_task_prompt", lang), cancellationToken: cancellationToken);
                return;
            }

            // Маппинг остальных текстовых кнопок в команды

            if (text == "📋 Список задач") text = "/tasks";
            else if (text == "⏱ Начать фокус (25)") text = "/focus 25";
            else if (text == "⏱ Начать фокус (50)") text = "/focus 50";
            else if (text == "⏹ Остановить фокус") text = "/stopfocus";
            else if (text == "📊 Статус фокуса") text = "/status";
            else if (text == LocalizationService.T("menu_create", lang) || text.Contains(LocalizationService.T("menu_create", lang))) text = "/plan";
            else if (text == LocalizationService.T("menu_plan", lang) || text.Contains(LocalizationService.T("menu_plan", lang))) text = "/today";
            else if (text == LocalizationService.T("menu_stats", lang) || text.Contains(LocalizationService.T("menu_stats", lang))) text = "/stats_menu_trigger";
            else if (text == LocalizationService.T("menu_settings", lang) || text.Contains(LocalizationService.T("menu_settings", lang))) text = "/settings_menu_trigger";
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
                    var langStrk = _taskService.GetUserLanguage(chatId) ?? "ru";
                    var streak = _streakService.GetStreak(chatId);
                    string streakResponse = LocalizationService.T("streak_info", langStrk)
                                              .Replace("{streak}", streak.CurrentStreak.ToString())
                                              .Replace("{best}", streak.BestStreak.ToString());
                    await _botClient.SendMessage(chatId, streakResponse, cancellationToken: cancellationToken);
                    break;
                case "/month":
                    var langMonth = _taskService.GetUserLanguage(chatId) ?? "ru";
                    string monthReport = _activityService.GetMonthlyStatsReport(chatId, langMonth);
                    await _botClient.SendMessage(chatId, monthReport, cancellationToken: cancellationToken);
                    break;
                case "/advice":
                    var langAdvice = _taskService.GetUserLanguage(chatId) ?? "ru";
                    string advice = _adviceService.GetAdvice(chatId, langAdvice);
                    await _botClient.SendMessage(chatId, advice, cancellationToken: cancellationToken);
                    break;
                case "/settings_menu_trigger":
                    await SendSettingsMenuAsync(chatId, cancellationToken);
                    break;
                case "/stats_menu_trigger":
                    await SendStatsMenuAsync(chatId, cancellationToken);
                    break;
                default:
                    // Если получена неизвестная команда
                    if (text.StartsWith("/"))
                    {
                        var langUnk = _taskService.GetUserLanguage(chatId) ?? "ru";
                        await _botClient.SendMessage(
                            chatId: chatId,
                            text: LocalizationService.T("unknown_command", langUnk),
                            cancellationToken: cancellationToken);
                    }
                    else
                    {
                        var langUnk = _taskService.GetUserLanguage(chatId) ?? "ru";
                        await SendMainMenuAsync(chatId, LocalizationService.T("main_menu", langUnk), cancellationToken);
                    }
                    break;
            }
        }

        /// <summary>
        /// Действие при получении команды /start
        /// </summary>
        private async Task HandleStartCommandAsync(long chatId, CancellationToken cancellationToken)
        {
            var lang = _taskService.GetUserLanguage(chatId);
            if (string.IsNullOrEmpty(lang))
            {
                var langKeyboard = new Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardMarkup(new[]
                {
                    new Telegram.Bot.Types.ReplyMarkups.KeyboardButton[] { "🇷🇺 Русский", "🇬🇧 English" }
                }) { ResizeKeyboard = true };
                
                await _botClient.SendMessage(chatId, "Выбери язык / Choose language", replyMarkup: langKeyboard, cancellationToken: cancellationToken);
                return;
            }

            string startMessage = LocalizationService.T("start", lang);
            await SendMainMenuAsync(chatId, startMessage, cancellationToken);
        }

        /// <summary>
        /// Отправляет главное кнопочное меню
        /// </summary>
        private async Task SendMainMenuAsync(long chatId, string text, CancellationToken cancellationToken)
        {
            var lang = _taskService.GetUserLanguage(chatId) ?? "ru";

            var btnPlanToday = LocalizationService.T("menu_plan", lang);
            var btnCreatePlan = LocalizationService.T("menu_create", lang);
            var btnStats = LocalizationService.T("menu_stats", lang);
            var btnSettings = LocalizationService.T("menu_settings", lang);

            var replyKeyboardMarkup = new Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardMarkup(new[]
            {
                new Telegram.Bot.Types.ReplyMarkups.KeyboardButton[] { btnPlanToday, btnCreatePlan },
                new Telegram.Bot.Types.ReplyMarkups.KeyboardButton[] { btnStats, btnSettings }
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
            var lang = _taskService.GetUserLanguage(chatId) ?? "ru";
            var replyKeyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { LocalizationService.T("menu_create", lang), LocalizationService.T("menu_plan", lang) },
                new KeyboardButton[] { "📊 Отчёт дня", LocalizationService.T("btn_back", lang) }
            })
            {
                ResizeKeyboard = true
            };

            await _botClient.SendMessage(chatId, LocalizationService.T("today_title", lang), replyMarkup: replyKeyboardMarkup, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Отправляет меню Статистики
        /// </summary>
        private async Task SendStatsMenuAsync(long chatId, CancellationToken cancellationToken)
        {
            var lang = _taskService.GetUserLanguage(chatId) ?? "ru";
            var replyKeyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "📊 Профиль", "📊 График" },
                new KeyboardButton[] { "📈 Неделя", "📅 Месяц" },
                new KeyboardButton[] { "🔥 Стрик", "💡 Совет" },
                new KeyboardButton[] { LocalizationService.T("btn_back", lang) }
            })
            {
                ResizeKeyboard = true
            };

            await _botClient.SendMessage(chatId, LocalizationService.T("menu_stats", lang), replyMarkup: replyKeyboardMarkup, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Отправляет меню Настроек
        /// </summary>
        private async Task SendSettingsMenuAsync(long chatId, CancellationToken cancellationToken)
        {
            var lang = _taskService.GetUserLanguage(chatId) ?? "ru";
            bool antiLenActive = _antiLazinessService.IsAntiLenActive(chatId);
            bool hardModeActive = _antiLazinessService.IsHardModeActive(chatId);

            string btnAntiLen = antiLenActive ? LocalizationService.T("settings_antilen_off", lang) : LocalizationService.T("settings_antilen_on", lang);
            string btnHardMode = hardModeActive ? LocalizationService.T("settings_hardmode_off", lang) : LocalizationService.T("settings_hardmode_on", lang);

            var replyKeyboardMarkup = new Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardMarkup(new[]
            {
                new Telegram.Bot.Types.ReplyMarkups.KeyboardButton[] { btnAntiLen },
                new Telegram.Bot.Types.ReplyMarkups.KeyboardButton[] { btnHardMode },
                new Telegram.Bot.Types.ReplyMarkups.KeyboardButton[] { LocalizationService.T("settings_language", lang) },
                new Telegram.Bot.Types.ReplyMarkups.KeyboardButton[] { LocalizationService.T("btn_back", lang) }
            })
            {
                ResizeKeyboard = true
            };

            string antilenState = antiLenActive ? LocalizationService.T("state_on", lang) : LocalizationService.T("state_off", lang);
            string hardmodeState = hardModeActive ? LocalizationService.T("state_on", lang) : LocalizationService.T("state_off", lang);

            string response = LocalizationService.T("settings_overview", lang)
                               .Replace("{antilen}", antilenState)
                               .Replace("{hardmode}", hardmodeState);

            await _botClient.SendMessage(chatId, response, replyMarkup: replyKeyboardMarkup, cancellationToken: cancellationToken);
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
            var lang = _taskService.GetUserLanguage(chatId) ?? "ru";
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
                await _botClient.SendMessage(chatId, LocalizationService.T("config_usage_antilen", lang), cancellationToken: cancellationToken);
            }
        }

        /// <summary>
        /// Обработка включения/выключения hardmode
        /// </summary>
        private async Task HandleHardModeCommandAsync(long chatId, string fullText, CancellationToken cancellationToken)
        {
            var lang = _taskService.GetUserLanguage(chatId) ?? "ru";
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
                await _botClient.SendMessage(chatId, LocalizationService.T("config_usage_hardmode", lang), cancellationToken: cancellationToken);
            }
        }

        /// <summary>
        /// Обработка команды /level
        /// </summary>
        private async Task HandleLevelCommandAsync(long chatId, CancellationToken cancellationToken)
        {
            var lang = _taskService.GetUserLanguage(chatId) ?? "ru";
            var profile = _statsService.GetProfile(chatId);
            int nextLevelXp = profile.Level * 100;
            
            string response = LocalizationService.T("level_info", lang)
                               .Replace("{level}", profile.Level.ToString())
                               .Replace("{xp}", profile.XP.ToString())
                               .Replace("{nextLevelXp}", nextLevelXp.ToString());
            
            await _botClient.SendMessage(chatId, response, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Обработка команды /profile
        /// </summary>
        private async Task HandleProfileCommandAsync(long chatId, CancellationToken cancellationToken)
        {
            var lang = _taskService.GetUserLanguage(chatId) ?? "ru";
            var profile = _statsService.GetProfile(chatId);
            
            string response = LocalizationService.T("profile_info", lang)
                               .Replace("{level}", profile.Level.ToString())
                               .Replace("{xp}", profile.XP.ToString())
                               .Replace("{tasksCompleted}", profile.TasksCompleted.ToString())
                               .Replace("{focusSessions}", profile.FocusSessions.ToString());
            
            await _botClient.SendMessage(chatId, response, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Начинает процедуру создания плана на день (/plan)
        /// </summary>
        private async Task HandlePlanCommandAsync(long chatId, CancellationToken cancellationToken)
        {
            var lang = _taskService.GetUserLanguage(chatId) ?? "ru";
            var existingPlan = _dayPlanService.GetTodayPlan(chatId);
            if (existingPlan != null)
            {
                await _botClient.SendMessage(chatId, LocalizationService.T("plan_already_exists", lang), cancellationToken: cancellationToken);
                await HandleTodayCommandAsync(chatId, cancellationToken);
                return;
            }

            var newState = new PlanFormState { Step = 1 };
            _planStates[chatId] = newState;

            await _botClient.SendMessage(chatId, LocalizationService.T("plan_prompt_main", lang), cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Показывает план на сегодня (/today)
        /// </summary>
        private async Task HandleTodayCommandAsync(long chatId, CancellationToken cancellationToken)
        {
            var plan = _dayPlanService.GetTodayPlan(chatId);
            var lang = _taskService.GetUserLanguage(chatId) ?? "ru";
            if (plan == null)
            {
                var emptyMarkup = new ReplyKeyboardMarkup(new[] { new KeyboardButton[] { LocalizationService.T("menu_create", lang) }, new KeyboardButton[] { "⬅ Назад" } }) { ResizeKeyboard = true };
                await _botClient.SendMessage(chatId, LocalizationService.T("no_plan", lang), replyMarkup: emptyMarkup, cancellationToken: cancellationToken);
                return;
            }

            string mainStatus = plan.MainDone ? "✔" : plan.MainFailed ? "❌" : "⏳";
            string mediumStatus = plan.MediumDone ? "✔" : plan.MediumFailed ? "❌" : "⏳";
            string easyStatus = plan.EasyDone ? "✔" : plan.EasyFailed ? "❌" : "⏳";

            string mainDeadlineStr = plan.MainDeadline != null ? LocalizationService.T("deadline_until", lang).Replace("{time}", plan.MainDeadline) : LocalizationService.T("deadline_none", lang);
            string medDeadlineStr = plan.MediumDeadline != null ? LocalizationService.T("deadline_until", lang).Replace("{time}", plan.MediumDeadline) : LocalizationService.T("deadline_none", lang);
            string easyDeadlineStr = plan.EasyDeadline != null ? LocalizationService.T("deadline_until", lang).Replace("{time}", plan.EasyDeadline) : LocalizationService.T("deadline_none", lang);

            string response = $"{LocalizationService.T("today_title", lang)}\n\n" +
                              $"🔥 {LocalizationService.T("task_main", lang)}\n{plan.MainTask}\n⏰ {mainDeadlineStr}\n{mainStatus}\n\n" +
                              $"⚙️ {LocalizationService.T("task_medium", lang)}\n{plan.MediumTask}\n⏰ {medDeadlineStr}\n{mediumStatus}\n\n" +
                              $"🟢 {LocalizationService.T("task_easy", lang)}\n{plan.EasyTask}\n⏰ {easyDeadlineStr}\n{easyStatus}";

            var replyKeyboardMarkup = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { LocalizationService.T("btn_main_done", lang), LocalizationService.T("btn_main_fail", lang), LocalizationService.T("btn_main_deadline", lang) },
                new KeyboardButton[] { LocalizationService.T("btn_med_done", lang), LocalizationService.T("btn_med_fail", lang), LocalizationService.T("btn_med_deadline", lang) },
                new KeyboardButton[] { LocalizationService.T("btn_easy_done", lang), LocalizationService.T("btn_easy_fail", lang), LocalizationService.T("btn_easy_deadline", lang) },
                new KeyboardButton[] { LocalizationService.T("btn_edit_task", lang), LocalizationService.T("btn_delete_plan", lang) },
                new KeyboardButton[] { LocalizationService.T("btn_back", lang) }
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
            var lang = _taskService.GetUserLanguage(chatId) ?? "ru";
            switch (state.Step)
            {
                case 1:
                    state.DraftPlan.MainTask = text;
                    state.Step = 2;
                    await _botClient.SendMessage(chatId, LocalizationService.T("plan_prompt_main_dl", lang), replyMarkup: GetDeadlineKeyboard(lang), cancellationToken: cancellationToken);
                    break;

                case 2:
                    if (!TryParseDeadline(text, lang, out string? mainDl, out string err1)) { await _botClient.SendMessage(chatId, err1, cancellationToken: cancellationToken); return; }
                    state.DraftPlan.MainDeadline = mainDl;
                    state.Step = 3;
                    await _botClient.SendMessage(chatId, LocalizationService.T("plan_prompt_medium", lang), replyMarkup: new ReplyKeyboardRemove(), cancellationToken: cancellationToken);
                    break;

                case 3:
                    state.DraftPlan.MediumTask = text;
                    state.Step = 4;
                    await _botClient.SendMessage(chatId, LocalizationService.T("plan_prompt_medium_dl", lang), replyMarkup: GetDeadlineKeyboard(lang), cancellationToken: cancellationToken);
                    break;

                case 4:
                    if (!TryParseDeadline(text, lang, out string? medDl, out string err2)) { await _botClient.SendMessage(chatId, err2, cancellationToken: cancellationToken); return; }
                    state.DraftPlan.MediumDeadline = medDl;
                    state.Step = 5;
                    await _botClient.SendMessage(chatId, LocalizationService.T("plan_prompt_easy", lang), replyMarkup: new ReplyKeyboardRemove(), cancellationToken: cancellationToken);
                    break;

                case 5:
                    state.DraftPlan.EasyTask = text;
                    state.Step = 6;
                    await _botClient.SendMessage(chatId, LocalizationService.T("plan_prompt_easy_dl", lang), replyMarkup: GetDeadlineKeyboard(lang), cancellationToken: cancellationToken);
                    break;

                case 6:
                    if (!TryParseDeadline(text, lang, out string? easyDl, out string err3)) { await _botClient.SendMessage(chatId, err3, cancellationToken: cancellationToken); return; }
                    state.DraftPlan.EasyDeadline = easyDl;
                    state.DraftPlan.UserId = chatId;
                    
                    // Сохраняем в БД
                    _dayPlanService.CreatePlan(state.DraftPlan);
                    
                    // Завершаем форму
                    _planStates.TryRemove(chatId, out _);

                    await _botClient.SendMessage(chatId, LocalizationService.T("plan_saved", lang), replyMarkup: new ReplyKeyboardRemove(), cancellationToken: cancellationToken);
                    await HandleTodayCommandAsync(chatId, cancellationToken);
                    break;
            }
        }

        /// <summary>
        /// Обработка команды /week для вывода статистики за 7 дней.
        /// </summary>
        private async Task HandleWeekCommandAsync(long chatId, CancellationToken cancellationToken)
        {
            var lang = _taskService.GetUserLanguage(chatId) ?? "ru";
            string report = _activityService.GetWeeklyStatsReport(chatId, lang);
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
            var lang = _taskService.GetUserLanguage(chatId) ?? "ru";
            if (state.Step == 1)
            {
                if (text == "1" || text == "2" || text == "3")
                {
                    state.TaskTypeToEdit = int.Parse(text);
                    state.Step = 2;
                    string mainName = LocalizationService.T("task_main", lang);
                    string medName = LocalizationService.T("task_medium", lang);
                    string easyName = LocalizationService.T("task_easy", lang);
                    string name = state.TaskTypeToEdit == 1 ? mainName : state.TaskTypeToEdit == 2 ? medName : easyName;
                    await _botClient.SendMessage(chatId, LocalizationService.T("edit_task_enter_text", lang).Replace("{task}", name), cancellationToken: cancellationToken);
                }
                else
                {
                    await _botClient.SendMessage(chatId, LocalizationService.T("edit_task_invalid", lang), cancellationToken: cancellationToken);
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

        private async Task HandleEditDeadlineStepAsync(long chatId, string text, EditDeadlineState state, CancellationToken cancellationToken)
        {
            var lang = _taskService.GetUserLanguage(chatId) ?? "ru";
            if (text == "Отмена" || text == "0")
            {
                _editDeadlineStates.TryRemove(chatId, out _);
                await HandleTodayCommandAsync(chatId, cancellationToken);
                return;
            }

            if (!TryParseDeadline(text, lang, out string? dl, out string err))
            {
                await _botClient.SendMessage(chatId, err, cancellationToken: cancellationToken);
                return;
            }

            _taskService.SetTaskDeadline(chatId, state.TaskType, dl);
            _editDeadlineStates.TryRemove(chatId, out _);
            
            await _botClient.SendMessage(chatId, LocalizationService.T("deadline_saved", lang).Replace("{time}", dl ?? LocalizationService.T("deadline_none", lang)), replyMarkup: new ReplyKeyboardRemove(), cancellationToken: cancellationToken);
            await HandleTodayCommandAsync(chatId, cancellationToken);
        }

        private ReplyKeyboardMarkup GetDeadlineKeyboard(string lang)
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { LocalizationService.T("deadline_none", lang) }
            }) { ResizeKeyboard = true, OneTimeKeyboard = true };
        }

        private bool TryParseDeadline(string text, string lang, out string? formattedTime, out string errorMessage)
        {
            formattedTime = null;
            errorMessage = "";
            
            if (text == LocalizationService.T("deadline_none", lang)) return true;

            if (TimeSpan.TryParse(text, out TimeSpan ts))
            {
                formattedTime = ts.ToString(@"hh\:mm");
                return true;
            }
            
            errorMessage = LocalizationService.T("deadline_invalid", lang);
            return false;
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

    class EditDeadlineState
    {
        public int TaskType { get; set; }
    }
}
