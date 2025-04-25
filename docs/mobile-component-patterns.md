# Mobile Component Pattern Reference

This document provides reference implementations for common mobile UI patterns in the HackerSimulator project. Each pattern includes code examples, usage guidelines, and best practices.

## 1. Bottom Navigation Bar

Bottom navigation is the primary navigation method for mobile interfaces, providing quick access to main app sections.

### Implementation Example:

```typescript
/**
 * Bottom Navigation Bar Component
 * Provides primary navigation for mobile interfaces
 */
export class BottomNavigationBar {
  private element: HTMLElement;
  private items: NavItem[] = [];
  private activeItemId: string = '';
  private callback: (itemId: string) => void;
  
  constructor(container: HTMLElement, items: NavItem[], callback: (itemId: string) => void) {
    this.element = document.createElement('div');
    this.element.className = 'mobile-bottom-nav';
    this.items = items;
    this.callback = callback;
    
    this.render();
    container.appendChild(this.element);
    
    // Set initial active item
    if (items.length > 0) {
      this.setActiveItem(items[0].id);
    }
  }
  
  /**
   * Render the navigation bar
   */
  private render(): void {
    this.element.innerHTML = '';
    
    this.items.forEach(item => {
      const navItem = document.createElement('button');
      navItem.className = 'mobile-nav-item';
      navItem.setAttribute('data-id', item.id);
      navItem.innerHTML = `
        <div class="nav-icon">${item.icon}</div>
        <div class="nav-label">${item.label}</div>
      `;
      
      if (item.id === this.activeItemId) {
        navItem.classList.add('active');
      }
      
      navItem.addEventListener('click', () => {
        this.setActiveItem(item.id);
        this.callback(item.id);
      });
      
      this.element.appendChild(navItem);
    });
  }
  
  /**
   * Set the active navigation item
   */
  public setActiveItem(itemId: string): void {
    this.activeItemId = itemId;
    
    const items = this.element.querySelectorAll('.mobile-nav-item');
    items.forEach(item => {
      if (item.getAttribute('data-id') === itemId) {
        item.classList.add('active');
      } else {
        item.classList.remove('active');
      }
    });
  }
}

/**
 * Navigation Item interface
 */
export interface NavItem {
  id: string;
  label: string;
  icon: string;
  badge?: number;
}
```

### CSS Implementation:

```less
.mobile-bottom-nav {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  height: 60px;
  background-color: var(--bg-color);
  border-top: 1px solid var(--border-color);
  display: flex;
  align-items: center;
  justify-content: space-around;
  z-index: 100;
  padding-bottom: env(safe-area-inset-bottom, 0); // For iOS notched devices
  
  .mobile-nav-item {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    flex: 1;
    height: 100%;
    background: none;
    border: none;
    color: var(--text-color-secondary);
    padding: 0;
    position: relative;
    
    &.active {
      color: var(--accent-color);
      
      &::after {
        content: '';
        position: absolute;
        bottom: 0;
        left: 25%;
        width: 50%;
        height: 3px;
        background-color: var(--accent-color);
        border-radius: 3px 3px 0 0;
      }
    }
    
    .nav-icon {
      margin-bottom: 4px;
    }
    
    .nav-label {
      font-size: 12px;
    }
  }
}

/* Media query for landscape orientation */
@media (orientation: landscape) {
  .mobile-bottom-nav {
    height: 50px;
    
    .mobile-nav-item .nav-label {
      font-size: 10px;
    }
  }
}
```

### Usage Guidelines:

1. Limit to 3-5 navigation items to avoid overcrowding
2. Use clear, recognizable icons with short labels
3. Highlight the active section
4. Maintain consistent navigation across the app
5. Ensure the navigation bar is accessible from all screens

---

## 2. Swipeable Card Pattern

Swipeable cards allow users to perform actions by swiping horizontally, commonly used for dismissing or flagging items.

### Implementation Example:

```typescript
/**
 * Swipeable Card Component
 * Creates cards that support swipe gestures for actions
 */
export class SwipeableCard {
  private element: HTMLElement;
  private content: HTMLElement;
  private leftAction: HTMLElement;
  private rightAction: HTMLElement;
  private initialX: number = 0;
  private currentX: number = 0;
  private offsetX: number = 0;
  private swiping: boolean = false;
  
  constructor(
    container: HTMLElement, 
    cardData: any, 
    leftActionCallback?: () => void,
    rightActionCallback?: () => void
  ) {
    // Create card element
    this.element = document.createElement('div');
    this.element.className = 'swipeable-card';
    
    // Create content container
    this.content = document.createElement('div');
    this.content.className = 'card-content';
    this.content.innerHTML = this.renderContent(cardData);
    this.element.appendChild(this.content);
    
    // Create action elements
    this.leftAction = document.createElement('div');
    this.leftAction.className = 'left-action';
    this.leftAction.innerHTML = '<ion-icon name="archive-outline"></ion-icon>';
    this.element.appendChild(this.leftAction);
    
    this.rightAction = document.createElement('div');
    this.rightAction.className = 'right-action';
    this.rightAction.innerHTML = '<ion-icon name="trash-outline"></ion-icon>';
    this.element.appendChild(this.rightAction);
    
    // Add card to container
    container.appendChild(this.element);
    
    // Set up touch events
    this.setupTouchEvents(leftActionCallback, rightActionCallback);
  }
  
  /**
   * Render the card content
   */
  private renderContent(data: any): string {
    return `
      <div class="card-title">${data.title}</div>
      <div class="card-subtitle">${data.subtitle}</div>
      <div class="card-description">${data.description}</div>
    `;
  }
  
  /**
   * Set up touch events for swipe detection
   */
  private setupTouchEvents(leftActionCallback?: () => void, rightActionCallback?: () => void): void {
    this.element.addEventListener('touchstart', (e) => {
      this.initialX = e.touches[0].clientX;
      this.swiping = true;
      this.element.classList.add('swiping');
    });
    
    this.element.addEventListener('touchmove', (e) => {
      if (!this.swiping) return;
      
      this.currentX = e.touches[0].clientX;
      this.offsetX = this.currentX - this.initialX;
      
      // Limit the swipe range
      if (this.offsetX > 150) this.offsetX = 150;
      if (this.offsetX < -150) this.offsetX = -150;
      
      this.content.style.transform = `translateX(${this.offsetX}px)`;
      
      // Show appropriate action based on swipe direction
      if (this.offsetX > 50) {
        this.leftAction.classList.add('visible');
        this.rightAction.classList.remove('visible');
      } else if (this.offsetX < -50) {
        this.rightAction.classList.add('visible');
        this.leftAction.classList.remove('visible');
      } else {
        this.leftAction.classList.remove('visible');
        this.rightAction.classList.remove('visible');
      }
    });
    
    this.element.addEventListener('touchend', () => {
      this.swiping = false;
      this.element.classList.remove('swiping');
      
      // Check if swipe was far enough to trigger an action
      if (this.offsetX > 100 && leftActionCallback) {
        leftActionCallback();
      } else if (this.offsetX < -100 && rightActionCallback) {
        rightActionCallback();
      }
      
      // Reset position with animation
      this.content.style.transition = 'transform 0.3s ease';
      this.content.style.transform = 'translateX(0)';
      
      // Reset action visibility
      this.leftAction.classList.remove('visible');
      this.rightAction.classList.remove('visible');
      
      // Clear transition after animation completes
      setTimeout(() => {
        this.content.style.transition = '';
      }, 300);
    });
  }
}
```

### CSS Implementation:

```less
.swipeable-card {
  position: relative;
  width: 100%;
  background-color: var(--bg-color);
  border-radius: 8px;
  overflow: hidden;
  margin-bottom: 12px;
  box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
  
  &.swiping {
    box-shadow: 0 4px 8px rgba(0, 0, 0, 0.2);
  }
  
  .card-content {
    position: relative;
    z-index: 2;
    background-color: var(--bg-color);
    padding: 16px;
    width: 100%;
    min-height: 80px;
    
    .card-title {
      font-weight: 500;
      font-size: 16px;
      margin-bottom: 4px;
      color: var(--text-color-primary);
    }
    
    .card-subtitle {
      font-size: 14px;
      color: var(--text-color-secondary);
      margin-bottom: 8px;
    }
    
    .card-description {
      font-size: 14px;
      color: var(--text-color-primary);
    }
  }
  
  .left-action,
  .right-action {
    position: absolute;
    top: 0;
    bottom: 0;
    width: 80px;
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 1;
    opacity: 0;
    transition: opacity 0.2s ease;
    
    &.visible {
      opacity: 1;
    }
    
    ion-icon {
      font-size: 24px;
      color: white;
    }
  }
  
  .left-action {
    left: 0;
    background-color: var(--success-color);
  }
  
  .right-action {
    right: 0;
    background-color: var(--error-color);
  }
}
```

### Usage Guidelines:

1. Use for list items where quick actions are needed
2. Provide clear visual feedback during swipe
3. Implement animations for smooth transitions
4. Ensure action areas are color-coded for intuitive understanding
5. Include a visual indicator or tutorial for first-time users

---

## 3. Pull-to-Refresh Pattern

Pull-to-refresh allows users to update content by pulling down and releasing at the top of a scrollable area.

### Implementation Example:

```typescript
/**
 * Pull-to-Refresh Component
 * Implements pull-down-to-refresh functionality for content containers
 */
export class PullToRefresh {
  private container: HTMLElement;
  private indicatorElement: HTMLElement;
  private contentElement: HTMLElement;
  private refreshCallback: () => Promise<void>;
  
  private startY: number = 0;
  private currentY: number = 0;
  private isPulling: boolean = false;
  private isRefreshing: boolean = false;
  private maxPullDistance: number = 120;
  private refreshThreshold: number = 80;
  
  constructor(
    container: HTMLElement, 
    contentElement: HTMLElement,
    refreshCallback: () => Promise<void>
  ) {
    this.container = container;
    this.contentElement = contentElement;
    this.refreshCallback = refreshCallback;
    
    // Create pull indicator
    this.indicatorElement = document.createElement('div');
    this.indicatorElement.className = 'pull-to-refresh-indicator';
    this.indicatorElement.innerHTML = `
      <div class="indicator-icon">
        <ion-icon name="arrow-down-outline"></ion-icon>
      </div>
      <div class="indicator-text">Pull to refresh</div>
    `;
    
    // Insert indicator at the top of the container
    this.container.insertBefore(this.indicatorElement, this.container.firstChild);
    
    // Setup event listeners
    this.setupEventListeners();
  }
  
  /**
   * Set up event listeners for touch events
   */
  private setupEventListeners(): void {
    this.container.addEventListener('touchstart', (e) => {
      // Only enable pull to refresh when scrolled to top
      if (this.container.scrollTop > 0 || this.isRefreshing) return;
      
      this.startY = e.touches[0].clientY;
      this.isPulling = true;
    });
    
    this.container.addEventListener('touchmove', (e) => {
      if (!this.isPulling || this.isRefreshing) return;
      
      this.currentY = e.touches[0].clientY;
      let pullDistance = this.currentY - this.startY;
      
      // Restrict pull distance and add resistance
      if (pullDistance > 0) {
        e.preventDefault();
        
        // Apply resistance formula for natural feel
        pullDistance = Math.min(this.maxPullDistance, pullDistance * 0.5);
        
        // Update indicator and content position
        this.indicatorElement.style.transform = `translateY(${pullDistance - 60}px)`;
        this.contentElement.style.transform = `translateY(${pullDistance}px)`;
        
        // Update indicator appearance based on pull distance
        if (pullDistance > this.refreshThreshold) {
          this.indicatorElement.classList.add('ready');
          this.indicatorElement.querySelector('.indicator-text').textContent = 'Release to refresh';
          this.indicatorElement.querySelector('.indicator-icon ion-icon').setAttribute('name', 'refresh-outline');
        } else {
          this.indicatorElement.classList.remove('ready');
          this.indicatorElement.querySelector('.indicator-text').textContent = 'Pull to refresh';
          this.indicatorElement.querySelector('.indicator-icon ion-icon').setAttribute('name', 'arrow-down-outline');
        }
      }
    }, { passive: false });
    
    this.container.addEventListener('touchend', () => {
      if (!this.isPulling || this.isRefreshing) return;
      
      this.isPulling = false;
      
      const pullDistance = this.currentY - this.startY;
      
      // Check if pulled past threshold
      if (pullDistance > this.refreshThreshold) {
        this.performRefresh();
      } else {
        this.resetPosition();
      }
    });
  }
  
  /**
   * Execute the refresh callback
   */
  private async performRefresh(): Promise<void> {
    this.isRefreshing = true;
    
    // Update indicator to show loading state
    this.indicatorElement.classList.add('refreshing');
    this.indicatorElement.querySelector('.indicator-text').textContent = 'Refreshing...';
    this.indicatorElement.querySelector('.indicator-icon ion-icon').setAttribute('name', 'refresh-outline');
    this.indicatorElement.classList.add('spin');
    
    // Position indicator and content for refreshing state
    this.indicatorElement.style.transform = 'translateY(0)';
    this.contentElement.style.transform = 'translateY(60px)';
    
    try {
      // Call the refresh callback
      await this.refreshCallback();
    } catch (error) {
      console.error('Refresh failed:', error);
    }
    
    // Reset after refresh completes
    setTimeout(() => {
      this.isRefreshing = false;
      this.resetPosition();
      this.indicatorElement.classList.remove('refreshing', 'spin', 'ready');
    }, 300);
  }
  
  /**
   * Reset the indicator and content position
   */
  private resetPosition(): void {
    // Add transition for smooth animation
    this.indicatorElement.style.transition = 'transform 0.3s ease';
    this.contentElement.style.transition = 'transform 0.3s ease';
    
    // Reset positions
    this.indicatorElement.style.transform = 'translateY(-60px)';
    this.contentElement.style.transform = 'translateY(0)';
    
    // Clear transitions after animation completes
    setTimeout(() => {
      this.indicatorElement.style.transition = '';
      this.contentElement.style.transition = '';
    }, 300);
  }
}
```

### CSS Implementation:

```less
.pull-to-refresh-indicator {
  position: absolute;
  top: -60px;
  left: 0;
  right: 0;
  height: 60px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  z-index: 1;
  
  .indicator-icon {
    margin-bottom: 4px;
    
    ion-icon {
      font-size: 24px;
      color: var(--text-color-secondary);
    }
  }
  
  .indicator-text {
    font-size: 14px;
    color: var(--text-color-secondary);
  }
  
  &.ready {
    .indicator-icon ion-icon,
    .indicator-text {
      color: var(--accent-color);
    }
  }
  
  &.refreshing {
    .indicator-icon ion-icon,
    .indicator-text {
      color: var(--accent-color);
    }
  }
  
  &.spin .indicator-icon ion-icon {
    animation: spin 1.5s linear infinite;
  }
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}
```

### Usage Guidelines:

1. Use for screens where content updates are frequent and valuable
2. Provide clear visual feedback during each pull state
3. Keep refresh operations fast; aim for under 2 seconds
4. Show a loading indicator during refresh
5. Add resistance to the pull for a natural feel

---

## 4. Bottom Sheet Component

Bottom sheets provide additional options or information that slides up from the bottom of the screen.

### Implementation Example:

```typescript
/**
 * Bottom Sheet Component
 * Provides a slide-up panel for additional options or content
 */
export class BottomSheet {
  private element: HTMLElement;
  private overlayElement: HTMLElement;
  private contentElement: HTMLElement;
  private handleElement: HTMLElement;
  private isOpen: boolean = false;
  private initialY: number = 0;
  private currentY: number = 0;
  private startHeight: number = 0;
  
  constructor(content: string | HTMLElement, options: BottomSheetOptions = {}) {
    // Create overlay element
    this.overlayElement = document.createElement('div');
    this.overlayElement.className = 'bottom-sheet-overlay';
    
    // Create bottom sheet element
    this.element = document.createElement('div');
    this.element.className = 'bottom-sheet';
    
    // Add drag handle
    this.handleElement = document.createElement('div');
    this.handleElement.className = 'bottom-sheet-handle';
    this.element.appendChild(this.handleElement);
    
    // Create content container
    this.contentElement = document.createElement('div');
    this.contentElement.className = 'bottom-sheet-content';
    
    // Set content
    if (typeof content === 'string') {
      this.contentElement.innerHTML = content;
    } else {
      this.contentElement.appendChild(content);
    }
    
    this.element.appendChild(this.contentElement);
    
    // Apply custom options
    if (options.className) {
      this.element.classList.add(options.className);
    }
    
    if (options.fullscreen) {
      this.element.classList.add('fullscreen');
    }
    
    // Append elements to body
    document.body.appendChild(this.overlayElement);
    document.body.appendChild(this.element);
    
    // Set up event listeners
    this.setupEventListeners();
  }
  
  /**
   * Set up event listeners for the bottom sheet
   */
  private setupEventListeners(): void {
    // Handle click on overlay
    this.overlayElement.addEventListener('click', () => {
      this.close();
    });
    
    // Handle touch events for dragging
    this.handleElement.addEventListener('touchstart', (e) => {
      this.initialY = e.touches[0].clientY;
      this.startHeight = this.element.getBoundingClientRect().height;
      this.element.classList.add('dragging');
    });
    
    this.handleElement.addEventListener('touchmove', (e) => {
      this.currentY = e.touches[0].clientY;
      const deltaY = this.currentY - this.initialY;
      
      // Don't allow dragging up past the max height
      if (deltaY < 0) return;
      
      const newHeight = Math.max(100, this.startHeight - deltaY);
      const windowHeight = window.innerHeight;
      const heightPercent = (newHeight / windowHeight) * 100;
      
      this.element.style.height = `${heightPercent}vh`;
    });
    
    this.handleElement.addEventListener('touchend', () => {
      this.element.classList.remove('dragging');
      
      // Check the current height to decide if should close or snap back
      const currentHeight = this.element.getBoundingClientRect().height;
      const windowHeight = window.innerHeight;
      
      if (currentHeight < windowHeight * 0.25) {
        this.close();
      } else {
        this.element.style.height = '';
      }
    });
  }
  
  /**
   * Open the bottom sheet
   */
  public open(): void {
    if (this.isOpen) return;
    
    this.isOpen = true;
    document.body.classList.add('bottom-sheet-open');
    this.overlayElement.classList.add('visible');
    this.element.classList.add('open');
  }
  
  /**
   * Close the bottom sheet
   */
  public close(): void {
    if (!this.isOpen) return;
    
    this.isOpen = false;
    document.body.classList.remove('bottom-sheet-open');
    this.overlayElement.classList.remove('visible');
    this.element.classList.remove('open');
    
    // Remove from DOM after animation completes
    setTimeout(() => {
      if (this.element.parentNode) {
        document.body.removeChild(this.element);
      }
      if (this.overlayElement.parentNode) {
        document.body.removeChild(this.overlayElement);
      }
    }, 300);
  }
  
  /**
   * Update the content of the bottom sheet
   */
  public updateContent(content: string | HTMLElement): void {
    this.contentElement.innerHTML = '';
    
    if (typeof content === 'string') {
      this.contentElement.innerHTML = content;
    } else {
      this.contentElement.appendChild(content);
    }
  }
}

/**
 * Bottom Sheet Options interface
 */
export interface BottomSheetOptions {
  className?: string;
  fullscreen?: boolean;
}
```

### CSS Implementation:

```less
.bottom-sheet-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.5);
  z-index: 1000;
  opacity: 0;
  pointer-events: none;
  transition: opacity 0.3s ease;
  
  &.visible {
    opacity: 1;
    pointer-events: auto;
  }
}

.bottom-sheet {
  position: fixed;
  left: 0;
  right: 0;
  bottom: 0;
  height: 50vh;
  background-color: var(--bg-color);
  border-radius: 16px 16px 0 0;
  z-index: 1001;
  box-shadow: 0 -4px 20px rgba(0, 0, 0, 0.2);
  transform: translateY(100%);
  transition: transform 0.3s ease;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  
  &.open {
    transform: translateY(0);
  }
  
  &.fullscreen {
    height: 95vh;
  }
  
  &.dragging {
    transition: none;
  }
  
  .bottom-sheet-handle {
    width: 100%;
    height: 40px;
    display: flex;
    align-items: center;
    justify-content: center;
    cursor: grab;
    
    &::before {
      content: '';
      width: 36px;
      height: 4px;
      background-color: var(--border-color);
      border-radius: 2px;
    }
    
    &:active {
      cursor: grabbing;
    }
  }
  
  .bottom-sheet-content {
    flex: 1;
    overflow-y: auto;
    padding: 0 16px 16px;
  }
}

// Prevent body scrolling when sheet is open
body.bottom-sheet-open {
  overflow: hidden;
}

// Media query for larger devices
@media (min-width: 768px) {
  .bottom-sheet {
    max-width: 600px;
    left: 50%;
    transform: translateX(-50%) translateY(100%);
    
    &.open {
      transform: translateX(-50%) translateY(0);
    }
  }
}
```

### Usage Guidelines:

1. Use for secondary actions or detailed information
2. Implement a drag handle for intuitive interaction
3. Support swipe-down gesture to dismiss
4. Consider semi-transparent overlay for clear context
5. Adapt height based on content requirements

---

## 5. Floating Action Button (FAB)

Floating Action Buttons provide a persistent action that's immediately available to users.

### Implementation Example:

```typescript
/**
 * Floating Action Button Component
 * Provides a prominent action button that floats above the interface
 */
export class FloatingActionButton {
  private element: HTMLElement;
  private speedDialOpen: boolean = false;
  private actions: FABAction[] = [];
  
  constructor(container: HTMLElement, options: FABOptions) {
    this.element = document.createElement('div');
    this.element.className = 'floating-action-button';
    
    // Apply custom options
    if (options.position) {
      this.element.classList.add(`position-${options.position}`);
    }
    
    if (options.color) {
      this.element.classList.add(`color-${options.color}`);
    }
    
    // Set icon
    this.element.innerHTML = `<ion-icon name="${options.icon}"></ion-icon>`;
    
    // Add click handler
    if (options.actions && options.actions.length > 0) {
      this.actions = options.actions;
      this.setupSpeedDial();
    } else if (options.onClick) {
      this.element.addEventListener('click', options.onClick);
    }
    
    // Add to container
    container.appendChild(this.element);
  }
  
  /**
   * Set up speed dial functionality for multiple actions
   */
  private setupSpeedDial(): void {
    // Add speed dial class
    this.element.classList.add('speed-dial');
    
    // Create the action buttons container
    const actionsContainer = document.createElement('div');
    actionsContainer.className = 'fab-actions';
    
    // Add each action button
    this.actions.forEach(action => {
      const actionButton = document.createElement('div');
      actionButton.className = 'fab-action-button';
      
      if (action.color) {
        actionButton.classList.add(`color-${action.color}`);
      }
      
      actionButton.innerHTML = `
        <span class="fab-action-label">${action.label}</span>
        <div class="fab-action-icon">
          <ion-icon name="${action.icon}"></ion-icon>
        </div>
      `;
      
      actionButton.addEventListener('click', (e) => {
        e.stopPropagation();
        action.onClick();
        this.toggleSpeedDial();
      });
      
      actionsContainer.appendChild(actionButton);
    });
    
    // Insert actions before the main button
    this.element.insertBefore(actionsContainer, this.element.firstChild);
    
    // Toggle speed dial on click
    this.element.addEventListener('click', () => {
      this.toggleSpeedDial();
    });
    
    // Close speed dial when clicking elsewhere
    document.addEventListener('click', (e) => {
      if (this.speedDialOpen && !this.element.contains(e.target as Node)) {
        this.closeSpeedDial();
      }
    });
  }
  
  /**
   * Toggle the speed dial open/closed state
   */
  private toggleSpeedDial(): void {
    if (this.speedDialOpen) {
      this.closeSpeedDial();
    } else {
      this.openSpeedDial();
    }
  }
  
  /**
   * Open the speed dial
   */
  private openSpeedDial(): void {
    this.speedDialOpen = true;
    this.element.classList.add('open');
    
    // Change the main icon to a close icon
    const mainIcon = this.element.querySelector('ion-icon');
    if (mainIcon) {
      mainIcon.setAttribute('name', 'close-outline');
    }
  }
  
  /**
   * Close the speed dial
   */
  private closeSpeedDial(): void {
    this.speedDialOpen = false;
    this.element.classList.remove('open');
    
    // Restore the original icon
    const mainIcon = this.element.querySelector('ion-icon');
    if (mainIcon) {
      mainIcon.setAttribute('name', this.actions[0].icon);
    }
  }
}

/**
 * Floating Action Button Options interface
 */
export interface FABOptions {
  icon: string;
  position?: 'bottom-right' | 'bottom-left' | 'top-right' | 'top-left';
  color?: 'primary' | 'secondary' | 'success' | 'danger' | 'warning';
  onClick?: () => void;
  actions?: FABAction[];
}

/**
 * Floating Action Button Action interface
 */
export interface FABAction {
  icon: string;
  label: string;
  color?: 'primary' | 'secondary' | 'success' | 'danger' | 'warning';
  onClick: () => void;
}
```

### CSS Implementation:

```less
.floating-action-button {
  position: fixed;
  width: 56px;
  height: 56px;
  border-radius: 50%;
  background-color: var(--accent-color);
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.3);
  z-index: 100;
  cursor: pointer;
  transition: all 0.3s ease;
  
  // Default position
  right: 16px;
  bottom: 16px;
  
  // Position variants
  &.position-bottom-left {
    right: auto;
    left: 16px;
    bottom: 16px;
  }
  
  &.position-top-right {
    right: 16px;
    bottom: auto;
    top: 16px;
  }
  
  &.position-top-left {
    right: auto;
    left: 16px;
    bottom: auto;
    top: 16px;
  }
  
  // Color variants
  &.color-primary { background-color: var(--primary-color); }
  &.color-secondary { background-color: var(--secondary-color); }
  &.color-success { background-color: var(--success-color); }
  &.color-danger { background-color: var(--error-color); }
  &.color-warning { background-color: var(--warning-color); }
  
  // Icon
  ion-icon {
    font-size: 24px;
  }
  
  // Active state
  &:active {
    transform: scale(0.95);
    box-shadow: 0 2px 5px rgba(0, 0, 0, 0.2);
  }
  
  // Speed dial
  &.speed-dial {
    z-index: 200;
    
    .fab-actions {
      position: absolute;
      bottom: 100%;
      display: flex;
      flex-direction: column;
      align-items: center;
      margin-bottom: 16px;
      opacity: 0;
      pointer-events: none;
      transition: opacity 0.3s ease;
      
      .fab-action-button {
        display: flex;
        align-items: center;
        margin-bottom: 16px;
        transform: scale(0);
        transition: transform 0.3s ease;
        
        .fab-action-label {
          background-color: rgba(0, 0, 0, 0.8);
          color: white;
          padding: 4px 8px;
          border-radius: 4px;
          font-size: 14px;
          margin-right: 8px;
          box-shadow: 0 2px 5px rgba(0, 0, 0, 0.2);
          white-space: nowrap;
        }
        
        .fab-action-icon {
          width: 40px;
          height: 40px;
          border-radius: 50%;
          background-color: var(--accent-color);
          display: flex;
          align-items: center;
          justify-content: center;
          box-shadow: 0 2px 5px rgba(0, 0, 0, 0.2);
          
          ion-icon {
            font-size: 20px;
            color: white;
          }
        }
        
        // Color variants for action buttons
        &.color-primary .fab-action-icon { background-color: var(--primary-color); }
        &.color-secondary .fab-action-icon { background-color: var(--secondary-color); }
        &.color-success .fab-action-icon { background-color: var(--success-color); }
        &.color-danger .fab-action-icon { background-color: var(--error-color); }
        &.color-warning .fab-action-icon { background-color: var(--warning-color); }
      }
    }
    
    // Open state
    &.open {
      .fab-actions {
        opacity: 1;
        pointer-events: auto;
        
        .fab-action-button {
          transform: scale(1);
          
          // Staggered animation for buttons
          @for $i from 1 through 5 {
            &:nth-child(#{$i}) {
              transition-delay: (0.05s * (5 - $i));
            }
          }
        }
      }
    }
  }
}

// Safe area inset for notched devices
@supports (padding: env(safe-area-inset-bottom)) {
  .floating-action-button {
    bottom: ~"calc(16px + env(safe-area-inset-bottom))";
    
    &.position-bottom-left {
      bottom: ~"calc(16px + env(safe-area-inset-bottom))";
    }
  }
}
```

### Usage Guidelines:

1. Use for the primary action on a screen
2. Place consistently across screens (usually bottom-right)
3. Choose a color that stands out against the background
4. Consider using speed dial for related actions
5. Ensure the button doesn't obscure important content

---

## 6. Skeleton Loading Pattern

Skeleton loading provides a preview of the content structure while data is loading, reducing perceived wait time.

### Implementation Example:

```typescript
/**
 * Skeleton Loading Component
 * Creates placeholder loading indicators that mimic content structure
 */
export class SkeletonLoader {
  private container: HTMLElement;
  private contentLoader: () => Promise<void>;
  private skeletonElements: HTMLElement[] = [];
  
  constructor(container: HTMLElement, contentLoader: () => Promise<void>, count: number = 1) {
    this.container = container;
    this.contentLoader = contentLoader;
    
    // Create skeleton elements
    this.createSkeletons(count);
    
    // Load the actual content
    this.loadContent();
  }
  
  /**
   * Create skeleton placeholder elements
   */
  private createSkeletons(count: number): void {
    for (let i = 0; i < count; i++) {
      const skeleton = document.createElement('div');
      skeleton.className = 'skeleton-item';
      skeleton.innerHTML = `
        <div class="skeleton-image pulse"></div>
        <div class="skeleton-content">
          <div class="skeleton-title pulse"></div>
          <div class="skeleton-text pulse"></div>
          <div class="skeleton-text pulse"></div>
          <div class="skeleton-text pulse" style="width: 70%"></div>
        </div>
      `;
      
      this.skeletonElements.push(skeleton);
      this.container.appendChild(skeleton);
    }
  }
  
  /**
   * Load the actual content
   */
  private async loadContent(): Promise<void> {
    try {
      await this.contentLoader();
      
      // Remove skeleton elements after content is loaded
      this.removeSkeleton();
    } catch (error) {
      console.error('Error loading content:', error);
      
      // Show error state or retry option
      this.showError();
    }
  }
  
  /**
   * Remove all skeleton elements
   */
  private removeSkeleton(): void {
    this.skeletonElements.forEach(element => {
      if (element.parentNode) {
        element.classList.add('fade-out');
        
        // Remove after fade animation
        setTimeout(() => {
          if (element.parentNode) {
            element.parentNode.removeChild(element);
          }
        }, 300);
      }
    });
    
    this.skeletonElements = [];
  }
  
  /**
   * Show error state
   */
  private showError(): void {
    this.skeletonElements.forEach(element => {
      element.classList.remove('pulse');
      element.classList.add('error');
    });
  }
  
  /**
   * Manually trigger a reload of the content
   */
  public reload(): void {
    // Remove previous error state if any
    this.skeletonElements.forEach(element => {
      element.classList.remove('error');
      element.classList.add('pulse');
    });
    
    this.loadContent();
  }
}
```

### CSS Implementation:

```less
.skeleton-item {
  display: flex;
  margin-bottom: 16px;
  background-color: var(--bg-color);
  border-radius: 8px;
  overflow: hidden;
  padding: 16px;
  transition: opacity 0.3s ease;
  
  &.fade-out {
    opacity: 0;
  }
  
  .skeleton-image {
    width: 80px;
    height: 80px;
    background-color: var(--skeleton-color, rgba(255, 255, 255, 0.1));
    border-radius: 8px;
    flex-shrink: 0;
    margin-right: 16px;
  }
  
  .skeleton-content {
    flex: 1;
    
    .skeleton-title {
      height: 24px;
      background-color: var(--skeleton-color, rgba(255, 255, 255, 0.1));
      border-radius: 4px;
      margin-bottom: 12px;
    }
    
    .skeleton-text {
      height: 16px;
      background-color: var(--skeleton-color, rgba(255, 255, 255, 0.1));
      border-radius: 4px;
      margin-bottom: 8px;
      
      &:last-child {
        margin-bottom: 0;
      }
    }
  }
  
  // Pulsing animation effect
  .pulse {
    position: relative;
    overflow: hidden;
    
    &::after {
      content: '';
      position: absolute;
      top: 0;
      left: 0;
      right: 0;
      bottom: 0;
      background: linear-gradient(
        90deg,
        transparent,
        rgba(255, 255, 255, 0.1),
        transparent
      );
      animation: pulse 1.5s infinite;
    }
  }
  
  // Error state
  &.error {
    background-color: rgba(255, 0, 0, 0.05);
    border: 1px solid rgba(255, 0, 0, 0.1);
  }
}

// Card style skeleton
.skeleton-card {
  border-radius: 8px;
  overflow: hidden;
  background-color: var(--bg-color);
  margin-bottom: 16px;
  
  .skeleton-card-image {
    width: 100%;
    height: 180px;
    background-color: var(--skeleton-color, rgba(255, 255, 255, 0.1));
  }
  
  .skeleton-card-content {
    padding: 16px;
    
    .skeleton-card-title {
      height: 24px;
      background-color: var(--skeleton-color, rgba(255, 255, 255, 0.1));
      border-radius: 4px;
      margin-bottom: 16px;
    }
    
    .skeleton-card-text {
      height: 16px;
      background-color: var(--skeleton-color, rgba(255, 255, 255, 0.1));
      border-radius: 4px;
      margin-bottom: 8px;
    }
  }
}

@keyframes pulse {
  0% { transform: translateX(-100%); }
  100% { transform: translateX(100%); }
}
```

### Usage Guidelines:

1. Match skeleton structure to the expected content layout
2. Use subtle animations to indicate loading state
3. Keep the skeleton visually simple but recognizable
4. Replace skeleton with real content as soon as it's available
5. Include error handling for failed content loads

---

By implementing these pattern references, you'll create a consistent mobile UI experience across the HackerSimulator project. Each component follows mobile best practices and includes built-in support for accessibility, performance, and user experience considerations.
