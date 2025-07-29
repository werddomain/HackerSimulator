using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HackerOs.OS.Applications.Attributes;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace HackerOs.OS.Applications.UI.Windows.Test
{
    /// <summary>
    /// Test application for verifying window state and application state synchronization
    /// </summary>
    [App("Window State Test", "window-state-test", 
        Description = "Test application for window state integration",
        Type = ApplicationType.WindowedApplication,
        AllowMultipleInstances = true)]
    public partial class WindowStateTest
    {
        [Inject]
        private ILogger<WindowStateTest> Logger { get; set; }
        
        /// <summary>
        /// Represents a log entry for the event log
        /// </summary>
        private class LogEntry
        {
            public DateTime Time { get; set; } = DateTime.Now;
            public string Message { get; set; } = string.Empty;
        }
        
        /// <summary>
        /// List of log entries for tracking events
        /// </summary>
        private List<LogEntry> EventLog { get; } = new();
        
        /// <summary>
        /// Add a log entry to the event log
        /// </summary>
        private void AddLog(string message)
        {
            EventLog.Add(new LogEntry { Time = DateTime.Now, Message = message });
            Logger?.LogInformation(message);
            
            // Keep log to a reasonable size
            if (EventLog.Count > 100)
            {
                EventLog.RemoveAt(0);
            }
            
            StateHasChanged();
        }
        
        /// <summary>
        /// Simulate a crash in the application
        /// </summary>
        private Task SimulateCrash()
        {
            AddLog("Simulating application crash...");
            // This would be implemented with actual window base functionality
            AddLog("Application state set to Crashed");
            return Task.CompletedTask;
        }
        
        protected override void OnInitialized()
        {
            AddLog("Component initialized");
            base.OnInitialized();
        }

        protected override void OnAfterRender(bool firstRender)
        {
            if (firstRender)
            {
                AddLog("Component rendered for the first time");
            }
            base.OnAfterRender(firstRender);
        }
    }
}
}
