/**
 * Mobile Task Switcher
 * Provides a card-based interface for switching between running applications on mobile devices
 */

import { IWindowManager } from './window-manager-interface';
import { MobileWindowManager } from './mobile-window-manager';
import { AppManager } from './app-manager';

export class MobileTaskSwitcher {
  private element: HTMLElement | null = null;
  private windowManager: IWindowManager;
  private appManager: AppManager;
  private isVisible: boolean = false;

  /**
   * Create a new mobile task switcher
   * @param windowManager Window manager instance
   * @param appManager App manager instance
   */
  constructor(windowManager: IWindowManager, appManager: AppManager) {
    this.windowManager = windowManager;
    this.appManager = appManager;
  }

  /**
   * Initialize the task switcher
   */
  public init(): void {
    // Create task switcher element if it doesn't exist
    if (!this.element) {
      this.element = document.createElement('div');
      this.element.className = 'mobile-task-switcher';
      document.body.appendChild(this.element);

      // Add close button
      const closeButton = document.createElement('button');
      closeButton.className = 'task-switcher-close';
      closeButton.innerHTML = '×';
      closeButton.addEventListener('click', () => this.hide());
      this.element.appendChild(closeButton);

      // Add task cards container
      const taskCardsContainer = document.createElement('div');
      taskCardsContainer.className = 'task-cards-container';
      this.element.appendChild(taskCardsContainer);
    }

    // Set up keyboard event to show task switcher
    document.addEventListener('keydown', (e) => {
      // Alt+Tab or similar for mobile could be implemented
      if (e.altKey && e.key === 'Tab') {
        e.preventDefault();
        this.toggle();
      }
    });
  }

  /**
   * Show the task switcher
   */
  public show(): void {
    if (!this.element) return;
    
    // Make visible
    this.element.classList.add('visible');
    this.isVisible = true;
    
    // Update task cards
    this.updateTaskCards();
    
    // Start listening for swipe gestures
    this.setupSwipeGestures();
  }

  /**
   * Hide the task switcher
   */
  public hide(): void {
    if (!this.element) return;
    
    // Hide
    this.element.classList.remove('visible');
    this.isVisible = false;
  }

  /**
   * Toggle the task switcher visibility
   */
  public toggle(): void {
    if (this.isVisible) {
      this.hide();
    } else {
      this.show();
    }
  }

  /**
   * Update the task cards based on current running applications
   */
  private updateTaskCards(): void {
    if (!this.element) return;
    
    // Get task cards container
    const taskCardsContainer = this.element.querySelector('.task-cards-container');
    if (!taskCardsContainer) return;
    
    // Clear existing cards
    taskCardsContainer.innerHTML = '';
    
    // Get window information from window manager
    const runningWindows = this.getRunningWindows();
    
    if (runningWindows.length === 0) {
      // No running windows, show empty state
      const emptyState = document.createElement('div');
      emptyState.className = 'task-switcher-empty-state';
      emptyState.innerHTML = `
        <div class="empty-icon">
          <svg xmlns="http://www.w3.org/2000/svg" width="48" height="48" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1" stroke-linecap="round" stroke-linejoin="round">
            <rect x="3" y="3" width="18" height="18" rx="2" ry="2"></rect>
            <line x1="3" y1="9" x2="21" y2="9"></line>
            <line x1="9" y1="21" x2="9" y2="9"></line>
          </svg>
        </div>
        <h3>No Running Apps</h3>
        <p>All apps are currently closed.</p>
      `;
      taskCardsContainer.appendChild(emptyState);
      return;
    }
    
    // Create task cards
    runningWindows.forEach(window => {
      const taskCard = this.createTaskCard(window);
      taskCardsContainer.appendChild(taskCard);
    });
  }

  /**
   * Get running windows from the window manager
   */
  private getRunningWindows(): any[] {
    // This implementation will depend on the actual window manager API
    // For now, we'll use a hardcoded example
    if (this.windowManager instanceof MobileWindowManager) {
      // If we have access to the mobile window manager
      const mobileWindowManager = this.windowManager as MobileWindowManager;
      
      // We need to get all the windows - this would rely on a method in the window manager
      // that may not be directly accessible
      // For demonstration, we'll assume there's a way to get this information
      return this.getWindowsFromManager();
    }
    
    return [];
  }

  /**
   * Get windows information from the window manager
   * This is a placeholder implementation that would be replaced with actual code
   */
  private getWindowsFromManager(): any[] {
    // In a real implementation, we would get this from the window manager
    // For now, we'll simulate some windows
    const runningApps = this.appManager.getRunningApps();
    
    return runningApps.map(app => {
      return {
        id: `window-${app.id}`,
        title: app.name,
        appId: app.id,
        icon: app.icon || '',
        screenshot: null // In a real implementation, we might have screenshots
      };
    });
  }

  /**
   * Create a task card for a window
   */
  private createTaskCard(window: any): HTMLElement {
    const taskCard = document.createElement('div');
    taskCard.className = 'task-card';
    taskCard.setAttribute('data-window-id', window.id);
    
    // Get app info from app manager
    const appInfo = this.appManager.getAppInfo(window.appId);
    
    // Create card content
    taskCard.innerHTML = `
      <div class="task-card-header">
        <div class="task-card-icon">${window.icon || appInfo?.icon || ''}</div>
        <div class="task-card-title">${window.title || appInfo?.name || 'Unknown'}</div>
        <button class="task-card-close">×</button>
      </div>
      <div class="task-card-preview">
        ${window.screenshot ? `<img src="${window.screenshot}" alt="${window.title}">` : 
        `<div class="task-card-placeholder">
           <svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1" stroke-linecap="round" stroke-linejoin="round">
             <rect x="3" y="3" width="18" height="18" rx="2" ry="2"></rect>
             <line x1="3" y1="9" x2="21" y2="9"></line>
           </svg>
         </div>`}
      </div>
    `;
    
    // Add event listeners
    taskCard.addEventListener('click', () => {
      // Activate the window
      if (this.windowManager instanceof MobileWindowManager) {
        this.restoreAndActivateWindow(window.id);
      }
      this.hide();
    });
    
    // Add close button event
    const closeButton = taskCard.querySelector('.task-card-close');
    if (closeButton) {
      closeButton.addEventListener('click', (e) => {
        e.stopPropagation();
        this.closeWindow(window.id);
        taskCard.classList.add('closing');
        
        // Remove card after animation
        setTimeout(() => {
          taskCard.remove();
          
          // Update task cards (check if empty)
          this.updateTaskCards();
        }, 300);
      });
    }
    
    // Setup swipe to dismiss
    this.setupSwipeToClose(taskCard, window.id);
    
    return taskCard;
  }

  /**
   * Restore and activate a window
   */
  private restoreAndActivateWindow(windowId: string): void {
    if (this.windowManager.isWindowMinimized(windowId)) {
      this.windowManager.restoreWindow(windowId);
    }
    this.windowManager.activateWindow(windowId);
  }

  /**
   * Close a window
   */
  private closeWindow(windowId: string): void {
    this.windowManager.closeWindow(windowId);
  }

  /**
   * Set up swipe gestures for task switcher
   */
  private setupSwipeGestures(): void {
    if (!this.element) return;
    
    // Get task cards container
    const taskCardsContainer = this.element.querySelector('.task-cards-container');
    if (!taskCardsContainer) return;
    
    let startX = 0;
    let startY = 0;
    let currentX = 0;
    let currentY = 0;
    let isSwiping = false;
    let activeCard: HTMLElement | null = null;
    
    taskCardsContainer.addEventListener('touchstart', (e) => {
      startX = e.touches[0].clientX;
      startY = e.touches[0].clientY;
      currentX = startX;
      currentY = startY;
      
      // Get the card being touched
      activeCard = (e.target as HTMLElement).closest('.task-card') as HTMLElement;
      if (activeCard) {
        activeCard.style.transition = '';
      }
    });
    
    taskCardsContainer.addEventListener('touchmove', (e) => {
      if (!activeCard) return;
      
      currentX = e.touches[0].clientX;
      currentY = e.touches[0].clientY;
      
      const deltaX = currentX - startX;
      const deltaY = currentY - startY;
      
      // If horizontal swipe is greater than vertical, handle as swipe
      if (Math.abs(deltaX) > Math.abs(deltaY) && Math.abs(deltaX) > 10) {
        isSwiping = true;
        activeCard.style.transform = `translateX(${deltaX}px)`;
        
        // Change opacity based on swipe distance
        const opacity = 1 - Math.min(Math.abs(deltaX) / 200, 0.5);
        activeCard.style.opacity = opacity.toString();
        
        // Prevent scrolling
        e.preventDefault();
      }
    });
    
    taskCardsContainer.addEventListener('touchend', () => {
      if (!activeCard) return;
      
      const deltaX = currentX - startX;
      
      // If significant horizontal swipe, remove the card
      if (isSwiping && Math.abs(deltaX) > 100) {
        activeCard.style.transition = 'transform 0.3s ease-out, opacity 0.3s ease-out';
        activeCard.style.transform = `translateX(${deltaX > 0 ? '100%' : '-100%'})`;
        activeCard.style.opacity = '0';
        
        // Get window ID
        const windowId = activeCard.getAttribute('data-window-id');
        if (windowId) {
          // Close the window
          this.closeWindow(windowId);
        }
        
        // Remove card after animation
        setTimeout(() => {
          activeCard?.remove();
          
          // Update task cards (check if empty)
          this.updateTaskCards();
        }, 300);
      } else {
        // Reset position
        activeCard.style.transition = 'transform 0.3s ease-out, opacity 0.3s ease-out';
        activeCard.style.transform = '';
        activeCard.style.opacity = '1';
      }
      
      // Reset state
      isSwiping = false;
      activeCard = null;
    });
  }

  /**
   * Set up swipe to close for a specific task card
   */
  private setupSwipeToClose(card: HTMLElement, windowId: string): void {
    let startX = 0;
    let currentX = 0;
    let isSwiping = false;
    
    card.addEventListener('touchstart', (e) => {
      startX = e.touches[0].clientX;
      currentX = startX;
      card.style.transition = '';
    });
    
    card.addEventListener('touchmove', (e) => {
      currentX = e.touches[0].clientX;
      const deltaX = currentX - startX;
      
      // Handle as swipe
      if (Math.abs(deltaX) > 10) {
        isSwiping = true;
        card.style.transform = `translateX(${deltaX}px)`;
        
        // Change opacity based on swipe distance
        const opacity = 1 - Math.min(Math.abs(deltaX) / 200, 0.5);
        card.style.opacity = opacity.toString();
        
        // Prevent scrolling
        e.preventDefault();
      }
    });
    
    card.addEventListener('touchend', () => {
      const deltaX = currentX - startX;
      
      // If significant swipe, close the window
      if (isSwiping && Math.abs(deltaX) > 100) {
        card.style.transition = 'transform 0.3s ease-out, opacity 0.3s ease-out';
        card.style.transform = `translateX(${deltaX > 0 ? '100%' : '-100%'})`;
        card.style.opacity = '0';
        
        // Close the window
        this.closeWindow(windowId);
        
        // Remove card after animation
        setTimeout(() => {
          card.remove();
          
          // Update task cards (check if empty)
          this.updateTaskCards();
        }, 300);
      } else {
        // Reset position
        card.style.transition = 'transform 0.3s ease-out, opacity 0.3s ease-out';
        card.style.transform = '';
        card.style.opacity = '1';
      }
      
      // Reset state
      isSwiping = false;
    });
  }
}
