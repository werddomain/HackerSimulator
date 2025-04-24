// In your application initialization code:
import { notification } from './notification';
import { UserSettings } from '../UserSettings';
import { OS } from '../os';

export function InitComponents(os: OS): Promise<void> {
    // Get your UserSettings instance
    const userSettings = os.getUserSettings();

    // Initialize the notification component with user settings
    notification.setUserSettings(userSettings);
    return Promise.resolve();
}
