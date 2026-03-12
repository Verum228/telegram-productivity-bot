using System;
using System.Threading.Tasks;

namespace TelegramProductivityBot
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Укажите токен вашего бота, который выдаст @BotFather
            string botToken = "8778537631:AAEvDx4X1d8HSrGMUTKNh28izyCM_9i_Kfs";

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

            Console.WriteLine("Бот запущен. Нажмите Enter для его остановки.");
            Console.ReadLine();

            // Остановка бота при завершении программы
            botService.Stop();
        }
    }
}
