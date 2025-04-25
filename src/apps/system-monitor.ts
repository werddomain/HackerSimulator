import { OS } from '../core/os';
import { Process } from '../core/process';
import { GuiApplication } from '../core/gui-application';
import { PerformanceMonitor, PerformanceMetrics } from '../core/performance-monitor';

/**
 * System Monitor App for the Hacker Game
 * Provides visualization of system resources and processes
 */
export class SystemMonitorApp extends GuiApplication {
  private updateInterval: number | null = null;
  private chartData: {
    cpu: number[];
    ram: number[];
    network: number[];
    disk: number[];
    fps: number[];
    frameTime: number[];
    timestamps: number[];
  } = {
    cpu: [],
    ram: [],
    network: [],
    disk: [],
    fps: [],
    frameTime: [],
    timestamps: []
  };
  
  // Track active tab
  private activeTab: 'overview' | 'processes' | 'performance' | 'disk' | 'network' | 'rendering' = 'overview';
  
  // Track process details view
  private selectedProcessPid: number | null = null;
  // Maximum data points to keep in memory for the charts
  private readonly MAX_DATA_POINTS = 60;
  
  // Simulate network and disk metrics (since they're not in the original system)
  private networkUsage = 0;
  private diskUsage = 60; // Start at 60% usage
  private diskTotal = 500; // 500 GB simulated capacity
  private diskFree = 200;  // 200 GB simulated free space
  
  // Performance monitoring
  private performanceMonitor: PerformanceMonitor | null = null;
  private performanceMetrics: PerformanceMetrics = {
    fps: 0,
    frameTime: 0,
    averageFrameTime: 0,
    minFrameTime: 0,
    maxFrameTime: 0,
    jank: 0,
    lastUpdate: 0
  };
  
  constructor(os: OS) {
    super(os);
  }
  
  /**
   * Get application name for process registration
   */
  protected getApplicationName(): string {
    return 'system-monitor';
  }
    /**
   * Application-specific initialization
   */
  protected initApplication(): void {
    if (!this.container) return;
    
    // Initialize performance monitoring
    this.performanceMonitor = new PerformanceMonitor({
      targetFps: 60,
      sampleSize: this.MAX_DATA_POINTS,
      criticalFrameTime: 16.67 // 60fps = 16.67ms per frame
    });
    
    // Set up metrics update callback
    this.performanceMonitor.onUpdate((metrics) => {
      this.performanceMetrics = metrics;
      
      // Add metrics to chart data
      this.chartData.fps.push(metrics.fps);
      this.chartData.frameTime.push(metrics.frameTime);
      
      // Keep only the last MAX_DATA_POINTS
      if (this.chartData.fps.length > this.MAX_DATA_POINTS) {
        this.chartData.fps.shift();
        this.chartData.frameTime.shift();
      }
      
      // Update rendering tab if active
      if (this.activeTab === 'rendering') {
        this.updateRenderingTab();
      }
    });
    
    // Start performance monitoring
    this.performanceMonitor.start();
    
    this.render();
    
    // Start update interval
    this.updateInterval = window.setInterval(() => this.updateData(), 1000);
    
    // Initial update
    this.updateData();
  }
  
  /**
   * Render the system monitor UI
   */
  private render(): void {
    if (!this.container) return;
    
    this.container.innerHTML = `
      <div class="system-monitor-app">
        <div class="system-monitor-header">
          <h2>System Monitor</h2>
          <div class="header-actions">
            <button class="refresh-btn">Refresh</button>
          </div>
        </div>
        
        <div class="tabs-container">
          <div class="tab-buttons">
            <button class="tab-btn ${this.activeTab === 'overview' ? 'active' : ''}" data-tab="overview">Overview</button>
            <button class="tab-btn ${this.activeTab === 'processes' ? 'active' : ''}" data-tab="processes">Processes</button>
            <button class="tab-btn ${this.activeTab === 'performance' ? 'active' : ''}" data-tab="performance">Performance</button>
            <button class="tab-btn ${this.activeTab === 'disk' ? 'active' : ''}" data-tab="disk">Disk</button>
            <button class="tab-btn ${this.activeTab === 'network' ? 'active' : ''}" data-tab="network">Network</button>
          </div>
          
          <!-- Overview Tab -->
          <div class="tab-content ${this.activeTab === 'overview' ? 'active' : ''}" id="tab-overview">
            <div class="resource-overview">
              <div class="resource-card">
                <div class="resource-title">CPU Usage</div>
                <div class="resource-value" id="cpu-value">0%</div>
                <div class="resource-bar-container">
                  <div class="resource-bar" id="cpu-bar" style="width: 0%;"></div>
                </div>
              </div>
              
              <div class="resource-card">
                <div class="resource-title">Memory Usage</div>
                <div class="resource-value" id="ram-value">0%</div>
                <div class="resource-bar-container">
                  <div class="resource-bar" id="ram-bar" style="width: 0%;"></div>
                </div>
              </div>
              
              <div class="resource-card">
                <div class="resource-title">Disk Usage</div>
                <div class="resource-value" id="disk-value">0%</div>
                <div class="resource-bar-container">
                  <div class="resource-bar" id="disk-bar" style="width: 0%;"></div>
                </div>
              </div>
              
              <div class="resource-card">
                <div class="resource-title">Network Usage</div>
                <div class="resource-value" id="network-value">0 MB/s</div>
                <div class="resource-bar-container">
                  <div class="resource-bar" id="network-bar" style="width: 0%;"></div>
                </div>
              </div>
            </div>
            
            <div class="system-info-container">
              <div class="system-info-card">
                <h3>System Information</h3>
                <table class="system-info-table">
                  <tr>
                    <td class="info-label">OS:</td>
                    <td class="info-value">HackerOS 2.0</td>
                  </tr>
                  <tr>
                    <td class="info-label">Kernel:</td>
                    <td class="info-value">4.18.0-kali2-amd64</td>
                  </tr>
                  <tr>
                    <td class="info-label">Uptime:</td>
                    <td class="info-value" id="system-uptime">00:00:00</td>
                  </tr>
                  <tr>
                    <td class="info-label">Hostname:</td>
                    <td class="info-value">hacker-machine</td>
                  </tr>
                  <tr>
                    <td class="info-label">User:</td>
                    <td class="info-value">user</td>
                  </tr>
                </table>
              </div>
              
              <div class="system-info-card">
                <h3>Hardware Information</h3>
                <table class="system-info-table">
                  <tr>
                    <td class="info-label">CPU:</td>
                    <td class="info-value">AMD Ryzen 7 5800X (8 cores, 16 threads)</td>
                  </tr>
                  <tr>
                    <td class="info-label">Memory:</td>
                    <td class="info-value">16 GB DDR4</td>
                  </tr>
                  <tr>
                    <td class="info-label">Disk:</td>
                    <td class="info-value" id="disk-free-space">Loading...</td>
                  </tr>
                  <tr>
                    <td class="info-label">Network:</td>
                    <td class="info-value">Ethernet, 1000 Mbps</td>
                  </tr>
                </table>
              </div>
            </div>
          </div>
          
          <!-- Processes Tab -->
          <div class="tab-content ${this.activeTab === 'processes' ? 'active' : ''}" id="tab-processes">
            ${this.selectedProcessPid === null ? `
              <div class="process-header">
                <h3>Processes</h3>
                <div class="process-actions">
                  <button class="process-action end-process">End Process</button>
                  <input type="text" class="process-filter" placeholder="Filter processes...">
                </div>
              </div>
              
              <div class="process-table-container">
                <table class="process-table">
                  <thead>
                    <tr>
                      <th class="col-select"><input type="checkbox" id="select-all-processes"></th>
                      <th class="col-pid">PID</th>
                      <th class="col-name">Name</th>
                      <th class="col-user">User</th>
                      <th class="col-cpu">CPU %</th>
                      <th class="col-memory">Memory %</th>
                      <th class="col-status">Status</th>
                      <th class="col-time">Time</th>
                    </tr>
                  </thead>
                  <tbody id="process-list">
                    <!-- Process list will be populated here -->
                  </tbody>
                </table>
              </div>
            ` : `
              <!-- Process Details View -->
              <div class="process-details" id="process-details">
                <div class="details-header">
                  <button class="back-to-processes">← Back to processes</button>
                </div>
                <div class="details-content" id="process-details-content">
                  <!-- Process details will be populated here -->
                </div>
              </div>
            `}
          </div>
          
          <!-- Performance Tab -->
          <div class="tab-content ${this.activeTab === 'performance' ? 'active' : ''}" id="tab-performance">
            <div class="charts-container">
              <div class="chart-card">
                <div class="chart-title">CPU History</div>
                <canvas id="cpu-chart" height="150"></canvas>
              </div>
              
              <div class="chart-card">
                <div class="chart-title">Memory History</div>
                <canvas id="ram-chart" height="150"></canvas>
              </div>
              
              <div class="chart-card">
                <div class="chart-title">Disk I/O History</div>
                <canvas id="disk-chart" height="150"></canvas>
              </div>
              
              <div class="chart-card">
                <div class="chart-title">Network History</div>
                <canvas id="network-chart" height="150"></canvas>
              </div>
            </div>
          </div>
          
          <!-- Disk Tab -->
          <div class="tab-content ${this.activeTab === 'disk' ? 'active' : ''}" id="tab-disk">
            <div class="disk-usage-container">
              <div class="disk-overview">
                <h3>Disk Space</h3>
                <div class="disk-chart-container">
                  <div class="disk-chart">
                    <div class="disk-usage-indicator" id="disk-usage-indicator" style="transform: rotate(0deg)"></div>
                  </div>
                  <div class="disk-info" id="disk-info">
                    <div class="disk-free">Free: <span id="disk-free">0 GB</span></div>
                    <div class="disk-total">Total: <span id="disk-total">0 GB</span></div>
                  </div>
                </div>
              </div>
              
              <div class="disk-activity">
                <h3>Disk Activity</h3>
                <table class="disk-activity-table">
                  <thead>
                    <tr>
                      <th>Process</th>
                      <th>Read (KB/s)</th>
                      <th>Write (KB/s)</th>
                    </tr>
                  </thead>
                  <tbody id="disk-activity-list">
                    <!-- Generated dynamically -->
                  </tbody>
                </table>
              </div>
            </div>
          </div>
          
          <!-- Network Tab -->
          <div class="tab-content ${this.activeTab === 'network' ? 'active' : ''}" id="tab-network">
            <div class="network-overview">
              <div class="chart-card" style="flex: 1;">
                <div class="chart-title">Network Traffic</div>
                <canvas id="network-traffic-chart" height="200"></canvas>
              </div>
              
              <div class="network-stats">
                <div class="stats-card">
                  <h3>Current Activity</h3>
                  <div class="stat-row">
                    <div class="stat-label">Sending:</div>
                    <div class="stat-value" id="network-send">0 KB/s</div>
                  </div>
                  <div class="stat-row">
                    <div class="stat-label">Receiving:</div>
                    <div class="stat-value" id="network-receive">0 KB/s</div>
                  </div>
                  <div class="stat-row">
                    <div class="stat-label">Active Connections:</div>
                    <div class="stat-value" id="network-connections">0</div>
                  </div>
                </div>
                
                <div class="stats-card">
                  <h3>Network Interfaces</h3>
                  <div class="network-interface">
                    <div class="interface-name">eth0 (Ethernet)</div>
                    <div class="interface-ip">192.168.1.100</div>
                    <div class="interface-mac">00:11:22:33:44:55</div>
                  </div>
                </div>
              </div>
            </div>
            
            <div class="network-connections-container">
              <h3>Network Connections</h3>
              <table class="network-connections-table">
                <thead>
                  <tr>
                    <th>Process</th>
                    <th>Protocol</th>
                    <th>Local Address</th>
                    <th>Remote Address</th>
                    <th>State</th>
                  </tr>
                </thead>
                <tbody id="network-connections-list">
                  <!-- Generated dynamically -->
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>
      
      <style>
        .system-monitor-app {
          display: flex;
          flex-direction: column;
          height: 100%;
          background-color: #1e1e1e;
          color: #d4d4d4;
          font-family: 'Segoe UI', Arial, sans-serif;
          padding: 10px;
          box-sizing: border-box;
        }
        
        .system-monitor-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin-bottom: 15px;
        }
        
        .system-monitor-header h2 {
          margin: 0;
          font-size: 18px;
        }
        
        .refresh-btn {
          background-color: #0e639c;
          color: white;
          border: none;
          padding: 5px 10px;
          border-radius: 3px;
          cursor: pointer;
        }
        
        .refresh-btn:hover {
          background-color: #1177bb;
        }
        
        /* Tab styles */
        .tabs-container {
          display: flex;
          flex-direction: column;
          flex: 1;
          overflow: hidden;
        }
        
        .tab-buttons {
          display: flex;
          background-color: #252526;
          border-top-left-radius: 5px;
          border-top-right-radius: 5px;
          overflow: hidden;
        }
        
        .tab-btn {
          padding: 10px 15px;
          background: none;
          border: none;
          color: #d4d4d4;
          cursor: pointer;
          border-bottom: 2px solid transparent;
          font-size: 14px;
        }
        
        .tab-btn:hover {
          background-color: #2a2d2e;
        }
        
        .tab-btn.active {
          background-color: #37373d;
          border-bottom: 2px solid #0e639c;
        }
        
        .tab-content {
          display: none;
          flex: 1;
          overflow: auto;
          background-color: #252526;
          border-bottom-left-radius: 5px;
          border-bottom-right-radius: 5px;
          padding: 15px;
        }
        
        .tab-content.active {
          display: flex;
          flex-direction: column;
        }
        
        /* Resource cards */
        .resource-overview {
          display: flex;
          flex-wrap: wrap;
          gap: 15px;
          margin-bottom: 20px;
        }
        
        .resource-card {
          flex: 1;
          min-width: 200px;
          background-color: #2d2d2d;
          border-radius: 5px;
          padding: 15px;
        }
        
        .resource-title {
          font-size: 14px;
          margin-bottom: 10px;
        }
        
        .resource-value {
          font-size: 24px;
          font-weight: bold;
          margin-bottom: 10px;
          color: #d7d7d7;
        }
        
        .resource-bar-container {
          width: 100%;
          height: 10px;
          background-color: #333;
          border-radius: 5px;
          overflow: hidden;
        }
        
        .resource-bar {
          height: 100%;
          background-color: #0e639c;
          transition: width 0.3s ease;
        }
        
        /* System info */
        .system-info-container {
          display: flex;
          flex-wrap: wrap;
          gap: 15px;
        }
        
        .system-info-card {
          flex: 1;
          min-width: 300px;
          background-color: #2d2d2d;
          border-radius: 5px;
          padding: 15px;
        }
        
        .system-info-card h3 {
          margin-top: 0;
          margin-bottom: 15px;
          font-size: 16px;
        }
        
        .system-info-table {
          width: 100%;
          border-collapse: collapse;
        }
        
        .system-info-table td {
          padding: 6px 0;
          border-bottom: 1px solid #333;
        }
        
        .info-label {
          font-weight: bold;
          width: 120px;
          color: #a0a0a0;
        }
        
        /* Charts */
        .charts-container {
          display: flex;
          flex-wrap: wrap;
          gap: 15px;
        }
        
        .chart-card {
          flex: 1 1 45%;
          min-width: 300px;
          background-color: #2d2d2d;
          border-radius: 5px;
          padding: 15px;
          margin-bottom: 15px;
        }
        
        .chart-title {
          font-size: 14px;
          margin-bottom: 15px;
        }
        
        canvas {
          width: 100%;
        }
        
        /* Process styles */
        .process-header {
          display: flex;
          justify-content: space-between;
          align-items: center;
          margin-bottom: 15px;
        }
        
        .process-header h3 {
          margin: 0;
          font-size: 16px;
        }
        
        .process-actions {
          display: flex;
          gap: 10px;
        }
        
        .process-action {
          background-color: #3c3c3c;
          color: #d4d4d4;
          border: none;
          padding: 5px 10px;
          border-radius: 3px;
          cursor: pointer;
        }
        
        .end-process {
          background-color: #b03c3c;
        }
        
        .end-process:hover {
          background-color: #cc4444;
        }
        
        .process-filter {
          background-color: #3c3c3c;
          border: 1px solid #555;
          color: #d4d4d4;
          padding: 5px 10px;
          border-radius: 3px;
        }
        
        .process-table-container {
          flex: 1;
          overflow: auto;
        }
        
        .process-table {
          width: 100%;
          border-collapse: collapse;
          font-size: 12px;
        }
        
        .process-table th, .process-table td {
          padding: 8px 10px;
          text-align: left;
          border-bottom: 1px solid #333;
        }
        
        .process-table th {
          background-color: #2d2d2d;
          position: sticky;
          top: 0;
          z-index: 1;
        }
        
        .process-table tbody tr:hover {
          background-color: #2a2d2e;
        }
        
        .process-table .col-select {
          width: 30px;
        }
        
        .process-table .col-pid {
          width: 60px;
        }
        
        .process-table .col-cpu, .process-table .col-memory {
          width: 70px;
        }
        
        .process-table .col-status {
          width: 90px;
        }
        
        .selected-process {
          background-color: #094771 !important;
        }
        
        .process-status-running {
          color: #3eb83e;
        }
        
        .process-status-sleeping {
          color: #cccc00;
        }
        
        .process-status-stopped {
          color: #cc4444;
        }
        
        /* Process details view */
        .process-details {
          flex: 1;
          display: flex;
          flex-direction: column;
        }
        
        .details-header {
          display: flex;
          margin-bottom: 15px;
        }
        
        .back-to-processes {
          background-color: #3c3c3c;
          color: #d4d4d4;
          border: none;
          padding: 5px 10px;
          border-radius: 3px;
          cursor: pointer;
        }
        
        .details-content {
          flex: 1;
          display: flex;
          flex-direction: column;
          gap: 15px;
        }
        
        /* Disk styles */
        .disk-usage-container {
          display: flex;
          flex-direction: column;
          gap: 20px;
        }
        
        .disk-overview {
          display: flex;
          flex-direction: column;
          background-color: #2d2d2d;
          border-radius: 5px;
          padding: 15px;
        }
        
        .disk-overview h3 {
          margin-top: 0;
          margin-bottom: 15px;
          font-size: 16px;
        }
        
        .disk-chart-container {
          display: flex;
          align-items: center;
          gap: 20px;
        }
        
        .disk-chart {
          position: relative;
          width: 150px;
          height: 150px;
          border-radius: 50%;
          background: conic-gradient(#0e639c 0% var(--usage), #333 var(--usage) 100%);
          --usage: 0%;
        }
        
        .disk-chart::before {
          content: '';
          position: absolute;
          top: 50%;
          left: 50%;
          transform: translate(-50%, -50%);
          width: 110px;
          height: 110px;
          background-color: #2d2d2d;
          border-radius: 50%;
        }
        
        .disk-usage-indicator {
          position: absolute;
          top: 0;
          left: 50%;
          width: 3px;
          height: 75px;
          background-color: #d4d4d4;
          transform-origin: bottom center;
        }
        
        .disk-info {
          flex: 1;
        }
        
        .disk-free, .disk-total {
          font-size: 16px;
          margin-bottom: 10px;
        }
        
        .disk-activity {
          background-color: #2d2d2d;
          border-radius: 5px;
          padding: 15px;
        }
        
        .disk-activity h3 {
          margin-top: 0;
          margin-bottom: 15px;
          font-size: 16px;
        }
        
        .disk-activity-table {
          width: 100%;
          border-collapse: collapse;
          font-size: 12px;
        }
        
        .disk-activity-table th, .disk-activity-table td {
          padding: 8px 10px;
          text-align: left;
          border-bottom: 1px solid #333;
        }
        
        .disk-activity-table th {
          background-color: #333;
        }
        
        /* Network styles */
        .network-overview {
          display: flex;
          flex-wrap: wrap;
          gap: 15px;
          margin-bottom: 20px;
        }
        
        .network-stats {
          flex: 1;
          min-width: 250px;
          display: flex;
          flex-direction: column;
          gap: 15px;
        }
        
        .stats-card {
          background-color: #2d2d2d;
          border-radius: 5px;
          padding: 15px;
        }
        
        .stats-card h3 {
          margin-top: 0;
          margin-bottom: 15px;
          font-size: 16px;
        }
        
        .stat-row {
          display: flex;
          justify-content: space-between;
          margin-bottom: 8px;
        }
        
        .network-interface {
          margin-bottom: 10px;
          padding-bottom: 10px;
          border-bottom: 1px solid #333;
        }
        
        .interface-name {
          font-weight: bold;
          margin-bottom: 5px;
        }
        
        .interface-ip, .interface-mac {
          color: #a0a0a0;
          font-size: 12px;
          margin-bottom: 2px;
        }
        
        .network-connections-container {
          background-color: #2d2d2d;
          border-radius: 5px;
          padding: 15px;
        }
        
        .network-connections-container h3 {
          margin-top: 0;
          margin-bottom: 15px;
          font-size: 16px;
        }
        
        .network-connections-table {
          width: 100%;
          border-collapse: collapse;
          font-size: 12px;
        }
        
        .network-connections-table th, .network-connections-table td {
          padding: 8px 10px;
          text-align: left;
          border-bottom: 1px solid #333;
        }
        
        .network-connections-table th {
          background-color: #333;
        }
      </style>
    `;
    
    // Set up event listeners
    this.setupEventListeners();
  }
  
  /**
   * Set up event listeners
   */
  private setupEventListeners(): void {
    if (!this.container) return;
    
    // Tab buttons
    const tabButtons = this.container.querySelectorAll('.tab-btn');
    tabButtons.forEach(btn => {
      btn.addEventListener('click', () => {
        const tab = btn.getAttribute('data-tab') as 'overview' | 'processes' | 'performance' | 'disk' | 'network';
        if (tab) {
          this.switchTab(tab);
        }
      });
    });
    
    // Refresh button
    const refreshBtn = this.container.querySelector('.refresh-btn');
    refreshBtn?.addEventListener('click', () => this.refresh());
    
    // Process list events
    if (this.activeTab === 'processes' && this.selectedProcessPid === null) {
      // End process button
      const endProcessBtn = this.container.querySelector('.end-process');
      endProcessBtn?.addEventListener('click', () => this.endSelectedProcesses());
      
      // Select all checkbox
      const selectAllCheckbox = this.container.querySelector('#select-all-processes');
      selectAllCheckbox?.addEventListener('change', (e) => {
        const target = e.target as HTMLInputElement;
        this.selectAllProcesses(target.checked);
      });
      
      // Process filter
      const filterInput = this.container.querySelector('.process-filter');
      filterInput?.addEventListener('input', (e) => {
        const target = e.target as HTMLInputElement;
        this.filterProcesses(target.value);
      });
    }
    
    // Process details view back button
    if (this.activeTab === 'processes' && this.selectedProcessPid !== null) {
      const backBtn = this.container.querySelector('.back-to-processes');
      backBtn?.addEventListener('click', () => {
        this.selectedProcessPid = null;
        this.render();
      });
    }
    
    // Process row double-click for details
    const processRows = this.container.querySelectorAll('.process-row');
    processRows.forEach(row => {
      row.addEventListener('dblclick', () => {
        const pid = parseInt(row.getAttribute('data-pid') || '0');
        if (pid > 0) {
          this.showProcessDetails(pid);
        }
      });
    });
  }
  
  /**
   * Switch active tab
   */
  private switchTab(tab: 'overview' | 'processes' | 'performance' | 'disk' | 'network'): void {
    this.activeTab = tab;
    this.render();
  }

  /**
   * Show process details view
   */
  private showProcessDetails(pid: number): void {
    this.selectedProcessPid = pid;
    this.render();
    this.updateProcessDetailsView();
  }

  /**
   * Update process details view
   */
  private updateProcessDetailsView(): void {
    if (!this.container || this.selectedProcessPid === null) return;
    
    const process = this.os.getProcessManager().getProcess(this.selectedProcessPid);
    const contentEl = this.container.querySelector('#process-details-content');
    
    if (!contentEl || !process) {
      // Process not found or content element not found
      return;
    }
    
    // Format uptime
    const uptime = this.formatUptime(Date.now() - process.startTime);
    
    contentEl.innerHTML = `
      <div class="details-section process-overview">
        <div class="process-header-info">
          <h2>${process.name}</h2>
          <div class="process-subheader">PID: ${process.pid} | User: ${process.user} | Status: <span class="process-status-${process.status}">${process.status}</span></div>
        </div>
        <div class="process-stats">
          <div class="stat-item">
            <div class="stat-label">CPU Usage</div>
            <div class="stat-value">${(process.cpuUsage * 100).toFixed(1)}%</div>
            <div class="resource-bar-container">
              <div class="resource-bar" style="width: ${process.cpuUsage * 100}%; background-color: ${this.getColorForUsage(process.cpuUsage * 100)}"></div>
            </div>
          </div>
          <div class="stat-item">
            <div class="stat-label">Memory Usage</div>
            <div class="stat-value">${process.memoryUsage.toFixed(1)}%</div>
            <div class="resource-bar-container">
              <div class="resource-bar" style="width: ${process.memoryUsage}%; background-color: ${this.getColorForUsage(process.memoryUsage)}"></div>
            </div>
          </div>
          <div class="stat-item">
            <div class="stat-label">Uptime</div>
            <div class="stat-value">${uptime}</div>
          </div>
        </div>
      </div>
      
      <div class="details-section">
        <h3>Process Information</h3>
        <table class="details-table">
          <tr>
            <td class="info-label">Command:</td>
            <td class="info-value">${process.command || process.name}</td>
          </tr>
          <tr>
            <td class="info-label">User:</td>
            <td class="info-value">${process.user}</td>
          </tr>
          <tr>
            <td class="info-label">PID:</td>
            <td class="info-value">${process.pid}</td>
          </tr>
          <tr>
            <td class="info-label">Status:</td>
            <td class="info-value">${process.status}</td>
          </tr>
          <tr>
            <td class="info-label">Start Time:</td>
            <td class="info-value">${new Date(process.startTime).toLocaleString()}</td>
          </tr>
          <tr>
            <td class="info-label">CPU Usage:</td>
            <td class="info-value">${(process.cpuUsage * 100).toFixed(1)}%</td>
          </tr>
          <tr>
            <td class="info-label">Memory Usage:</td>
            <td class="info-value">${process.memoryUsage.toFixed(1)}% (${Math.round(process.memoryUsage * 160 / 100)} MB)</td>
          </tr>
        </table>
      </div>
      
      <div class="details-section">
        <h3>Action</h3>
        ${process.pid < 10 ? 
          `<div class="system-process-warning">This is a system process. Terminating it may cause system instability.</div>` :
          `<button class="process-action end-process" id="terminate-process">Terminate Process</button>`
        }
      </div>
    `;
    
    // Add event listener for terminate button
    const terminateBtn = contentEl.querySelector('#terminate-process');
    terminateBtn?.addEventListener('click', () => {
      if (this.selectedProcessPid !== null && this.selectedProcessPid >= 10) {
        this.os.getProcessManager().killProcess(this.selectedProcessPid);
        this.selectedProcessPid = null;
        this.render();
      }
    });
  }

  /**
   * Get color based on usage percentage
   */
  private getColorForUsage(usage: number): string {
    if (usage < 60) {
      return '#0e639c';
    } else if (usage < 80) {
      return '#e6a23c';
    } else {
      return '#cc4444';
    }
  }
  
  /**
   * Update system data
   */
  private updateData(): void {
    if (!this.container) return;
    
    const processManager = this.os.getProcessManager();
    
    // Get CPU and memory usage
    const cpuUsage = processManager.getTotalCpuUsage();
    const ramUsage = processManager.getTotalMemoryUsage();
    
    // Simulate network and disk metrics
    this.networkUsage = Math.min(100, Math.max(0, this.networkUsage + (Math.random() * 10 - 5)));
    this.diskUsage = Math.min(100, Math.max(0, this.diskUsage + (Math.random() * 2 - 1)));
    this.diskFree = Math.min(this.diskTotal, Math.max(0, this.diskTotal * (1 - this.diskUsage / 100)));
    
    // Add data points to chart data
    this.chartData.cpu.push(cpuUsage);
    this.chartData.ram.push(ramUsage);
    this.chartData.network.push(this.networkUsage);
    this.chartData.disk.push(this.diskUsage);
    this.chartData.timestamps.push(Date.now());
    
    // Keep only the last MAX_DATA_POINTS
    if (this.chartData.cpu.length > this.MAX_DATA_POINTS) {
      this.chartData.cpu.shift();
      this.chartData.ram.shift();
      this.chartData.network.shift();
      this.chartData.disk.shift();
      this.chartData.timestamps.shift();
    }
    
    // Update the appropriate section based on active tab
    if (this.activeTab === 'overview') {
      this.updateOverviewTab(cpuUsage, ramUsage);
    } else if (this.activeTab === 'processes') {
      if (this.selectedProcessPid === null) {
        this.updateProcessList();
      } else {
        this.updateProcessDetailsView();
      }
    } else if (this.activeTab === 'performance') {
      this.updatePerformanceTab();
    } else if (this.activeTab === 'disk') {
      this.updateDiskTab();
    } else if (this.activeTab === 'network') {
      this.updateNetworkTab();
    }
  }
  
  /**
   * Update overview tab
   */
  private updateOverviewTab(cpuUsage: number, ramUsage: number): void {
    if (!this.container) return;
    
    // Update resource cards
    const cpuValue = this.container.querySelector('#cpu-value');
    const cpuBar = this.container.querySelector('#cpu-bar');
    const ramValue = this.container.querySelector('#ram-value');
    const ramBar = this.container.querySelector('#ram-bar');
    const diskValue = this.container.querySelector('#disk-value');
    const diskBar = this.container.querySelector('#disk-bar');
    const networkValue = this.container.querySelector('#network-value');
    const networkBar = this.container.querySelector('#network-bar');
    
    // Update CPU
    if (cpuValue) cpuValue.textContent = `${Math.round(cpuUsage)}%`;
    if (cpuBar) {
      const element = cpuBar as HTMLElement;
      element.style.width = `${cpuUsage}%`;
      element.style.backgroundColor = this.getColorForUsage(cpuUsage);
    }
    
    // Update RAM
    if (ramValue) ramValue.textContent = `${Math.round(ramUsage)}%`;
    if (ramBar) {
      const element = ramBar as HTMLElement;
      element.style.width = `${ramUsage}%`;
      element.style.backgroundColor = this.getColorForUsage(ramUsage);
    }
    
    // Update Disk
    if (diskValue) diskValue.textContent = `${Math.round(this.diskUsage)}%`;
    if (diskBar) {
      const element = diskBar as HTMLElement;
      element.style.width = `${this.diskUsage}%`;
      element.style.backgroundColor = this.getColorForUsage(this.diskUsage);
    }
    
    // Update Network (display as MB/s)
    const networkSpeedMBps = (this.networkUsage * 12.5 / 100).toFixed(1); // Convert percentage to MB/s (arbitrary scale)
    if (networkValue) networkValue.textContent = `${networkSpeedMBps} MB/s`;
    if (networkBar) {
      const element = networkBar as HTMLElement;
      element.style.width = `${this.networkUsage}%`;
      element.style.backgroundColor = this.getColorForUsage(this.networkUsage);
    }
    
    // Update system uptime
    const uptimeElement = this.container.querySelector('#system-uptime');
    if (uptimeElement) {
      // Calculate time since page load as simulated uptime
      const uptime = this.formatSystemUptime(performance.now());
      uptimeElement.textContent = uptime;
    }
    
    // Update disk free space
    const diskFreeElement = this.container.querySelector('#disk-free-space');
    if (diskFreeElement) {
      diskFreeElement.textContent = `${Math.round(this.diskFree)} GB free of ${this.diskTotal} GB`;
    }
  }
  
  /**
   * Update performance tab with charts
   */
  private updatePerformanceTab(): void {
    if (!this.container) return;
    
    const cpuCanvas = this.container.querySelector('#cpu-chart') as HTMLCanvasElement;
    const ramCanvas = this.container.querySelector('#ram-chart') as HTMLCanvasElement;
    const diskCanvas = this.container.querySelector('#disk-chart') as HTMLCanvasElement;
    const networkCanvas = this.container.querySelector('#network-chart') as HTMLCanvasElement;
    
    if (cpuCanvas) this.drawChart(cpuCanvas, this.chartData.cpu, '#0e639c');
    if (ramCanvas) this.drawChart(ramCanvas, this.chartData.ram, '#0e639c');
    if (diskCanvas) this.drawChart(diskCanvas, this.chartData.disk, '#e6a23c');
    if (networkCanvas) this.drawChart(networkCanvas, this.chartData.network, '#3eb83e');
  }
  
  /**
   * Update disk tab
   */
  private updateDiskTab(): void {
    if (!this.container) return;
    
    // Update disk usage gauge
    const diskChart = this.container.querySelector('.disk-chart') as HTMLElement;
    if (diskChart) {
      diskChart.style.setProperty('--usage', `${this.diskUsage}%`);
    }
    
    // Update disk usage indicator (dial)
    const diskIndicator = this.container.querySelector('#disk-usage-indicator') as HTMLElement;
    if (diskIndicator) {
      const rotation = (this.diskUsage / 100) * 180; // 0% = 0 degrees, 100% = 180 degrees
      diskIndicator.style.transform = `rotate(${rotation}deg)`;
    }
    
    // Update disk info text
    const diskFreeEl = this.container.querySelector('#disk-free');
    const diskTotalEl = this.container.querySelector('#disk-total');
    
    if (diskFreeEl) diskFreeEl.textContent = `${Math.round(this.diskFree)} GB`;
    if (diskTotalEl) diskTotalEl.textContent = `${this.diskTotal} GB`;
    
    // Update disk activity table with simulated data
    const diskActivityList = this.container.querySelector('#disk-activity-list');
    if (diskActivityList) {
      const processes = this.os.getProcessManager().getAllProcesses();
      const activeProcesses = processes
        .filter(p => p.cpuUsage > 0.05) // Only show processes with some activity
        .sort((a, b) => b.cpuUsage - a.cpuUsage) // Sort by CPU usage
        .slice(0, 5); // Show top 5
      
      diskActivityList.innerHTML = '';
      
      if (activeProcesses.length === 0) {
        diskActivityList.innerHTML = '<tr><td colspan="3" class="no-data">No disk activity</td></tr>';
      } else {
        activeProcesses.forEach(process => {
          // Simulate disk read/write based on CPU usage
          const readKBs = Math.round(process.cpuUsage * 500 * Math.random());
          const writeKBs = Math.round(process.cpuUsage * 300 * Math.random());
          
          const row = document.createElement('tr');
          row.innerHTML = `
            <td>${process.name}</td>
            <td>${readKBs} KB/s</td>
            <td>${writeKBs} KB/s</td>
          `;
          diskActivityList.appendChild(row);
        });
      }
    }
  }
  
  /**
   * Update network tab
   */
  private updateNetworkTab(): void {
    if (!this.container) return;
    
    // Update network traffic chart
    const networkChart = this.container.querySelector('#network-traffic-chart') as HTMLCanvasElement;
    if (networkChart) {
      this.drawChart(networkChart, this.chartData.network, '#3eb83e');
    }
    
    // Update network stats
    const sendSpeedEl = this.container.querySelector('#network-send');
    const receiveSpeedEl = this.container.querySelector('#network-receive');
    const connectionsEl = this.container.querySelector('#network-connections');
    
    // Calculate simulated sending/receiving speeds
    const sendSpeed = (this.networkUsage * 5 * Math.random()).toFixed(1);
    const receiveSpeed = (this.networkUsage * 8 * Math.random()).toFixed(1);
    const connections = Math.floor(this.networkUsage / 10) + 3; // Simulate connection count based on network usage
    
    if (sendSpeedEl) sendSpeedEl.textContent = `${sendSpeed} KB/s`;
    if (receiveSpeedEl) receiveSpeedEl.textContent = `${receiveSpeed} KB/s`;
    if (connectionsEl) connectionsEl.textContent = connections.toString();
    
    // Update network connections table with simulated data
    const connectionsTable = this.container.querySelector('#network-connections-list');
    if (connectionsTable) {
      const processes = this.os.getProcessManager().getAllProcesses();
      const networkProcesses = processes
        .filter(p => Math.random() < 0.3 || p.name.includes('ssh') || p.name.includes('http')) // Random selection with bias toward network processes
        .sort(() => Math.random() - 0.5) // Randomize order
        .slice(0, connections); // Show only as many as our connection count
      
      connectionsTable.innerHTML = '';
      
      if (networkProcesses.length === 0) {
        connectionsTable.innerHTML = '<tr><td colspan="5" class="no-data">No active connections</td></tr>';
      } else {
        // Network protocols
        const protocols = ['TCP', 'UDP'];
        // Connection states
        const states = ['ESTABLISHED', 'LISTEN', 'TIME_WAIT', 'CLOSE_WAIT'];
        
        networkProcesses.forEach(process => {
          const protocol = protocols[Math.floor(Math.random() * protocols.length)];
          const state = states[Math.floor(Math.random() * states.length)];
          const localPort = Math.floor(Math.random() * 60000) + 1024;
          const remotePort = [80, 443, 22, 53][Math.floor(Math.random() * 4)];
          
          const localAddress = `192.168.1.100:${localPort}`;
          const remoteAddress = `${Math.floor(Math.random() * 255)}.${Math.floor(Math.random() * 255)}.${Math.floor(Math.random() * 255)}.${Math.floor(Math.random() * 255)}:${remotePort}`;
          
          const row = document.createElement('tr');
          row.innerHTML = `
            <td>${process.name}</td>
            <td>${protocol}</td>
            <td>${localAddress}</td>
            <td>${remoteAddress}</td>
            <td>${state}</td>
          `;
          connectionsTable.appendChild(row);
        });
      }
    }
  }
  
  /**
   * Format system uptime as HH:MM:SS
   */
  private formatSystemUptime(ms: number): string {
    const seconds = Math.floor(ms / 1000);
    const minutes = Math.floor(seconds / 60);
    const hours = Math.floor(minutes / 60);
    const days = Math.floor(hours / 24);
    
    const hoursStr = String(hours % 24).padStart(2, '0');
    const minutesStr = String(minutes % 60).padStart(2, '0');
    const secondsStr = String(seconds % 60).padStart(2, '0');
    
    if (days > 0) {
      return `${days}d ${hoursStr}:${minutesStr}:${secondsStr}`;
    } else {
      return `${hoursStr}:${minutesStr}:${secondsStr}`;
    }
  }

  /**
   * Format uptime duration
   */
  private formatUptime(ms: number): string {
    const seconds = Math.floor(ms / 1000);
    const minutes = Math.floor(seconds / 60);
    const hours = Math.floor(minutes / 60);
    
    if (hours > 0) {
      return `${hours}h ${minutes % 60}m`;
    } else if (minutes > 0) {
      return `${minutes}m ${seconds % 60}s`;
    } else {
      return `${seconds}s`;
    }
  }
  
  /**
   * Update process list
   */
  private updateProcessList(): void {
    if (!this.container) return;
    
    const processManager = this.os.getProcessManager();
    const processes = processManager.getAllProcesses();
    const processList = this.container.querySelector('#process-list');
    
    if (!processList) return;
    
    // Store selected processes before updating
    const selectedProcesses = new Set<number>();
    this.container.querySelectorAll('.process-row.selected-process').forEach(row => {
      const pid = row.getAttribute('data-pid');
      if (pid) selectedProcesses.add(parseInt(pid));
    });
    
    // Get filter value
    const filterInput = this.container.querySelector('.process-filter') as HTMLInputElement;
    const filterValue = filterInput?.value.toLowerCase() || '';
    
    // Clear current list
    processList.innerHTML = '';
    
    // Add processes to the list
    processes
      .filter(process => {
        if (!filterValue) return true;
        return process.name.toLowerCase().includes(filterValue) || 
               process.user.toLowerCase().includes(filterValue) || 
               process.pid.toString().includes(filterValue);
      })
      .sort((a, b) => b.cpuUsage - a.cpuUsage) // Sort by CPU usage (descending)
      .forEach(process => {
        const row = document.createElement('tr');
        row.className = 'process-row';
        row.setAttribute('data-pid', process.pid.toString());
        
        // Restore selection state
        if (selectedProcesses.has(process.pid)) {
          row.classList.add('selected-process');
        }
        
        // Format uptime
        const uptime = this.formatUptime(Date.now() - process.startTime);
        
        // Status class
        const statusClass = `process-status-${process.status}`;
        
        row.innerHTML = `
          <td><input type="checkbox" class="process-checkbox" ${selectedProcesses.has(process.pid) ? 'checked' : ''}></td>
          <td>${process.pid}</td>
          <td>${process.name}</td>
          <td>${process.user}</td>
          <td>${(process.cpuUsage * 100).toFixed(1)}%</td>
          <td>${process.memoryUsage.toFixed(1)}%</td>
          <td class="${statusClass}">${process.status}</td>
          <td>${uptime}</td>
        `;
        
        // Add event listeners
        row.addEventListener('click', (e) => {
          // Don't toggle if clicking on the checkbox
          if ((e.target as HTMLElement).closest('.process-checkbox')) return;
          this.toggleProcessSelection(row);
        });
        
        const checkbox = row.querySelector('.process-checkbox');
        checkbox?.addEventListener('change', (e) => {
          const target = e.target as HTMLInputElement;
          if (target.checked) {
            row.classList.add('selected-process');
          } else {
            row.classList.remove('selected-process');
          }
          this.updateSelectAllCheckbox();
        });
        
        processList.appendChild(row);
      });
  }

  /**
   * Draw a simple line chart on a canvas
   */
  private drawChart(canvas: HTMLCanvasElement, data: number[], color: string): void {
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    
    const width = canvas.width;
    const height = canvas.height;
    
    // Clear canvas
    ctx.clearRect(0, 0, width, height);
    
    // No data to display
    if (data.length === 0) return;
    
    // Find min and max values
    const max = 100; // Fixed scale to 100%
    const min = 0;
    
    // Draw background grid
    ctx.strokeStyle = '#333';
    ctx.lineWidth = 0.5;
    
    // Draw horizontal grid lines
    for (let i = 0; i <= 4; i++) {
      const y = height - (i * height / 4);
      ctx.beginPath();
      ctx.moveTo(0, y);
      ctx.lineTo(width, y);
      ctx.stroke();
    }
    
    // Draw the line
    ctx.strokeStyle = color;
    ctx.lineWidth = 2;
    ctx.beginPath();
    
    // Calculate point coordinates
    const points = data.map((value, index) => {
      const x = (index / (data.length - 1)) * width;
      const y = height - ((value - min) / (max - min)) * height;
      return { x, y };
    });
    
    // Draw path
    ctx.moveTo(points[0].x, points[0].y);
    for (let i = 1; i < points.length; i++) {
      ctx.lineTo(points[i].x, points[i].y);
    }
    ctx.stroke();
    
    // Fill area under the line
    ctx.fillStyle = `${color}33`; // Add transparency
    ctx.beginPath();
    ctx.moveTo(points[0].x, height);
    for (let i = 0; i < points.length; i++) {
      ctx.lineTo(points[i].x, points[i].y);
    }
    ctx.lineTo(points[points.length - 1].x, height);
    ctx.closePath();
    ctx.fill();
  }
  
  /**
   * Toggle process selection
   */
  private toggleProcessSelection(row: HTMLElement): void {
    row.classList.toggle('selected-process');
    
    // Update checkbox
    const checkbox = row.querySelector('.process-checkbox') as HTMLInputElement;
    if (checkbox) {
      checkbox.checked = row.classList.contains('selected-process');
    }
    
    // Update select all checkbox
    this.updateSelectAllCheckbox();
  }
  
  /**
   * Select all processes
   */
  private selectAllProcesses(selected: boolean): void {
    if (!this.container) return;
    
    const rows = this.container.querySelectorAll('.process-row');
    rows.forEach(row => {
      if (selected) {
        row.classList.add('selected-process');
      } else {
        row.classList.remove('selected-process');
      }
      
      // Update checkbox
      const checkbox = row.querySelector('.process-checkbox') as HTMLInputElement;
      if (checkbox) {
        checkbox.checked = selected;
      }
    });
  }
  
  /**
   * Update select all checkbox state
   */
  private updateSelectAllCheckbox(): void {
    if (!this.container) return;
    
    const selectAllCheckbox = this.container.querySelector('#select-all-processes') as HTMLInputElement;
    if (!selectAllCheckbox) return;
    
    const totalRows = this.container.querySelectorAll('.process-row').length;
    const selectedRows = this.container.querySelectorAll('.process-row.selected-process').length;
    
    if (selectedRows === 0) {
      selectAllCheckbox.checked = false;
      selectAllCheckbox.indeterminate = false;
    } else if (selectedRows === totalRows) {
      selectAllCheckbox.checked = true;
      selectAllCheckbox.indeterminate = false;
    } else {
      selectAllCheckbox.checked = false;
      selectAllCheckbox.indeterminate = true;
    }
  }
  
  /**
   * End selected processes
   */
  private endSelectedProcesses(): void {
    if (!this.container) return;
    
    const processManager = this.os.getProcessManager();
    const selectedRows = this.container.querySelectorAll('.process-row.selected-process');
    
    selectedRows.forEach(row => {
      const pid = parseInt(row.getAttribute('data-pid') || '0');
      if (pid > 0) {
        // Don't kill system processes (pid < 10)
        if (pid >= 10) {
          processManager.killProcess(pid);
        } else {
          // Show error for system processes
          alert('Cannot terminate system process');
        }
      }
    });
    
    // Update process list
    this.updateProcessList();
  }
  
  /**
   * Filter processes
   */
  private filterProcesses(filter: string): void {
    // This triggers a re-render of the process list with filtering
    this.updateProcessList();
  }
  
  /**
   * Refresh data
   */
  private refresh(): void {
    this.updateData();
  }
  
  /**
   * Clean up resources
   */
  public dispose(): void {
    if (this.updateInterval !== null) {
      clearInterval(this.updateInterval);
      this.updateInterval = null;
    }
  }
}
