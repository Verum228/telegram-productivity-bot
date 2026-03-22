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
                { "ru", "💡 Я заметил, что ты редко используешь таймер Pomodoro. Попробуй включить /focus 25 сегодня — это поможет не отвлекаться и сделать дела быстрее." },
                { "en", "💡 I noticed you rarely use the Pomodoro timer. Try using /focus 25 today — it will help you stay focused and get things done faster." }
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
