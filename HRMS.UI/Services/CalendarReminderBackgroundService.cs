using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using HRMS.Infrastructure.Persistence;
using HRMS.Domain.Entities.Core;
using HRMS.Domain.Common;

namespace HRMS.UI.Services
{
    public class CalendarReminderBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CalendarReminderBackgroundService> _logger;

        public CalendarReminderBackgroundService(IServiceScopeFactory scopeFactory, ILogger<CalendarReminderBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("CalendarReminderBackgroundService is running.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessRemindersAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing calendar and training event reminders.");
                }

                // Check every 60 seconds
                await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
            }

            _logger.LogInformation("CalendarReminderBackgroundService is stopping.");
        }

        private async Task ProcessRemindersAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var now = SriLankaTime.Now;

            // ─────────────────────────────────────────────────────────────
            // 1. Process CalendarEvents Reminders
            // ─────────────────────────────────────────────────────────────
            var upcomingEvents = await context.CalendarEvents
                .Include(e => e.Employee)
                .Where(e => e.StartTime > now && e.StartTime <= now.AddHours(25))
                .ToListAsync();

            bool changesMade = false;

            foreach (var evt in upcomingEvents)
            {
                var timeUntilEvent = evt.StartTime - now;

                // ── Day Before Reminder (within 24 hours) ──
                if (timeUntilEvent <= TimeSpan.FromHours(24) && timeUntilEvent > TimeSpan.FromHours(1) && !evt.DayBeforeNotificationSent)
                {
                    await SendCalendarNotificationAsync(context, evt, 
                        $"📅 Tomorrow: {evt.Title}",
                        $"Reminder: '{evt.Title}' is scheduled for tomorrow at {evt.StartTime:hh:mm tt}. Location: {evt.Location ?? "Online/Not specified"}.");
                    evt.DayBeforeNotificationSent = true;
                    changesMade = true;
                }

                // ── Hour Before Reminder (within 1 hour) ──
                if (timeUntilEvent <= TimeSpan.FromHours(1) && timeUntilEvent > TimeSpan.Zero && !evt.HourBeforeNotificationSent)
                {
                    var venue = !string.IsNullOrWhiteSpace(evt.MeetingLink) ? $"Meeting Link: {evt.MeetingLink}" : $"Venue: {evt.Location ?? "Scheduled Location"}";
                    await SendCalendarNotificationAsync(context, evt,
                        $"⏰ In 1 Hour: {evt.Title}",
                        $"Reminder: '{evt.Title}' starts in 1 hour at {evt.StartTime:hh:mm tt}. {venue}");
                    evt.HourBeforeNotificationSent = true;
                    changesMade = true;
                }
            }

            // ─────────────────────────────────────────────────────────────
            // 2. Process Training Session Reminders for Enrolled Employees
            // ─────────────────────────────────────────────────────────────
            var scheduledTrainings = await context.Trainings
                .Include(t => t.EmployeeTrainings)
                    .ThenInclude(et => et.Employee)
                .Where(t => t.Status == "Scheduled" && t.Date >= now.Date && t.Date <= now.Date.AddDays(2))
                .ToListAsync();

            foreach (var training in scheduledTrainings)
            {
                var sessionDateTime = training.Date.Date.Add(training.StartTime);
                var timeUntilSession = sessionDateTime - now;

                if (timeUntilSession <= TimeSpan.Zero) continue;

                // Day before reminder for enrolled employees
                if (timeUntilSession <= TimeSpan.FromHours(24) && timeUntilSession > TimeSpan.FromHours(1))
                {
                    foreach (var et in training.EmployeeTrainings)
                    {
                        if (et.Employee == null || string.IsNullOrWhiteSpace(et.Employee.Email)) continue;

                        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == et.Employee.Email || u.UserName == et.Employee.Email);
                        if (user == null) continue;

                        var notifTitle = $"🎓 Training Tomorrow: {training.Title}";
                        var alreadySent = await context.Notifications.AnyAsync(n => n.UserId == user.Id && n.Title == notifTitle);
                        if (!alreadySent)
                        {
                            context.Notifications.Add(new Notification
                            {
                                UserId = user.Id,
                                Title = notifTitle,
                                Message = $"Reminder: You are scheduled for '{training.Title}' tomorrow at {sessionDateTime:hh:mm tt}. Venue: {training.Location ?? "Head Office"}. Trainer: {training.TrainerName ?? "Instructor"}.",
                                TargetUrl = "/Calendar/Index",
                                IsRead = false,
                                CreatedAt = SriLankaTime.Now
                            });
                            changesMade = true;
                        }
                    }
                }

                // 1 hour before reminder for enrolled employees
                if (timeUntilSession <= TimeSpan.FromHours(1) && timeUntilSession > TimeSpan.Zero)
                {
                    foreach (var et in training.EmployeeTrainings)
                    {
                        if (et.Employee == null || string.IsNullOrWhiteSpace(et.Employee.Email)) continue;

                        var user = await context.Users.FirstOrDefaultAsync(u => u.Email == et.Employee.Email || u.UserName == et.Employee.Email);
                        if (user == null) continue;

                        var notifTitle = $"⏰ Training in 1 Hour: {training.Title}";
                        var alreadySent = await context.Notifications.AnyAsync(n => n.UserId == user.Id && n.Title == notifTitle);
                        if (!alreadySent)
                        {
                            context.Notifications.Add(new Notification
                            {
                                UserId = user.Id,
                                Title = notifTitle,
                                Message = $"Your training session '{training.Title}' starts at {sessionDateTime:hh:mm tt}. Venue: {training.Location ?? "Head Office"}.",
                                TargetUrl = "/Calendar/Index",
                                IsRead = false,
                                CreatedAt = SriLankaTime.Now
                            });
                            changesMade = true;
                        }
                    }
                }
            }

            if (changesMade)
            {
                await context.SaveChangesAsync();
            }
        }

        private async Task SendCalendarNotificationAsync(ApplicationDbContext context, HRMS.Domain.Entities.Calendar.CalendarEvent evt, string title, string message)
        {
            // 1. Notify Creator User
            if (!string.IsNullOrWhiteSpace(evt.CreatedByUserId))
            {
                var creatorUser = await context.Users.FirstOrDefaultAsync(u => u.Id == evt.CreatedByUserId || u.Email == evt.CreatedByUserId || u.UserName == evt.CreatedByUserId);
                if (creatorUser != null)
                {
                    context.Notifications.Add(new Notification
                    {
                        UserId = creatorUser.Id,
                        Title = title,
                        Message = message,
                        TargetUrl = "/Calendar/Index",
                        IsRead = false,
                        CreatedAt = SriLankaTime.Now
                    });
                }
            }

            // 2. Notify Associated Employee if different from creator
            if (evt.EmployeeId.HasValue && evt.Employee != null && !string.IsNullOrWhiteSpace(evt.Employee.Email))
            {
                var empUser = await context.Users.FirstOrDefaultAsync(u => u.Email == evt.Employee.Email || u.UserName == evt.Employee.Email);
                if (empUser != null && empUser.Id != evt.CreatedByUserId)
                {
                    context.Notifications.Add(new Notification
                    {
                        UserId = empUser.Id,
                        Title = title,
                        Message = message,
                        TargetUrl = "/Calendar/Index",
                        IsRead = false,
                        CreatedAt = SriLankaTime.Now
                    });
                }
            }
        }
    }
}
