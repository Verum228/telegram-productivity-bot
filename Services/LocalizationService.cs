using System.Collections.Generic;

namespace TelegramProductivityBot.Services
{
    public static class LocalizationService
    {
        private static readonly Dictionary<string, Dictionary<string, string>> _texts = new Dictionary<string, Dictionary<string, string>>
        {
            { "start", new Dictionary<string, string> {
                { "ru", "Привет. Я бот продуктивности.\nЯ помогу тебе не лениться." },
                { "en", "Hi! I'm a productivity bot.\nI will help you stop being lazy." }
            }},
            { "menu_plan", new Dictionary<string, string> {
                { "ru", "📅 План на сегодня" },
                { "en", "📅 Today's Plan" }
            }},
            { "menu_create", new Dictionary<string, string> {
                { "ru", "📝 Создать план" },
                { "en", "📝 Create Plan" }
            }},
            { "menu_stats", new Dictionary<string, string> {
                { "ru", "📊 Статистика" },
                { "en", "📊 Statistics" }
            }},
            { "menu_settings", new Dictionary<string, string> {
                { "ru", "⚙️ Настройки" },
                { "en", "⚙️ Settings" }
            }},
            { "today_title", new Dictionary<string, string> {
                { "ru", "📅 План на сегодня" },
                { "en", "📅 Today plan" }
            }},
            { "task_main", new Dictionary<string, string> {
                { "ru", "Главная" },
                { "en", "Main" }
            }},
            { "task_medium", new Dictionary<string, string> {
                { "ru", "Средняя" },
                { "en", "Medium" }
            }},
            { "task_easy", new Dictionary<string, string> {
                { "ru", "Лёгкая" },
                { "en", "Easy" }
            }},
            { "progress", new Dictionary<string, string> {
                { "ru", "Прогресс: {done} / 3" },
                { "en", "Progress: {done} / 3" }
            }},
            { "task_done", new Dictionary<string, string> {
                { "ru", "✔ {task} выполнена\n+{xp} XP\nПрогресс: {done} / 3" },
                { "en", "✔ {task} done\n+{xp} XP\nProgress: {done} / 3" }
            }},
            { "task_failed", new Dictionary<string, string> {
                { "ru", "❌ {task} провалена\n{xp} XP\nПрогресс: {done} / 3" },
                { "en", "❌ {task} failed\n{xp} XP\nProgress: {done} / 3" }
            }},
            { "deadline_30", new Dictionary<string, string> {
                { "ru", "⏰ Через 30 минут дедлайн: {task}\nОсталось мало времени!" },
                { "en", "⏰ Deadline in 30 minutes: {task}\nTime is running out!" }
            }},
            { "deadline_10", new Dictionary<string, string> {
                { "ru", "🚨 КРИТИЧЕСКИЙ ДЕДЛАЙН: {task}\nОсталось 10 минут!" },
                { "en", "🚨 CRITICAL DEADLINE: {task}\n10 minutes left!" }
            }},
            { "deadline_missed", new Dictionary<string, string> {
                { "ru", "❌ Дедлайн пропущен: {task}\n-5 XP" },
                { "en", "❌ Deadline missed: {task}\n-5 XP" }
            }},
            { "no_plan", new Dictionary<string, string> {
                { "ru", "У тебя ещё нет плана на сегодня" },
                { "en", "You don't have a plan for today yet" }
            }},
            { "settings_antilen_on", new Dictionary<string, string> {
                { "ru", "Анти-лень ВКЛ" },
                { "en", "Anti-laziness ON" }
            }},
            { "settings_antilen_off", new Dictionary<string, string> {
                { "ru", "Анти-лень ВЫКЛ" },
                { "en", "Anti-laziness OFF" }
            }},
            { "settings_hardmode_on", new Dictionary<string, string> {
                { "ru", "Hard mode ВКЛ" },
                { "en", "Hard mode ON" }
            }},
            { "settings_hardmode_off", new Dictionary<string, string> {
                { "ru", "Hard mode ВЫКЛ" },
                { "en", "Hard mode OFF" }
            }},
            { "mode_enabled", new Dictionary<string, string> {
                { "ru", "включён" },
                { "en", "enabled" }
            }},
            { "mode_disabled", new Dictionary<string, string> {
                { "ru", "выключен" },
                { "en", "disabled" }
            }},
            { "antilen_status", new Dictionary<string, string> {
                { "ru", "Анти-лень режим {status}. Ожидайте напоминания." },
                { "en", "Anti-laziness mode {status}. Expect reminders." }
            }},
            { "hardmode_status", new Dictionary<string, string> {
                { "ru", "Жёсткий режим (hardmode) {status}. Бот будет спрашивать, чем вы заняты." },
                { "en", "Hard mode {status}. The bot will ask what you are doing." }
            }},
            { "settings_language", new Dictionary<string, string> {
                { "ru", "🌍 Язык" },
                { "en", "🌍 Language" }
            }},
            { "settings_overview", new Dictionary<string, string> {
                { "ru", "⚙️ Настройки:\n\nАнти-лень: {antilen}\nHard mode: {hardmode}" },
                { "en", "⚙️ Settings:\n\nAnti-laziness: {antilen}\nHard mode: {hardmode}" }
            }},
            { "state_on", new Dictionary<string, string> { { "ru", "ВКЛ" }, { "en", "ON" } }},
            { "state_off", new Dictionary<string, string> { { "ru", "ВЫКЛ" }, { "en", "OFF" } }},
            { "deadline_until", new Dictionary<string, string> {
                { "ru", "До {time}" },
                { "en", "Until {time}" }
            }},
            { "deadline_none", new Dictionary<string, string> {
                { "ru", "Без дедлайна" },
                { "en", "No deadline" }
            }},
            { "btn_main_done", new Dictionary<string, string> { { "ru", "✔ Главная" }, { "en", "✔ Main" } }},
            { "btn_main_fail", new Dictionary<string, string> { { "ru", "❌ Главная" }, { "en", "❌ Main" } }},
            { "btn_main_deadline", new Dictionary<string, string> { { "ru", "⏰ Дедлайн главной" }, { "en", "⏰ Main deadline" } }},
            { "btn_med_done", new Dictionary<string, string> { { "ru", "✔ Средняя" }, { "en", "✔ Medium" } }},
            { "btn_med_fail", new Dictionary<string, string> { { "ru", "❌ Средняя" }, { "en", "❌ Medium" } }},
            { "btn_med_deadline", new Dictionary<string, string> { { "ru", "⏰ Дедлайн средней" }, { "en", "⏰ Medium deadline" } }},
            { "btn_easy_done", new Dictionary<string, string> { { "ru", "✔ Лёгкая" }, { "en", "✔ Easy" } }},
            { "btn_easy_fail", new Dictionary<string, string> { { "ru", "❌ Лёгкая" }, { "en", "❌ Easy" } }},
            { "btn_easy_deadline", new Dictionary<string, string> { { "ru", "⏰ Дедлайн лёгкой" }, { "en", "⏰ Easy deadline" } }},
            { "btn_edit_task", new Dictionary<string, string> { { "ru", "✏ Изменить задачу" }, { "en", "✏ Edit task" } }},
            { "btn_delete_plan", new Dictionary<string, string> { { "ru", "🗑 Удалить план" }, { "en", "🗑 Delete plan" } }},
            { "btn_back", new Dictionary<string, string> { { "ru", "⬅ Назад" }, { "en", "⬅ Back" } }},
            { "plan_completed_locked", new Dictionary<string, string> { { "ru", "План дня завершён 🔥 Изменения больше не принимаются." }, { "en", "Day plan completed 🔥 No more changes allowed." } }},
            { "status_already_set", new Dictionary<string, string> { { "ru", "Статус уже установлен" }, { "en", "Status is already set" } }},
            { "plan_completed", new Dictionary<string, string> { { "ru", "План дня завершён 🔥" }, { "en", "Day plan completed 🔥" } }},
            { "streak_increased", new Dictionary<string, string> { { "ru", "🔥 Твой стрик теперь составляет {streak} дней!" }, { "en", "🔥 Your streak is now {streak} days!" } }},
            { "lang_select", new Dictionary<string, string> { { "ru", "Выбери язык / Choose language" }, { "en", "Choose language / Выбери язык" } }},
            { "lang_set_ru", new Dictionary<string, string> { { "ru", "Язык установлен на Русский" }, { "en", "Language changed to Russian" } }},
            { "lang_set_en", new Dictionary<string, string> { { "ru", "Language set to English" }, { "en", "Language changed to English" } }},
            { "plan_already_exists", new Dictionary<string, string> {
                { "ru", "У тебя уже есть план на сегодня. Используй меню «План на сегодня» для изменения или удаления." },
                { "en", "You already have a plan for today. Use the 'Today plan' menu to edit or delete." }
            }},
            { "plan_prompt_main", new Dictionary<string, string> {
                { "ru", "Главная задача дня (самая важная)" },
                { "en", "Main task of the day (most important)" }
            }},
            { "plan_prompt_main_dl", new Dictionary<string, string> {
                { "ru", "Дедлайн для главной задачи (введите HH:mm или нажмите кнопку):" },
                { "en", "Deadline for the main task (enter HH:mm or press the button):" }
            }},
            { "plan_prompt_medium", new Dictionary<string, string> {
                { "ru", "Средняя задача дня" },
                { "en", "Medium task of the day" }
            }},
            { "plan_prompt_medium_dl", new Dictionary<string, string> {
                { "ru", "Дедлайн для средней задачи:" },
                { "en", "Deadline for the medium task:" }
            }},
            { "plan_prompt_easy", new Dictionary<string, string> {
                { "ru", "Лёгкая задача дня" },
                { "en", "Easy task of the day" }
            }},
            { "plan_prompt_easy_dl", new Dictionary<string, string> {
                { "ru", "Дедлайн для лёгкой задачи:" },
                { "en", "Deadline for the easy task:" }
            }},
            { "plan_saved", new Dictionary<string, string> {
                { "ru", "План на сегодня сохранён." },
                { "en", "Today's plan is saved." }
            }},
            { "plan_cancelled", new Dictionary<string, string> {
                { "ru", "Создание плана отменено." },
                { "en", "Plan creation cancelled." }
            }},
            { "plan_deleted", new Dictionary<string, string> {
                { "ru", "План на сегодня удалён." },
                { "en", "Today's plan deleted." }
            }},
            { "edit_task_prompt", new Dictionary<string, string> {
                { "ru", "Какую задачу изменить?\n1 Главную\n2 Среднюю\n3 Лёгкую" },
                { "en", "Which task to edit?\n1 Main\n2 Medium\n3 Easy" }
            }},
            { "edit_task_enter_text", new Dictionary<string, string> {
                { "ru", "Введи новый текст для {task} задачи:" },
                { "en", "Enter new text for the {task} task:" }
            }},
            { "edit_task_invalid", new Dictionary<string, string> {
                { "ru", "Пожалуйста, выбери номер задачи (1, 2 или 3). Если хочешь выйти из редактирования, напиши 0." },
                { "en", "Please select a task number (1, 2, or 3). If you want to cancel, type 0." }
            }},
            { "deadline_saved", new Dictionary<string, string> {
                { "ru", "Дедлайн сохранён: {time}" },
                { "en", "Deadline saved: {time}" }
            }},
            { "deadline_invalid", new Dictionary<string, string> {
                { "ru", "Пожалуйста, введите время в правильном формате (например, 18:30) или нажмите 'Без дедлайна'." },
                { "en", "Please enter time in the correct format (e.g. 18:30) or press 'No deadline'." }
            }},
            { "config_usage_antilen", new Dictionary<string, string> {
                { "ru", "Использование: /antilen on или /antilen off" },
                { "en", "Usage: /antilen on or /antilen off" }
            }},
            { "config_usage_hardmode", new Dictionary<string, string> {
                { "ru", "Использование: /hardmode on или /hardmode off" },
                { "en", "Usage: /hardmode on or /hardmode off" }
            }},
            { "level_info", new Dictionary<string, string> {
                { "ru", "Твой уровень: {level}\nXP: {xp} / {nextLevelXp}" },
                { "en", "Your level: {level}\nXP: {xp} / {nextLevelXp}" }
            }},
            { "profile_info", new Dictionary<string, string> {
                { "ru", "Статистика:\n\nУровень: {level}\nXP: {xp}\nВыполнено задач: {tasksCompleted}\nФокус-сессий: {focusSessions}" },
                { "en", "Statistics:\n\nLevel: {level}\nXP: {xp}\nTasks completed: {tasksCompleted}\nFocus sessions: {focusSessions}" }
            }},
            { "streak_info", new Dictionary<string, string> {
                { "ru", "🔥 Streak: {streak} дней\nЛучший результат: {best} дней" },
                { "en", "🔥 Streak: {streak} days\nBest result: {best} days" }
            }},
            { "main_menu", new Dictionary<string, string> {
                { "ru", "Главное меню" },
                { "en", "Main menu" }
            }},
            { "stats_title", new Dictionary<string, string> {
                { "ru", "Статистика" },
                { "en", "Statistics" }
            }},
            { "report_stats_week", new Dictionary<string, string> {
                { "ru", "Статистика за неделю:\n\n" },
                { "en", "Weekly statistics:\n\n" }
            }},
            { "report_stats_month", new Dictionary<string, string> {
                { "ru", "Статистика за месяц:\n\n" },
                { "en", "Monthly statistics:\n\n" }
            }},
            { "stats_completed", new Dictionary<string, string> {
                { "ru", "✔ Выполнено задач: {total}\n" },
                { "en", "✔ Tasks completed: {total}\n" }
            }},
            { "stats_main", new Dictionary<string, string> {
                { "ru", "🔥 Главные: {main}\n" },
                { "en", "🔥 Main: {main}\n" }
            }},
            { "stats_medium", new Dictionary<string, string> {
                { "ru", "⚙ Средние: {medium}\n" },
                { "en", "⚙ Medium: {medium}\n" }
            }},
            { "stats_easy", new Dictionary<string, string> {
                { "ru", "🟢 Лёгкие: {easy}\n" },
                { "en", "🟢 Easy: {easy}\n" }
            }},
            { "stats_xp", new Dictionary<string, string> {
                { "ru", "⭐ Получено XP: {xp}" },
                { "en", "⭐ XP Gained: {xp}" }
            }},
            { "report_stats_streak", new Dictionary<string, string> {
                { "ru", "Серия дней" },
                { "en", "Streak" }
            }},
            { "stats_best", new Dictionary<string, string> {
                { "ru", "Лучший результат" },
                { "en", "Best result" }
            }},
            { "stats_graph_title", new Dictionary<string, string> {
                { "ru", "Твоя продуктивность за 7 дней" },
                { "en", "Your productivity for 7 days" }
            }},
            { "stats_graph_caption", new Dictionary<string, string> {
                { "ru", "Твоя продуктивность за последнюю неделю!" },
                { "en", "Your productivity for the last week!" }
            }},
            { "report_stats_advice", new Dictionary<string, string> {
                { "ru", "💡 Я заметил, что ты редко фокусируешься. Попробуй выделить 25 минут без отвлечений — это поможет сделать дела быстрее." },
                { "en", "💡 I noticed you rarely stay focused. Try working for 25 minutes without distractions — it will help you get things done faster." }
            }},
            { "stats_advice_streak", new Dictionary<string, string> {
                { "ru", "🔥 Твоя серия дней просто огонь! Продолжай в том же духе, ты выработал отличную привычку. Главное — не сбивать темп!" },
                { "en", "🔥 Your daily streak is on fire! Keep it up, you've built a great habit. Just don't break the momentum!" }
            }},
            { "stats_advice_tasks", new Dictionary<string, string> {
                { "ru", "📉 За последнюю неделю выполнено маловато задач. Возможно, твой план на день слишком амбициозен? Попробуй ставить более мелкие и легкие задачи в план, чтобы втянуться." },
                { "en", "📉 few tasks have been completed in the last week. Maybe your daily plan is too ambitious? Try setting smaller and easier tasks to get into the flow." }
            }},
            { "stats_advice_default", new Dictionary<string, string> {
                { "ru", "✨ Ты молодец! Совет дня: если задача кажется неподъемной, разбей её на 3 мелких шага и сделай первый." },
                { "en", "✨ Good job! Tip of the day: if a task seems overwhelming, break it down into 3 small steps and just do the first one." }
            }},
            { "stats_advice_1", new Dictionary<string, string> {
                { "ru", "Начни с самой сложной задачи — дальше будет легче." },
                { "en", "Start with the hardest task — everything else will feel easier." }
            }},
            { "stats_advice_2", new Dictionary<string, string> {
                { "ru", "Убери отвлекающие факторы хотя бы на 20 минут." },
                { "en", "Remove distractions for at least 20 minutes." }
            }},
            { "stats_advice_3", new Dictionary<string, string> {
                { "ru", "Разбей большую задачу на маленькие шаги." },
                { "en", "Break a big task into smaller steps." }
            }},
            { "stats_advice_4", new Dictionary<string, string> {
                { "ru", "Сделай хотя бы 5 минут — это уже прогресс." },
                { "en", "Do at least 5 minutes — it still counts." }
            }},
            { "stats_advice_5", new Dictionary<string, string> {
                { "ru", "Не жди мотивации — начни действовать." },
                { "en", "Don't wait for motivation — start acting." }
            }},
            { "stats_advice_6", new Dictionary<string, string> {
                { "ru", "Сконцентрируйся на одной задаче, не переключайся." },
                { "en", "Focus on one task — avoid switching." }
            }},
            { "stats_advice_7", new Dictionary<string, string> {
                { "ru", "Начни прямо сейчас, даже если не хочется." },
                { "en", "Start right now, even if you don't feel like it." }
            }},
            { "stats_advice_8", new Dictionary<string, string> {
                { "ru", "Сделай задачу проще — главное начать." },
                { "en", "Make the task easier — the goal is to start." }
            }},
            { "stats_advice_9", new Dictionary<string, string> {
                { "ru", "Поставь таймер и работай без остановки." },
                { "en", "Set a timer and work without stopping." }
            }},
            { "stats_advice_10", new Dictionary<string, string> {
                { "ru", "Не жди идеального момента — его не будет." },
                { "en", "Don't wait for the perfect moment — it won't come." }
            }},
            { "stats_advice_11", new Dictionary<string, string> {
                { "ru", "Закрой лишние вкладки и сосредоточься." },
                { "en", "Close unnecessary tabs and focus." }
            }},
            { "stats_advice_12", new Dictionary<string, string> {
                { "ru", "Напомни себе, зачем ты это делаешь." },
                { "en", "Remind yourself why you're doing this." }
            }},
            { "stats_advice_13", new Dictionary<string, string> {
                { "ru", "Даже 10 минут работы лучше, чем ничего." },
                { "en", "Even 10 minutes of work is better than nothing." }
            }},
            { "stats_advice_14", new Dictionary<string, string> {
                { "ru", "Не перегружай себя — делай шаг за шагом." },
                { "en", "Don't overload yourself — go step by step." }
            }},
            { "stats_advice_15", new Dictionary<string, string> {
                { "ru", "Сконцентрируйся на результате, а не на сложности." },
                { "en", "Focus on the result, not the difficulty." }
            }},
            { "morning_reminder", new Dictionary<string, string> {
                { "ru", "Доброе утро.\nНе забудь создать план на сегодня:\n/plan" },
                { "en", "Good morning.\nDon't forget to create your plan for today:\n/plan" }
            }},
            { "evening_reminder", new Dictionary<string, string> {
                { "ru", "Подведи итог дня:\n/report" },
                { "en", "Summarize your day:\n/report" }
            }},
            { "stats_profile", new Dictionary<string, string> {
                { "ru", "📊 Профиль" },
                { "en", "📊 Profile" }
            }},
            { "stats_graph", new Dictionary<string, string> {
                { "ru", "📊 График" },
                { "en", "📊 Graph" }
            }},
            { "stats_week", new Dictionary<string, string> {
                { "ru", "📈 Неделя" },
                { "en", "📈 Week" }
            }},
            { "stats_month", new Dictionary<string, string> {
                { "ru", "📅 Месяц" },
                { "en", "📅 Month" }
            }},
            { "stats_streak", new Dictionary<string, string> {
                { "ru", "🔥 Стрик" },
                { "en", "🔥 Streak" }
            }},
            { "stats_advice", new Dictionary<string, string> {
                { "ru", "💡 Совет" },
                { "en", "💡 Advice" }
            }},
            { "anti_reminder_12", new Dictionary<string, string> {
                { "ru", "Ты ещё не сделал ни одной задачи из плана дня. Обязательно начни с самой важной!" },
                { "en", "You haven't completed any tasks from today's plan yet. Be sure to start with the most important one!" }
            }},
            { "anti_reminder_18", new Dictionary<string, string> {
                { "ru", "Ещё не было выполнения задач сегодня. Хочешь включить жёсткий режим? (Настройки -> Hard mode ВКЛ)" },
                { "en", "No tasks completed today yet. Want to enable Hard mode? (Settings -> Hard mode ON)" }
            }},
            { "hardmode_penalty", new Dictionary<string, string> {
                { "ru", "Ты не отвечаешь 3 раза подряд! Выписан штраф -15 XP ❌" },
                { "en", "You haven't replied 3 times in a row! Penalty of -15 XP issued ❌" }
            }},
            { "hardmode_question_0", new Dictionary<string, string> {
                { "ru", "⏳ Чем ты сейчас занимаешься?" },
                { "en", "⏳ What are you doing right now?" }
            }},
            { "hardmode_question_1", new Dictionary<string, string> {
                { "ru", "⚠️ Ты отвлёкся?" },
                { "en", "⚠️ Did you get distracted?" }
            }},
            { "hardmode_question_2", new Dictionary<string, string> {
                { "ru", "🔥 Вернись к задаче!" },
                { "en", "🔥 Get back to work!" }
            }},
            { "hardmode_question_3", new Dictionary<string, string> {
                { "ru", "⏳ Время идёт. Чем занят?" },
                { "en", "⏳ Time is ticking. What are you up to?" }
            }},
            { "hardmode_question_4", new Dictionary<string, string> {
                { "ru", "⚠️ На чём сейчас фокус?" },
                { "en", "⚠️ What's your focus right now?" }
            }},
            { "hardmode_question_5", new Dictionary<string, string> {
                { "ru", "🔥 Пора продолжать работу!" },
                { "en", "🔥 Time to continue working!" }
            }},
            { "hardmode_question_6", new Dictionary<string, string> {
                { "ru", "⏳ Как успехи? Вернись к задаче!" },
                { "en", "⏳ How is it going? Get back to the task!" }
            }},
            { "unknown_command", new Dictionary<string, string> {
                { "ru", "Используйте кнопки меню." },
                { "en", "Please use the menu buttons." }
            }}
        };

        public static string T(string key, string lang)
        {
            if (string.IsNullOrEmpty(lang)) lang = "ru";

            if (_texts != null && _texts.TryGetValue(key, out var translations))
            {
                if (translations.TryGetValue(lang, out var text))
                    return text;
                
                if (translations.TryGetValue("ru", out var defaultText))
                    return defaultText;
            }
            
            return key; // Fallback to key itself if all fails to prevent crashes
        }
    }
}
