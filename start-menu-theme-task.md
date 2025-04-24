# Start Menu Theme Task List

## App Tile Color System Enhancement

The goal is to make app tile colors more flexible and themeable, allowing users to customize colors while also supporting themes. The current system assigns fixed colors to app classes like `app-tile-terminal` and falls back to letter-based classes (`app-tile-A` to `app-tile-I`).

### Tasks

1. [x] Update the `ThemeManager.ts` to include app tile color variables from themes
   - Added support for custom app tile foreground colors
   - Added theme option to disable custom app tile colors

2. [x] Modify `StartMenuController.ts` to support custom app tile color assignments
   - Added method to parse color assignments from user settings (format: `appId;colorClass`)
   - Implemented color class assignment for newly pinned apps
   - Updated `pinApp` method to assign colors

3. [x] Update `UserSettings.ts` to store and retrieve app color assignments
   - Added methods to get/set app color preferences
   - Support the semicolon-delimited format: `terminal;A,browser;B,calculator;G`

4. [x] Update app tile styling in `start-menu.less`
   - Ensured theme variables are used for app tile colors
   - Added support for custom foreground colors

5. [x] Add UI in the settings app for customizing app tile colors
   - Created a new "Start Menu" section in the settings app
   - Added toggle for disabling custom app tile colors
   - Implemented color selector UI for each pinned app
   - Added CSS styling for the new UI components

6. [x] Implement random color assignment for newly pinned apps
   - Added logic to select an unused color when a new app is pinned
   - Ensured consistent color assignment across sessions

7. [x] Test and validate theming integration
   - Verified that themes can override app tile colors
   - Tested that user customizations persist correctly

8. [x] Add documentation for the new features
   - Created app-tile-colors.md with feature documentation
   - Documented the format for app color assignments
