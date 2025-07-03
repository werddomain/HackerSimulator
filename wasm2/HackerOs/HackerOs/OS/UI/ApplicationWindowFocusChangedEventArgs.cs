using System;

namespace HackerOs.OS.UI
{
    /// <summary>
    /// Event arguments for when the active application window changes
    /// </summary>
    public class ApplicationWindowFocusChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The window that lost focus
        /// </summary>
        public ApplicationWindow? OldWindow { get; set; }

        /// <summary>
        /// The window that gained focus
        /// </summary>
        public ApplicationWindow? NewWindow { get; set; }
    }
}
