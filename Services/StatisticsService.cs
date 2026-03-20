using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;

namespace TelegramProductivityBot.Services
{
    public class StatisticsService
    {
        private readonly TaskService _taskService;

        public StatisticsService(TaskService taskService)
        {
            _taskService = taskService;
        }

        public Dictionary<string, int> GetLast7DaysStats(long userId)
        {
            var history = _taskService.GetPast7DaysPlans(userId);
            var stats = new Dictionary<string, int>();

            for(int i = 6; i >= 0; i--)
            {
                var d = DateTime.Today.AddDays(-i);
                stats[d.ToString("yyyy-MM-dd")] = 0;
            }

            foreach (var plan in history)
            {
                string dateStr = plan.CreatedDate.ToString("yyyy-MM-dd");
                if (stats.ContainsKey(dateStr))
                {
                    int completed = (plan.MainDone ? 1 : 0) + (plan.MediumDone ? 1 : 0) + (plan.EasyDone ? 1 : 0);
                    stats[dateStr] = completed;
                }
            }

            return stats;
        }

        public Stream GenerateGraphImage(Dictionary<string, int> data)
        {
            int width = 800;
            int height = 500;
            Bitmap bmp = new Bitmap(width, height);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                // For linux/cross-platform compatibility, use generic fonts if Arial is missing
                Font titleFont = new Font(FontFamily.GenericSansSerif, 24, FontStyle.Bold);
                Font labelFont = new Font(FontFamily.GenericSansSerif, 16);
                Pen drawPen = new Pen(Color.Black, 2);
                Brush barBrush = new SolidBrush(Color.FromArgb(100, 150, 250));
                
                g.DrawString("Твоя продуктивность за 7 дней", titleFont, Brushes.Black, new PointF(100, 20));

                int startX = 100;
                int endX = 750;
                int startY = 400;
                
                g.DrawLine(drawPen, startX, startY, endX, startY);
                g.DrawLine(drawPen, startX, startY, startX, 100);

                for (int i = 0; i <= 3; i++)
                {
                    int y = startY - (i * 80);
                    g.DrawString(i.ToString(), labelFont, Brushes.Black, startX - 40, y - 12);
                    g.DrawLine(new Pen(Color.LightGray, 1), startX, y, endX, y);
                }

                int daysCount = 7;
                int barWidth = 40;
                int spacing = (endX - startX) / (daysCount + 1);
                
                int index = 0;
                foreach (var kvp in data)
                {
                    DateTime dt;
                    string dayLabel = kvp.Key;
                    if (DateTime.TryParse(kvp.Key, out dt))
                    {
                        string[] sysDays = { "Вс", "Пн", "Вт", "Ср", "Чт", "Пт", "Сб" };
                        dayLabel = sysDays[(int)dt.DayOfWeek];
                    }

                    int value = kvp.Value > 3 ? 3 : kvp.Value;
                    int barHeight = value * 80;
                    int x = startX + spacing * (index + 1) - (barWidth / 2);
                    int y = startY - barHeight;

                    if (barHeight > 0)
                    {
                        g.FillRectangle(barBrush, x, y, barWidth, barHeight);
                        g.DrawRectangle(drawPen, x, y, barWidth, barHeight);
                    }

                    g.DrawString(dayLabel, labelFont, Brushes.Black, x - 5, startY + 10);
                    index++;
                }
            }

            var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            return ms;
        }
    }
}
