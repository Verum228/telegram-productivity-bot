using System;
using System.Collections.Generic;
using System.IO;
using SkiaSharp;

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

        public Stream GenerateGraphImage(Dictionary<string, int> data, string lang)
        {
            int width = 800;
            int height = 500;
            
            var info = new SKImageInfo(width, height);
            using var surface = SKSurface.Create(info);
            var canvas = surface.Canvas;
            
            canvas.Clear(SKColors.White);

            using var titlePaint = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = true,
                TextSize = 24,
                Typeface = SKTypeface.Default,
                IsStroke = false
            };

            using var labelPaint = new SKPaint
            {
                Color = SKColors.Black,
                IsAntialias = true,
                TextSize = 18,
                Typeface = SKTypeface.Default,
                IsStroke = false
            };

            using var linePaint = new SKPaint
            {
                Color = SKColors.Black,
                StrokeWidth = 2,
                IsAntialias = true,
                IsStroke = true
            };

            using var gridPaint = new SKPaint
            {
                Color = SKColors.LightGray,
                StrokeWidth = 1,
                IsAntialias = true,
                IsStroke = true
            };

            using var barPaint = new SKPaint
            {
                Color = new SKColor(100, 150, 250, 255),
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            
            using var barBorderPaint = new SKPaint
            {
                Color = SKColors.Black,
                StrokeWidth = 2,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };

            canvas.DrawText(LocalizationService.T("stats_graph_title", lang), 100, 50, titlePaint);

            int startX = 100;
            int endX = 750;
            int startY = 400;
            
            canvas.DrawLine(startX, startY, endX, startY, linePaint);
            canvas.DrawLine(startX, startY, startX, 100, linePaint);

            for (int i = 0; i <= 3; i++)
            {
                int y = startY - (i * 80);
                canvas.DrawText(i.ToString(), startX - 30, y + 6, labelPaint);
                canvas.DrawLine(startX, y, endX, y, gridPaint);
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
                    var rect = new SKRect(x, y, x + barWidth, startY);
                    canvas.DrawRect(rect, barPaint);
                    canvas.DrawRect(rect, barBorderPaint);
                }

                // X label
                canvas.DrawText(dayLabel, x - 5, startY + 25, labelPaint);
                index++;
            }

            using var image = surface.Snapshot();
            using var dataImage = image.Encode(SKEncodedImageFormat.Png, 100);
            
            var ms = new MemoryStream();
            dataImage.SaveTo(ms);
            ms.Position = 0;
            return ms;
        }
    }
}
