# Platform Switching Guide for HackerSimulator

This guide explains how to switch between desktop and mobile modes in HackerSimulator, along with best practices for using each platform effectively.

## Platform Switching Overview

HackerSimulator offers a seamless experience across different device types by providing both desktop and mobile interfaces. You can switch between these modes at any time, even on the same device, to best suit your current needs and environment.

## Switching Between Platforms

### Desktop to Mobile Mode

1. **Via Settings Menu**:
   - Click the **Settings** icon in the system tray or open the Settings app
   - Navigate to **Display & Layout**
   - Toggle **Mobile Mode** to ON
   - Click **Apply Changes**
   - The interface will reload with mobile optimizations

2. **Via Command Line**:
   - Open the Terminal app
   - Type `system.togglePlatform("mobile")` and press Enter
   - Confirm the change when prompted

3. **Via Keyboard Shortcut**:
   - Press `Ctrl+Shift+M` (Windows/Linux) or `Cmd+Shift+M` (Mac)
   - Confirm the change when prompted

![Desktop to Mobile Transition](https://hackersimulator.net/assets/platform/desktop-to-mobile.gif)

### Mobile to Desktop Mode

1. **Via Settings**:
   - Tap the **Settings** icon in the bottom navigation bar
   - Select **Display & Layout**
   - Toggle **Desktop Mode** to ON
   - Tap **Apply Changes**
   - The interface will reload with desktop optimizations

2. **Via Command Line**:
   - Open the Terminal app
   - Type `system.togglePlatform("desktop")` and press Enter
   - Confirm the change when prompted

3. **Via Gesture**:
   - Use a four-finger pinch out gesture on the home screen
   - Confirm the change when prompted

![Mobile to Desktop Transition](https://hackersimulator.net/assets/platform/mobile-to-desktop.gif)

## What Happens During Platform Switching

When you switch platforms, HackerSimulator:

1. **Preserves your work**: All open applications, files, and data remain intact
2. **Adjusts the interface**: UI elements resize and reposition for the target platform
3. **Adapts controls**: Input methods change between touch-optimized and mouse/keyboard-optimized
4. **Maintains state**: Your session continues uninterrupted with all applications in the same state

## Platform-Specific Features and Limitations

### Desktop Mode Features

| Feature | Description | Availability in Mobile Mode |
|---------|-------------|----------------------------|
| **Multiple Windows** | Open multiple application windows side by side | Limited (Only one app visible at a time) |
| **Hover States** | Interactive elements respond to mouse hover | Not available (Uses active states instead) |
| **Keyboard Shortcuts** | Extensive keyboard shortcut support | Limited (Only essential shortcuts) |
| **Right-Click Menus** | Context menus via right-click | Limited (Uses long press instead) |
| **Drag and Drop** | Drag files between applications | Limited (Simplified version available) |
| **Advanced Tools** | Access to all developer tools and features | Some advanced features simplified |

### Mobile Mode Features

| Feature | Description | Availability in Desktop Mode |
|---------|-------------|----------------------------|
| **Touch Gestures** | Swipe, pinch, tap interactions | Limited (Only basic gestures supported) |
| **Bottom Navigation** | Quick access navigation bar | Not available (Uses top menu bar instead) |
| **Pull-to-Refresh** | Refresh content by pulling down | Not available (Uses refresh button instead) |
| **Haptic Feedback** | Vibration feedback for actions | Not available |
| **Virtual Keyboard** | Context-aware on-screen keyboard | Not available (Uses physical keyboard) |
| **One-handed Mode** | UI adjustments for single-hand use | Not available |

## Platform-Specific View Transitions

When switching platforms, certain views and interfaces transform as follows:

| Desktop Element | Mobile Transformation |
|-----------------|------------------------|
| **Desktop/Workspace** | Becomes home screen with app grid |
| **Taskbar** | Transforms into bottom navigation bar |
| **Window System** | Changes to fullscreen app views with back navigation |
| **Start Menu** | Becomes expandable app drawer |
| **System Tray** | Transforms into quick settings panel |
| **Multiple Monitors** | Adapts to single screen view |

![View Transition Diagram](https://hackersimulator.net/assets/platform/view-transitions.png)

## Best Practices for Platform Switching

### When to Use Desktop Mode

Desktop mode is ideal for:
- Complex multitasking requiring multiple windows
- Detailed coding and development work
- Using external monitors or large screens
- Tasks requiring precise mouse input
- Extended typing or keyboard-intensive work
- Advanced customization and configuration

### When to Use Mobile Mode

Mobile mode is best for:
- Touch-based devices or touchscreens
- On-the-go usage
- Quick tasks and monitoring
- Tablet or phone usage
- Single-task focus
- Limited screen space
- Touch or gesture-based interaction

### Data Continuity

For the best experience when switching between platforms:
- Save your work before switching platforms
- Close resource-intensive applications before switching
- Allow the system a few moments to complete the transition
- Verify all your applications have properly adapted after switching
- Restart any applications that don't display correctly after switching

## Troubleshooting Platform Switching Issues

### Common Issues and Solutions

#### Interface Elements Misaligned
- **Solution**: Force a UI refresh by opening Settings and toggling Dark/Light mode
- **Alternative**: Restart HackerSimulator completely

#### Applications Not Responding After Switch
- **Solution**: Close and reopen the affected application
- **Alternative**: Use Terminal to run `system.restartApp("[app-name]")`

#### Platform Switch Fails to Complete
- **Solution**: Cancel the switch and try again
- **Alternative**: Use Terminal to run `system.resetUI()`

#### Lost Work After Platform Switch
- **Solution**: Check Temp folder for auto-saved versions
- **Prevention**: Always save work before switching platforms

#### Performance Issues After Multiple Switches
- **Solution**: Restart HackerSimulator to clear memory
- **Prevention**: Limit platform switching during intensive work sessions

### Platform Switch Recovery Mode

If you encounter severe issues during platform switching:

1. Press `Ctrl+Alt+R` (or long-press Power button on mobile)
2. Select **Recovery Mode** from the menu
3. Choose **Restore UI to Default**
4. Select your preferred platform to restart with

## External Device Considerations

### Connecting External Devices

When connecting external hardware:

| Device Type | Effect on Platform Mode |
|-------------|-------------------------|
| **Keyboard** | Automatically enhances keyboard shortcuts in mobile mode |
| **Mouse** | Enables cursor and hover states in mobile mode |
| **External Monitor** | Prompts to switch to desktop mode |
| **Touchscreen** | Enables touch gestures in desktop mode |

### Cross-Platform Sessions

For working across multiple devices:
- Use HackerSimulator Cloud Sync to maintain workspace consistency
- Enable Cross-Device Session Handoff in Settings
- Log in with the same account on all devices
- Use the "Continue on Device" feature to transfer your session

## Conclusion

Platform switching in HackerSimulator provides flexibility for different devices and work contexts. By understanding the differences between platforms and following best practices, you can create a seamless workflow that adapts to your changing needs.

For more detailed information about specific features in each platform:
- See the [Desktop User Guide](desktop-user-guide.md)
- See the [Mobile User Guide](mobile-user-guide.md)

---

*Last updated: April 2025*
