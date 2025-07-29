// auth-activity-tracker.js
// Tracks user activity to refresh authentication tokens

window.authActivityTracker = {
    // Reference to the .NET object that will handle activity notifications
    dotNetHelper: null,
    
    // Events to track for activity
    activityEvents: ['mousemove', 'click', 'keydown', 'touchstart', 'scroll'],
    
    // Debounce timer to prevent excessive refreshes
    debounceTimer: null,
    debounceDelay: 1000, // 1 second
    
    // Register all event listeners
    registerActivityListeners: function (dotNetHelper) {
        if (!dotNetHelper) {
            console.warn('No .NET helper provided for auth activity tracking');
            return;
        }
        
        this.dotNetHelper = dotNetHelper;
        
        // Remove existing listeners if any
        this.removeActivityListeners();
        
        // Register new listeners
        this.activityEvents.forEach(eventName => {
            document.addEventListener(eventName, this.handleUserActivity.bind(this));
        });
        
        // Also track navigation events
        window.addEventListener('popstate', this.handleUserActivity.bind(this));
        
        console.log('Auth activity listeners registered');
    },
    
    // Remove all event listeners
    removeActivityListeners: function() {
        if (!this.dotNetHelper) return;
        
        this.activityEvents.forEach(eventName => {
            document.removeEventListener(eventName, this.handleUserActivity.bind(this));
        });
        
        window.removeEventListener('popstate', this.handleUserActivity.bind(this));
        this.dotNetHelper = null;
    },
    
    // Handle user activity with debounce
    handleUserActivity: function() {
        if (!this.dotNetHelper) return;
        
        clearTimeout(this.debounceTimer);
        
        this.debounceTimer = setTimeout(() => {
            // Call the .NET method to refresh token if needed
            this.dotNetHelper.invokeMethodAsync('OnUserActivity');
        }, this.debounceDelay);
    }
};
