using System;
using System.Threading.Tasks;

namespace TelegramProductivityBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Укажите токен вашего бота, который выдаст @BotFather
            string botToken = "";

            if (botToken == "YOUR_BOT_TOKEN_HERE")
            {
                Console.WriteLine("Пожалуйста, замените 'YOUR_BOT_TOKEN_HERE' на реальный токен в Program.cs.");
                return;
            }

            // Инициализируем наш сервис бота
            var botService = new BotService(botToken);

            Console.WriteLine("Инициализация бота...");
            
            // Запуск long polling
            await botService.StartAsync();

            Console.WriteLine("Бот запущен.");

            // Остановка бота при завершении программы
            
            await Task.Delay(-1);

            
        }
    }
}
