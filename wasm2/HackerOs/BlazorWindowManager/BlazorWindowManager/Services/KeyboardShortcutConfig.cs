namespace BlazorWindowManager.Services;

/// <summary>
/// Centralized keyboard shortcut configuration with browser-safe key combinations
/// </summary>
public class KeyboardShortcutConfig
{
    #region Shortcut Definitions
    
    /// <summary>
    /// Window switcher activation (default: Ctrl+` backtick)
    /// </summary>
    public KeyCombination WindowSwitcher { get; set; } = new("Ctrl", "`");
    
    /// <summary>
    /// Close active window (default: Ctrl+Shift+W)
    /// </summary>
    public KeyCombination CloseWindow { get; set; } = new("Ctrl+Shift", "W");
    
    /// <summary>
    /// Maximize/restore active window (default: Ctrl+Shift+M)
    /// </summary>
    public KeyCombination MaximizeWindow { get; set; } = new("Ctrl+Shift", "M");
    
    /// <summary>
    /// Minimize active window (default: Ctrl+Shift+N)
    /// </summary>
    public KeyCombination MinimizeWindow { get; set; } = new("Ctrl+Shift", "N");
    
    /// <summary>
    /// Move window with arrow keys (default: Ctrl+Arrow)
    /// </summary>
    public KeyCombination MoveWindow { get; set; } = new("Ctrl", "Arrow");
    
    /// <summary>
    /// Resize window with arrow keys (default: Ctrl+Shift+Arrow)
    /// </summary>
    public KeyCombination ResizeWindow { get; set; } = new("Ctrl+Shift", "Arrow");
    
    /// <summary>
    /// Open window context menu (default: Ctrl+Shift+Space)
    /// </summary>
    public KeyCombination WindowContextMenu { get; set; } = new("Ctrl+Shift", " ");
    
    /// <summary>
    /// Cycle through windows forward (default: Ctrl+Tab)
    /// </summary>
    public KeyCombination CycleWindowsNext { get; set; } = new("Ctrl", "Tab");
    
    /// <summary>
    /// Cycle through windows backward (default: Ctrl+Shift+Tab)
    /// </summary>
    public KeyCombination CycleWindowsPrevious { get; set; } = new("Ctrl+Shift", "Tab");
    
    #endregion
    
    #region Predicate Functions (Callback Style)
    
    /// <summary>
    /// Check if the event represents the window switcher shortcut
    /// </summary>
    /// <param name="eventArgs">Keyboard event details</param>
    /// <returns>True if this is the window switcher shortcut</returns>
    public bool IsWindowSwitcherShortcut(KeyboardEventArgs eventArgs)
    {
        return WindowSwitcher.Matches(eventArgs);
    }
    
    /// <summary>
    /// Check if the event represents the close window shortcut
    /// </summary>
    /// <param name="eventArgs">Keyboard event details</param>
    /// <returns>True if this is the close window shortcut</returns>
    public bool IsCloseWindowShortcut(KeyboardEventArgs eventArgs)
    {
        return CloseWindow.Matches(eventArgs);
    }
    
    /// <summary>
    /// Check if the event represents the maximize window shortcut
    /// </summary>
    /// <param name="eventArgs">Keyboard event details</param>
    /// <returns>True if this is the maximize window shortcut</returns>
    public bool IsMaximizeWindowShortcut(KeyboardEventArgs eventArgs)
    {
        return MaximizeWindow.Matches(eventArgs);
    }
    
    /// <summary>
    /// Check if the event represents the minimize window shortcut
    /// </summary>
    /// <param name="eventArgs">Keyboard event details</param>
    /// <returns>True if this is the minimize window shortcut</returns>
    public bool IsMinimizeWindowShortcut(KeyboardEventArgs eventArgs)
    {
        return MinimizeWindow.Matches(eventArgs);
    }
    
    /// <summary>
    /// Check if the event represents a window movement shortcut
    /// </summary>
    /// <param name="eventArgs">Keyboard event details</param>
    /// <returns>True if this is a window movement shortcut</returns>
    public bool IsMoveWindowShortcut(KeyboardEventArgs eventArgs)
    {
        return MoveWindow.MatchesWithArrowKey(eventArgs);
    }
    
    /// <summary>
    /// Check if the event represents a window resize shortcut
    /// </summary>
    /// <param name="eventArgs">Keyboard event details</param>
    /// <returns>True if this is a window resize shortcut</returns>
    public bool IsResizeWindowShortcut(KeyboardEventArgs eventArgs)
    {
        return ResizeWindow.MatchesWithArrowKey(eventArgs);
    }
    
    /// <summary>
    /// Check if the event represents the window context menu shortcut
    /// </summary>
    /// <param name="eventArgs">Keyboard event details</param>
    /// <returns>True if this is the context menu shortcut</returns>
    public bool IsWindowContextMenuShortcut(KeyboardEventArgs eventArgs)
    {
        return WindowContextMenu.Matches(eventArgs);
    }
    
    /// <summary>
    /// Check if the event represents cycling to next window
    /// </summary>
    /// <param name="eventArgs">Keyboard event details</param>
    /// <returns>True if this is the cycle next shortcut</returns>
    public bool IsCycleWindowsNextShortcut(KeyboardEventArgs eventArgs)
    {
        return CycleWindowsNext.Matches(eventArgs);
    }
    
    /// <summary>
    /// Check if the event represents cycling to previous window
    /// </summary>
    /// <param name="eventArgs">Keyboard event details</param>
    /// <returns>True if this is the cycle previous shortcut</returns>
    public bool IsCycleWindowsPreviousShortcut(KeyboardEventArgs eventArgs)
    {
        return CycleWindowsPrevious.Matches(eventArgs);
    }
    
    #endregion
    
    #region Utility Methods
    
    /// <summary>
    /// Get all configured shortcuts as a dictionary
    /// </summary>
    /// <returns>Dictionary of shortcut names and their key combinations</returns>
    public Dictionary<string, KeyCombination> GetAllShortcuts()
    {
        return new Dictionary<string, KeyCombination>
        {
            { nameof(WindowSwitcher), WindowSwitcher },
            { nameof(CloseWindow), CloseWindow },
            { nameof(MaximizeWindow), MaximizeWindow },
            { nameof(MinimizeWindow), MinimizeWindow },
            { nameof(MoveWindow), MoveWindow },
            { nameof(ResizeWindow), ResizeWindow },
            { nameof(WindowContextMenu), WindowContextMenu },
            { nameof(CycleWindowsNext), CycleWindowsNext },
            { nameof(CycleWindowsPrevious), CycleWindowsPrevious }
        };
    }
    
    /// <summary>
    /// Get human-readable descriptions of all shortcuts
    /// </summary>
    /// <returns>Dictionary of shortcut names and descriptions</returns>
    public Dictionary<string, string> GetShortcutDescriptions()
    {
        return new Dictionary<string, string>
        {
            { nameof(WindowSwitcher), $"Activate window switcher ({WindowSwitcher})" },
            { nameof(CloseWindow), $"Close active window ({CloseWindow})" },
            { nameof(MaximizeWindow), $"Maximize/restore window ({MaximizeWindow})" },
            { nameof(MinimizeWindow), $"Minimize window ({MinimizeWindow})" },
            { nameof(MoveWindow), $"Move window with arrows ({MoveWindow}+Arrow)" },
            { nameof(ResizeWindow), $"Resize window with arrows ({ResizeWindow}+Arrow)" },
            { nameof(WindowContextMenu), $"Open window context menu ({WindowContextMenu})" },
            { nameof(CycleWindowsNext), $"Cycle to next window ({CycleWindowsNext})" },
            { nameof(CycleWindowsPrevious), $"Cycle to previous window ({CycleWindowsPrevious})" }
        };
    }
    
    /// <summary>
    /// Create a default configuration with browser-safe key combinations
    /// </summary>
    /// <returns>Default keyboard shortcut configuration</returns>
    public static KeyboardShortcutConfig CreateDefault()
    {
        return new KeyboardShortcutConfig();
    }
    
    /// <summary>
    /// Create an alternative configuration for users who prefer different keys
    /// </summary>
    /// <returns>Alternative keyboard shortcut configuration</returns>
    public static KeyboardShortcutConfig CreateAlternative()
    {
        return new KeyboardShortcutConfig
        {
            WindowSwitcher = new("Ctrl+Shift", "Tab"),
            CloseWindow = new("Ctrl+Alt", "W"),
            MaximizeWindow = new("Ctrl+Alt", "M"),
            MinimizeWindow = new("Ctrl+Alt", "N"),
            MoveWindow = new("Alt", "Arrow"),
            ResizeWindow = new("Alt+Shift", "Arrow"),
            WindowContextMenu = new("Ctrl+Alt", " "),
            CycleWindowsNext = new("Ctrl+Shift", "`"),
            CycleWindowsPrevious = new("Ctrl+Shift+Alt", "`")
        };
    }
    
    #endregion
}

/// <summary>
/// Represents a keyboard key combination
/// </summary>
public class KeyCombination
{
    public string Modifiers { get; set; }
    public string Key { get; set; }
    
    public KeyCombination(string modifiers, string key)
    {
        Modifiers = modifiers;
        Key = key;
    }
    
    /// <summary>
    /// Check if this key combination matches the given keyboard event
    /// </summary>
    /// <param name="eventArgs">Keyboard event to check</param>
    /// <returns>True if the event matches this combination</returns>
    public bool Matches(KeyboardEventArgs eventArgs)
    {
        // Handle special case for space key
        var keyToMatch = Key == " " ? " " : Key;
        if (keyToMatch == " " && eventArgs.Key != " ") return false;
        if (keyToMatch != " " && !string.Equals(eventArgs.Key, keyToMatch, StringComparison.OrdinalIgnoreCase)) return false;
        
        return MatchesModifiers(eventArgs);
    }
    
    /// <summary>
    /// Check if this key combination matches with any arrow key
    /// </summary>
    /// <param name="eventArgs">Keyboard event to check</param>
    /// <returns>True if the event matches this combination with an arrow key</returns>
    public bool MatchesWithArrowKey(KeyboardEventArgs eventArgs)
    {
        var isArrowKey = eventArgs.Key == "ArrowUp" || eventArgs.Key == "ArrowDown" || 
                        eventArgs.Key == "ArrowLeft" || eventArgs.Key == "ArrowRight";
        
        if (!isArrowKey) return false;
        
        return MatchesModifiers(eventArgs);
    }
    
    private bool MatchesModifiers(KeyboardEventArgs eventArgs)
    {
        var requiredModifiers = Modifiers.Split('+').Select(m => m.Trim()).ToList();
        
        var hasCtrl = requiredModifiers.Contains("Ctrl", StringComparer.OrdinalIgnoreCase);
        var hasAlt = requiredModifiers.Contains("Alt", StringComparer.OrdinalIgnoreCase);
        var hasShift = requiredModifiers.Contains("Shift", StringComparer.OrdinalIgnoreCase);
        var hasMeta = requiredModifiers.Contains("Meta", StringComparer.OrdinalIgnoreCase);
        
        return eventArgs.CtrlKey == hasCtrl &&
               eventArgs.AltKey == hasAlt &&
               eventArgs.ShiftKey == hasShift &&
               eventArgs.MetaKey == hasMeta;
    }
    
    public override string ToString()
    {
        return Key == "Arrow" ? $"{Modifiers}+Arrow" : $"{Modifiers}+{Key}";
    }
}

/// <summary>
/// Keyboard event arguments for shortcut matching
/// </summary>
public class KeyboardEventArgs
{
    public string Key { get; set; } = string.Empty;
    public bool CtrlKey { get; set; }
    public bool AltKey { get; set; }
    public bool ShiftKey { get; set; }
    public bool MetaKey { get; set; }
    
    public KeyboardEventArgs() { }
    
    public KeyboardEventArgs(string key, bool ctrlKey = false, bool altKey = false, bool shiftKey = false, bool metaKey = false)
    {
        Key = key;
        CtrlKey = ctrlKey;
        AltKey = altKey;
        ShiftKey = shiftKey;
        MetaKey = metaKey;
    }
    
    public override string ToString()
    {
        var modifiers = new List<string>();
        if (CtrlKey) modifiers.Add("Ctrl");
        if (AltKey) modifiers.Add("Alt");
        if (ShiftKey) modifiers.Add("Shift");
        if (MetaKey) modifiers.Add("Meta");
        
        return modifiers.Count > 0 ? $"{string.Join("+", modifiers)}+{Key}" : Key;
    }
}
