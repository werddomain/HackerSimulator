import { OS } from '../core/os';
import { GuiApplication } from '../core/gui-application';
import { ErrorHandler, ErrorLevel, ErrorLogEntry } from '../core/error-handler';

/**
 * Interface for log date information
 */
interface LogDateInfo {
  date: Date;
  dayFolder: string;
  hours: string[];
  totalErrors: number;
}

/**
 * Error Log Viewer App for the Hacker Game
 * Provides a interface to view and manage system errors
 */
export class ErrorLogViewerApp extends GuiApplication {
  private errorHandler: ErrorHandler;
  private activeTab: string = 'all';
  private filterText: string = '';
  private selectedErrorId: string | null = null;
  private sortField: keyof ErrorLogEntry = 'timestamp';
  private sortDirection: 'asc' | 'desc' = 'desc';
  private isLoading: boolean = false;
  private lastLoadTime: number = 0;
  private autoRefreshInterval: number | null = null;
  private logDates: LogDateInfo[] = [];
  private selectedDate: string | null = null;
  private selectedHour: string | null = null;
  private logStats = {
    totalErrors: 0,
    unhandledErrors: 0,
    criticalErrors: 0,
    oldestLogDate: null as Date | null,
    newestLogDate: null as Date | null,
    logFileCount: 0
  };

  constructor(os: OS) {
    super(os);
    this.errorHandler = ErrorHandler.getInstance();
  }

  /**
   * Get application name for process registration
   */
  protected getApplicationName(): string {
    return 'error-log-viewer';
  }

  /**
   * Application-specific initialization
   */
  protected initApplication(): void {
    if (!this.container) return;
    
    this.render();
    this.setupEventListeners();
    this.loadLogIndex();
    
    // Set up auto-refresh (every 60 seconds)
    this.autoRefreshInterval = window.setInterval(() => {
      this.loadLogIndex(true);
    }, 60000);
  }
  
  /**
   * Clean up when application is closed
   */
  protected onClose(): void {
    // Clear auto-refresh interval
    if (this.autoRefreshInterval !== null) {
      window.clearInterval(this.autoRefreshInterval);
      this.autoRefreshInterval = null;
    }
  }

  /**
   * Render the error log viewer UI
   */
  private render(): void {
    if (!this.container) return;
    
    this.container.innerHTML = `
      <div class="error-log-viewer-app">
        <div class="error-log-header">
          <h1>Error Log Viewer</h1>
          <div class="error-log-actions">
            <div class="search-container">
              <input type="text" class="search-input" placeholder="Search errors...">
              <button class="clear-search" style="display: none;">×</button>
            </div>
            <button class="refresh-btn" title="Refresh">↻</button>
            <button class="clear-log-btn" title="Clear All Logs">🗑️</button>
          </div>
        </div>
        
        <div class="error-log-tabs">
          <div class="tab active" data-tab="all">All Logs</div>
          <div class="tab" data-tab="error">Errors</div>
          <div class="tab" data-tab="warning">Warnings</div>
          <div class="tab" data-tab="info">Info</div>
          <div class="tab" data-tab="unhandled">Unhandled</div>
        </div>
        
        <div class="error-log-container">
          <div class="error-log-sidebar">
            <div class="log-calendar">
              <h3>Log Archive</h3>
              <div class="calendar-container"></div>
            </div>
          </div>
          
          <div class="error-log-content">
            <div class="error-log-list-container">
              <table class="error-log-list">
                <thead>
                  <tr>
                    <th class="sortable" data-sort="level">Level</th>
                    <th class="sortable" data-sort="timestamp">Time</th>
                    <th class="sortable" data-sort="message">Message</th>
                    <th class="sortable" data-sort="fileName">Source</th>
                    <th class="sortable" data-sort="handled">Status</th>
                  </tr>
                </thead>
                <tbody>
                  <tr class="loading-row">
                    <td colspan="5">Loading error logs...</td>
                  </tr>
                </tbody>
              </table>
            </div>
            
            <div class="error-details-container">
              <div class="no-error-selected">
                <p>Select an error to view details</p>
              </div>
              <div class="error-details" style="display: none;">
                <h3 class="error-title">Error Details</h3>
                <div class="error-meta">
                  <div class="meta-item">
                    <span class="meta-label">Level:</span>
                    <span class="meta-value level-value"></span>
                  </div>
                  <div class="meta-item">
                    <span class="meta-label">Time:</span>
                    <span class="meta-value timestamp-value"></span>
                  </div>
                  <div class="meta-item">
                    <span class="meta-label">Source:</span>
                    <span class="meta-value source-value"></span>
                  </div>
                  <div class="meta-item">
                    <span class="meta-label">Status:</span>
                    <span class="meta-value status-value"></span>
                  </div>
                </div>
                
                <div class="error-message-container">
                  <h4>Message</h4>
                  <div class="error-message"></div>
                </div>
                
                <div class="error-stack-container">
                  <h4>Stack Trace</h4>
                  <pre class="error-stack"></pre>
                </div>
                
                <div class="error-actions">
                  <button class="mark-handled-btn">Mark as Handled</button>
                </div>
              </div>
            </div>
          </div>
        </div>
        
        <div class="error-log-status-bar">
          <div class="error-log-status">0 errors | Last updated: Never</div>
        </div>
      </div>
    `;
  }

  /**
   * Set up event listeners
   */
  private setupEventListeners(): void {
    if (!this.container) return;
    
    // Tab switching
    const tabs = this.container.querySelectorAll('.tab');
    tabs.forEach(tab => {
      tab.addEventListener('click', () => {
        this.activeTab = tab.getAttribute('data-tab') || 'all';
        this.updateActiveTab();
        this.renderErrorList();
      });
    });
    
    // Search functionality
    const searchInput = this.container.querySelector('.search-input') as HTMLInputElement;
    const clearSearchBtn = this.container.querySelector('.clear-search') as HTMLButtonElement;
    
    searchInput?.addEventListener('input', () => {
      this.filterText = searchInput.value;
      if (clearSearchBtn) {
        clearSearchBtn.style.display = this.filterText ? 'block' : 'none';
      }
      this.renderErrorList();
    });
    
    clearSearchBtn?.addEventListener('click', () => {
      if (searchInput) {
        searchInput.value = '';
        this.filterText = '';
        clearSearchBtn.style.display = 'none';
        this.renderErrorList();
      }
    });
    
    // Refresh button
    const refreshBtn = this.container.querySelector('.refresh-btn');
    refreshBtn?.addEventListener('click', () => {
      this.loadLogIndex();
    });
    
    // Clear logs button
    const clearLogBtn = this.container.querySelector('.clear-log-btn');
    clearLogBtn?.addEventListener('click', () => {
      this.showConfirmDialog('Clear Logs', 'Are you sure you want to clear all error logs?', () => {
        this.errorHandler.clearErrorLog();
        this.loadLogIndex();
        this.hideErrorDetails();
      });
    });
    
    // Sort functionality
    const sortHeaders = this.container.querySelectorAll('th.sortable');
    sortHeaders.forEach(header => {
      header.addEventListener('click', () => {
        const sortField = header.getAttribute('data-sort') as keyof ErrorLogEntry;
        if (sortField === this.sortField) {
          this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
        } else {
          this.sortField = sortField;
          this.sortDirection = 'desc';
        }
        this.renderErrorList();
      });
    });
    
    // Mark as handled button
    const markHandledBtn = this.container.querySelector('.mark-handled-btn');
    markHandledBtn?.addEventListener('click', () => {
      if (this.selectedErrorId) {
        this.errorHandler.markAsHandled(this.selectedErrorId);
        this.renderErrorList();
        this.renderErrorDetails(this.selectedErrorId);
      }
    });
  }

  /**
   * Update active tab
   */
  private updateActiveTab(): void {
    if (!this.container) return;
    
    const tabs = this.container.querySelectorAll('.tab');
    tabs.forEach(tab => {
      tab.classList.toggle('active', tab.getAttribute('data-tab') === this.activeTab);
    });
  }
  
  /**
   * Load log index and available log dates
   */
  private async loadLogIndex(silent: boolean = false): Promise<void> {
    if (this.isLoading) return;
    
    this.isLoading = true;
    
    if (!silent && this.container) {
      const tbody = this.container.querySelector('.error-log-list tbody');
      if (tbody) {
        tbody.innerHTML = `
          <tr class="loading-row">
            <td colspan="5">Loading error logs...</td>
          </tr>
        `;
      }
    }
    
    try {
      // Load error logs
      await this.errorHandler.loadErrorLog();
      
      // Get available log files
      this.scanAvailableLogDates();
      
      // Calculate log statistics
      const errors = this.errorHandler.getErrorLog();
      this.logStats.totalErrors = errors.length;
      this.logStats.unhandledErrors = errors.filter(e => !e.handled).length;
      this.logStats.criticalErrors = errors.filter(e => e.level === ErrorLevel.CRITICAL).length;
      
      if (errors.length > 0) {
        const timestamps = errors.map(e => e.timestamp);
        this.logStats.oldestLogDate = new Date(Math.min(...timestamps));
        this.logStats.newestLogDate = new Date(Math.max(...timestamps));
      } else {
        this.logStats.oldestLogDate = null;
        this.logStats.newestLogDate = null;
      }
      
      this.lastLoadTime = Date.now();
      this.isLoading = false;
      
      this.renderCalendar();
      this.renderErrorList();
      this.updateStatusBar();
    } catch (error) {
      console.error('Failed to load error logs:', error);
      this.showErrorMessage('Failed to load error logs');
      this.isLoading = false;
      
      if (this.container) {
        const tbody = this.container.querySelector('.error-log-list tbody');
        if (tbody) {
            var errorMessage = ErrorHandler.getErrorMessage(error);
          tbody.innerHTML = `
            <tr class="error-row">
              <td colspan="5">Error loading logs: ${errorMessage}</td>
            </tr>
          `;
        }
      }
    }
  }
  
  /**
   * Scan available log dates from log files
   */
  private async scanAvailableLogDates(): Promise<void> {
    // This would scan the file system for log files
    // Since we're using the ErrorHandler.loadErrorLog method to load all logs,
    // we can just process the errors we have from there
    
    const errors = this.errorHandler.getErrorLog();
    if (errors.length === 0) {
      this.logDates = [];
      return;
    }
    
    // Group errors by date
    const dateMap = new Map<string, ErrorLogEntry[]>();
    
    for (const error of errors) {
      const date = new Date(error.timestamp);
      const year = date.getFullYear();
      const month = String(date.getMonth() + 1).padStart(2, '0');
      const day = String(date.getDate()).padStart(2, '0');
      const dateKey = `${year}${month}${day}`;
      
      if (!dateMap.has(dateKey)) {
        dateMap.set(dateKey, []);
      }
      
      dateMap.get(dateKey)!.push(error);
    }
    
    // Create log date info objects
    this.logDates = [];
    
    for (const [dateKey, dateErrors] of dateMap.entries()) {
      // Group by hour
      const hourMap = new Map<string, ErrorLogEntry[]>();
      
      for (const error of dateErrors) {
        const date = new Date(error.timestamp);
        const hour = String(date.getHours()).padStart(2, '0');
        
        if (!hourMap.has(hour)) {
          hourMap.set(hour, []);
        }
        
        hourMap.get(hour)!.push(error);
      }
      
      // Convert date key to Date object
      const year = parseInt(dateKey.substring(0, 4));
      const month = parseInt(dateKey.substring(4, 6)) - 1;
      const day = parseInt(dateKey.substring(6, 8));
      
      this.logDates.push({
        date: new Date(year, month, day),
        dayFolder: `${ErrorHandler.LOG_FOLDER_ROOT}/${dateKey}`,
        hours: Array.from(hourMap.keys()).sort(),
        totalErrors: dateErrors.length
      });
    }
    
    // Sort by date (newest first)
    this.logDates.sort((a, b) => b.date.getTime() - a.date.getTime());
    
    // Set selected date to the most recent if not already set
    if (!this.selectedDate && this.logDates.length > 0) {
      const latestDate = this.logDates[0];
      this.selectedDate = this.formatDateKey(latestDate.date);
      
      // Set selected hour to the latest hour
      if (latestDate.hours.length > 0) {
        this.selectedHour = latestDate.hours[latestDate.hours.length - 1];
      }
    }
  }
  
  /**
   * Format a date as YYYYMMDD
   */
  private formatDateKey(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}${month}${day}`;
  }
  
  /**
   * Render calendar with log dates
   */
  private renderCalendar(): void {
    if (!this.container) return;
    
    const calendarContainer = this.container.querySelector('.calendar-container');
    if (!calendarContainer) return;
    
    if (this.logDates.length === 0) {
      calendarContainer.innerHTML = '<div class="no-logs">No error logs found</div>';
      return;
    }
    
    // Group dates by month
    const monthMap = new Map<string, LogDateInfo[]>();
    
    for (const logDate of this.logDates) {
      const date = logDate.date;
      const year = date.getFullYear();
      const month = date.getMonth();
      const monthKey = `${year}-${month}`;
      
      if (!monthMap.has(monthKey)) {
        monthMap.set(monthKey, []);
      }
      
      monthMap.get(monthKey)!.push(logDate);
    }
    
    let calendarHtml = '';
    
    // Create calendar for each month
    for (const [monthKey, monthDates] of monthMap.entries()) {
      const [yearStr, monthStr] = monthKey.split('-');
      const year = parseInt(yearStr);
      const month = parseInt(monthStr);
      const monthName = new Date(year, month, 1).toLocaleString('default', { month: 'long' });
      
      calendarHtml += `
        <div class="calendar-month">
          <div class="calendar-month-header">${monthName} ${year}</div>
          <div class="calendar-days">
            ${this.renderCalendarMonth(year, month, monthDates)}
          </div>
        </div>
      `;
      
      // If this is the selected month, add hour list
      const selectedDateObj = this.selectedDate ? this.getDateFromKey(this.selectedDate) : null;
      if (selectedDateObj && selectedDateObj.getFullYear() === year && selectedDateObj.getMonth() === month) {
        const selectedDateInfo = this.logDates.find(d => this.formatDateKey(d.date) === this.selectedDate);
        if (selectedDateInfo) {
          calendarHtml += this.renderHourList(selectedDateInfo);
        }
      }
    }
    
    calendarContainer.innerHTML = calendarHtml;
    
    // Add event listeners to calendar days
    const calendarDays = calendarContainer.querySelectorAll('.calendar-day');
    calendarDays.forEach(day => {
      if (!day.classList.contains('empty')) {
        day.addEventListener('click', () => {
          const dateKey = day.getAttribute('data-date');
          if (dateKey) {
            this.selectDate(dateKey);
          }
        });
      }
    });
    
    // Add event listeners to hour items
    const hourItems = calendarContainer.querySelectorAll('.hour-item');
    hourItems.forEach(hourItem => {
      hourItem.addEventListener('click', () => {
        const hour = hourItem.getAttribute('data-hour');
        if (hour) {
          this.selectHour(hour);
        }
      });
    });
  }
  
  /**
   * Render a calendar month
   */
  private renderCalendarMonth(year: number, month: number, dates: LogDateInfo[]): string {
    // Get first day of month
    const firstDay = new Date(year, month, 1);
    const startingDay = firstDay.getDay(); // 0 = Sunday, 1 = Monday, etc.
    
    // Get days in month
    const daysInMonth = new Date(year, month + 1, 0).getDate();
    
    // Create map of dates with logs
    const dateMap = new Map<number, LogDateInfo>();
    
    for (const dateInfo of dates) {
      const day = dateInfo.date.getDate();
      dateMap.set(day, dateInfo);
    }
    
    // Create calendar days
    let html = '';
    
    // Add day headers
    const dayNames = ['Su', 'Mo', 'Tu', 'We', 'Th', 'Fr', 'Sa'];
    for (const dayName of dayNames) {
      html += `<div class="calendar-day-header">${dayName}</div>`;
    }
    
    // Add empty cells before first day
    for (let i = 0; i < startingDay; i++) {
      html += '<div class="calendar-day empty"></div>';
    }
    
    // Add days of month
    for (let day = 1; day <= daysInMonth; day++) {
      const dateInfo = dateMap.get(day);
      const dateKey = this.formatDateKey(new Date(year, month, day));
      const hasLogs = !!dateInfo;
      const isActive = dateKey === this.selectedDate;
      
      let classes = 'calendar-day';
      if (hasLogs) classes += ' has-logs';
      if (isActive) classes += ' active';
      
      html += `<div class="${classes}" data-date="${dateKey}">${day}</div>`;
    }
    
    // Add empty cells after last day to complete the grid
    const totalCells = Math.ceil((startingDay + daysInMonth) / 7) * 7;
    for (let i = startingDay + daysInMonth; i < totalCells; i++) {
      html += '<div class="calendar-day empty"></div>';
    }
    
    return html;
  }
  
  /**
   * Render hour list for a selected date
   */
  private renderHourList(dateInfo: LogDateInfo): string {
    let html = '<div class="hour-list">';
    
    if (dateInfo.hours.length === 0) {
      html += '<div class="no-hours">No hourly logs for this date</div>';
    } else {
      for (let i = 0; i < 24; i++) {
        const hour = String(i).padStart(2, '0');
        const hasLogs = dateInfo.hours.includes(hour);
        
        if (hasLogs) {
          const isActive = hour === this.selectedHour;
          const hourLabel = this.formatHourLabel(i);
          
          html += `
            <div class="hour-item ${isActive ? 'active' : ''}" data-hour="${hour}">
              <span class="hour-label">${hourLabel}</span>
              <span class="error-count">Logs</span>
            </div>
          `;
        }
      }
    }
    
    html += '</div>';
    return html;
  }
  
  /**
   * Format hour label (e.g., "2 PM")
   */
  private formatHourLabel(hour: number): string {
    const isPM = hour >= 12;
    const hour12 = hour === 0 ? 12 : hour > 12 ? hour - 12 : hour;
    return `${hour12} ${isPM ? 'PM' : 'AM'}`;
  }
  
  /**
   * Get Date object from date key (YYYYMMDD)
   */
  private getDateFromKey(dateKey: string): Date {
    const year = parseInt(dateKey.substring(0, 4));
    const month = parseInt(dateKey.substring(4, 6)) - 1;
    const day = parseInt(dateKey.substring(6, 8));
    return new Date(year, month, day);
  }
  
  /**
   * Select a date in the calendar
   */
  private selectDate(dateKey: string): void {
    if (this.selectedDate === dateKey) return;
    
    this.selectedDate = dateKey;
    this.selectedHour = null; // Reset selected hour
    
    // Re-render calendar to update selection
    this.renderCalendar();
    
    // Re-render error list with new date filter
    this.renderErrorList();
  }
  
  /**
   * Select an hour in the hour list
   */
  private selectHour(hour: string): void {
    if (this.selectedHour === hour) return;
    
    this.selectedHour = hour;
    
    // Re-render calendar to update hour selection
    this.renderCalendar();
    
    // Re-render error list with new hour filter
    this.renderErrorList();
  }
  
  /**
   * Update the status bar with log statistics
   */
  private updateStatusBar(): void {
    if (!this.container) return;
    
    const statusBar = this.container.querySelector('.error-log-status');
    if (!statusBar) return;
    
    const lastUpdated = new Date(this.lastLoadTime).toLocaleTimeString();
    
    let statusText = `${this.logStats.totalErrors} errors`;
    if (this.logStats.unhandledErrors > 0) {
      statusText += ` (${this.logStats.unhandledErrors} unhandled)`;
    }
    if (this.logStats.criticalErrors > 0) {
      statusText += ` | ${this.logStats.criticalErrors} critical`;
    }
    statusText += ` | Last updated: ${lastUpdated}`;
    
    if (this.selectedDate) {
      const date = this.getDateFromKey(this.selectedDate);
      const dateStr = date.toLocaleDateString();
      statusText += ` | Viewing: ${dateStr}`;
      
      if (this.selectedHour) {
        statusText += ` ${this.formatHourLabel(parseInt(this.selectedHour))}`;
      }
    }
    
    statusBar.textContent = statusText;
  }

  /**
   * Filter and sort errors based on active tab, search text, and selected date/hour
   */
  private getFilteredErrors(): ErrorLogEntry[] {
    let errors = this.errorHandler.getErrorLog();
    
    // Apply date filter
    if (this.selectedDate) {
      const dateStart = this.getDateFromKey(this.selectedDate);
      dateStart.setHours(0, 0, 0, 0);
      
      const dateEnd = new Date(dateStart);
      dateEnd.setHours(23, 59, 59, 999);
      
      errors = errors.filter(error => {
        const errorDate = new Date(error.timestamp);
        return errorDate >= dateStart && errorDate <= dateEnd;
      });
      
      // Apply hour filter
      if (this.selectedHour) {
        const hour = parseInt(this.selectedHour);
        errors = errors.filter(error => {
          const errorDate = new Date(error.timestamp);
          return errorDate.getHours() === hour;
        });
      }
    }
    
    // Apply tab filter
    if (this.activeTab !== 'all') {
      if (this.activeTab === 'error') {
        errors = errors.filter(error => error.level === ErrorLevel.ERROR || error.level === ErrorLevel.CRITICAL);
      } else if (this.activeTab === 'warning') {
        errors = errors.filter(error => error.level === ErrorLevel.WARNING);
      } else if (this.activeTab === 'info') {
        errors = errors.filter(error => error.level === ErrorLevel.INFO);
      } else if (this.activeTab === 'unhandled') {
        errors = errors.filter(error => !error.handled);
      }
    }
    
    // Apply text filter
    if (this.filterText) {
      const lowerFilter = this.filterText.toLowerCase();
      errors = errors.filter(error => 
        error.message.toLowerCase().includes(lowerFilter) ||
        error.fileName.toLowerCase().includes(lowerFilter) ||
        error.level.toLowerCase().includes(lowerFilter)
      );
    }
    
    // Apply sorting
    errors.sort((a, b) => {
      let valueA = a[this.sortField];
      let valueB = b[this.sortField];
       // Handle null/undefined values first
    if (valueA === null || valueA === undefined) {
      return this.sortDirection === 'asc' ? 1 : -1;
    }
    if (valueB === null || valueB === undefined) {
      return this.sortDirection === 'asc' ? -1 : 1;
    }
    
    // Special case for timestamps (convert to numbers for comparison)
    if (this.sortField === 'timestamp') {
      const timeA = typeof valueA === 'number' ? valueA : new Date(<string|Date>valueA).getTime();
      const timeB = typeof valueB === 'number' ? valueB : new Date(<string|Date>valueB).getTime();
      return this.sortDirection === 'asc' ? timeA - timeB : timeB - timeA;
    }
    
    // Handle different types
    if (typeof valueA === 'number' && typeof valueB === 'number') {
      return this.sortDirection === 'asc' ? valueA - valueB : valueB - valueA;
    } 
    else if (typeof valueA === 'string' && typeof valueB === 'string') {
      return this.sortDirection === 'asc' 
        ? valueA.toLowerCase().localeCompare(valueB.toLowerCase())
        : valueB.toLowerCase().localeCompare(valueA.toLowerCase());
    } 
    else if (typeof valueA === 'boolean' && typeof valueB === 'boolean') {
      const boolCompare = valueA === valueB ? 0 : valueA ? 1 : -1;
      return this.sortDirection === 'asc' ? boolCompare : -boolCompare;
    } 
    else {
      // Convert to strings for any other types
      const strA = String(valueA).toLowerCase();
      const strB = String(valueB).toLowerCase();
      return this.sortDirection === 'asc' 
        ? strA.localeCompare(strB)
        : strB.localeCompare(strA);
    }
    });
    
    return errors;
  }

  /**
   * Render the error list based on current filters
   */
  private renderErrorList(): void {
    if (!this.container) return;
    
    const tbody = this.container.querySelector('.error-log-list tbody');
    if (!tbody) return;
    
    // Get filtered and sorted errors
    const errors = this.getFilteredErrors();
    
    if (errors.length === 0) {
      tbody.innerHTML = `
        <tr class="empty-row">
          <td colspan="5">No errors found</td>
        </tr>
      `;
      return;
    }
    
    // Build error list HTML
    const errorRows = errors.map(error => {
      const levelClass = `level-${error.level}`;
      const statusClass = error.handled ? 'status-handled' : 'status-unhandled';
      const selected = error.id === this.selectedErrorId ? 'selected' : '';
      
      const timestamp = new Date(error.timestamp).toLocaleString();
      const sourceName = error.fileName.split('/').pop() || error.fileName;
      
      return `
        <tr class="error-row ${levelClass} ${statusClass} ${selected}" data-error-id="${error.id}">
          <td class="level-cell"><span class="level-indicator">${this.getLevelIcon(error.level)}</span> ${error.level}</td>
          <td class="timestamp-cell">${timestamp}</td>
          <td class="message-cell">${this.truncateText(error.message, 70)}</td>
          <td class="source-cell">${sourceName}</td>
          <td class="status-cell">${error.handled ? 'Handled' : 'Unhandled'}</td>
        </tr>
      `;
    }).join('');
    
    tbody.innerHTML = errorRows;
    
    // Add click event to rows
    const rows = tbody.querySelectorAll('.error-row');
    rows.forEach(row => {
      row.addEventListener('click', () => {
        const errorId = row.getAttribute('data-error-id');
        if (errorId) {
          this.selectError(errorId);
        }
      });
    });
  }

  /**
   * Select an error and show its details
   */
  private selectError(errorId: string): void {
    if (!this.container) return;
    
    // Update selected row
    const rows = this.container.querySelectorAll('.error-row');
    rows.forEach(row => {
      row.classList.toggle('selected', row.getAttribute('data-error-id') === errorId);
    });
    
    this.selectedErrorId = errorId;
    this.renderErrorDetails(errorId);
  }

  /**
   * Render error details
   */
  private renderErrorDetails(errorId: string): void {
    if (!this.container) return;
    
    const errorDetails = this.container.querySelector('.error-details') as HTMLElement;
    const noErrorSelected = this.container.querySelector('.no-error-selected') as HTMLElement;
    
    // Find the error in the log
    const error = this.errorHandler.getErrorLog().find(e => e.id === errorId);
    
    if (!error) {
      this.hideErrorDetails();
      return;
    }
    
    // Show error details container
    if (errorDetails) errorDetails.style.display = 'block';
    if (noErrorSelected) noErrorSelected.style.display = 'none';
    
    // Fill in error details
    const levelElement = this.container.querySelector('.level-value');
    const timestampElement = this.container.querySelector('.timestamp-value');
    const sourceElement = this.container.querySelector('.source-value');
    const statusElement = this.container.querySelector('.status-value');
    const messageElement = this.container.querySelector('.error-message');
    const stackElement = this.container.querySelector('.error-stack');
    const markHandledBtn = this.container.querySelector('.mark-handled-btn') as HTMLButtonElement;
    
    if (levelElement) {
      levelElement.className = `meta-value level-value level-${error.level}`;
      levelElement.textContent = error.level;
    }
    
    if (timestampElement) {
      timestampElement.textContent = new Date(error.timestamp).toLocaleString();
    }
    
    if (sourceElement) {
      const source = error.fileName;
      if (error.lineNumber) {
        sourceElement.textContent = `${source} (Line ${error.lineNumber}${error.columnNumber ? `, Col ${error.columnNumber}` : ''})`;
      } else {
        sourceElement.textContent = source;
      }
    }
    
    if (statusElement) {
      statusElement.className = `meta-value status-value status-${error.handled ? 'handled' : 'unhandled'}`;
      statusElement.textContent = error.handled ? 'Handled' : 'Unhandled';
    }
    
    if (messageElement) {
      messageElement.textContent = error.message;
    }
    
    if (stackElement) {
      stackElement.textContent = error.stack || 'No stack trace available';
    }
    
    // Update mark as handled button
    if (markHandledBtn) {
      if (error.handled) {
        markHandledBtn.textContent = 'Already Handled';
        markHandledBtn.disabled = true;
      } else {
        markHandledBtn.textContent = 'Mark as Handled';
        markHandledBtn.disabled = false;
      }
    }
  }

  /**
   * Hide error details
   */
  private hideErrorDetails(): void {
    if (!this.container) return;
    
    const errorDetails = this.container.querySelector('.error-details') as HTMLElement;
    const noErrorSelected = this.container.querySelector('.no-error-selected') as HTMLElement;
    
    if (errorDetails) errorDetails.style.display = 'none';
    if (noErrorSelected) noErrorSelected.style.display = 'block';
    
    this.selectedErrorId = null;
  }

  /**
   * Get level icon
   */
  private getLevelIcon(level: ErrorLevel): string {
    switch (level) {
      case ErrorLevel.INFO:
        return 'ℹ️';
      case ErrorLevel.WARNING:
        return '⚠️';
      case ErrorLevel.ERROR:
        return '❌';
      case ErrorLevel.CRITICAL:
        return '🔥';
      default:
        return '⚠️';
    }
  }

  /**
   * Truncate text to a maximum length
   */
  private truncateText(text: string, maxLength: number): string {
    if (text.length <= maxLength) {
      return text;
    }
    return text.substring(0, maxLength) + '...';
  }

  /**
   * Show error message in a dialog
   */
  private showErrorMessage(message: string): void {
    this.dialogManager.Msgbox.Show('Error', message, ['ok'], 'error');
  }

  /**
   * Show confirmation dialog
   */
  private showConfirmDialog(title: string, message: string, onConfirm: () => void): void {
    this.dialogManager.Msgbox.Show(title, message, ['yes', 'no'])
      .then(result => {
        if (result === 'yes') {
          onConfirm();
        }
      });
  }
}
