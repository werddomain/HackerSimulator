/**
 * Mobile System Monitor Extension
 * Adds mobile-specific performance monitoring to the System Monitor app
 */

import './mobile-performance.less';
import { PerformanceMonitor } from '../core/performance-monitor';

/**
 * Initialize a mobile-optimized rendering tab in the System Monitor
 * @param {HTMLElement} container - The container element for the system monitor
 * @param {Function} switchTabCallback - Callback to switch tabs 
 */
export function initMobilePerformanceMonitoring(container, switchTabCallback) {
  if (!container) return;
  
  // Create the performance monitor
  const performanceMonitor = new PerformanceMonitor({
    targetFps: 60,
    sampleSize: 60,
    criticalFrameTime: 16.67, // 60fps = 16.67ms per frame
    smoothingFactor: 0.1
  });
  
  // Add Rendering tab button to tab bar
  const tabButtons = container.querySelector('.tab-buttons');
  if (tabButtons) {
    const renderingTabBtn = document.createElement('button');
    renderingTabBtn.className = 'tab-btn';
    renderingTabBtn.setAttribute('data-tab', 'rendering');
    renderingTabBtn.textContent = 'Rendering';
    
    renderingTabBtn.addEventListener('click', () => {
      // Update active tab visually
      const allTabBtns = tabButtons.querySelectorAll('.tab-btn');
      allTabBtns.forEach(btn => btn.classList.remove('active'));
      renderingTabBtn.classList.add('active');
      
      // Hide all tab content
      const allTabContent = container.querySelectorAll('.tab-content');
      allTabContent.forEach(content => content.classList.remove('active'));
      
      // Show rendering tab content
      const renderingTab = container.querySelector('#tab-rendering');
      if (renderingTab) {
        renderingTab.classList.add('active');
      }
      
      // Update rendering metrics
      updateRenderingMetrics();
      
      // Call the switch tab callback with 'rendering'
      if (switchTabCallback) {
        switchTabCallback('rendering');
      }
    });
    
    tabButtons.appendChild(renderingTabBtn);
  }
  
  // Create rendering tab content
  const tabsContainer = container.querySelector('.tabs-container');
  if (tabsContainer) {
    const renderingTab = document.createElement('div');
    renderingTab.className = 'tab-content';
    renderingTab.id = 'tab-rendering';
    
    renderingTab.innerHTML = `
      <div class="performance-metrics">
        <div class="metric-card">
          <div class="metric-title">Frames Per Second</div>
          <div class="metric-value" id="fps-value">0 FPS</div>
          <div class="metric-bar-container">
            <div class="metric-bar" id="fps-bar" style="width: 0%;"></div>
          </div>
          <div class="metric-description">Target: 60 FPS</div>
        </div>
        
        <div class="metric-card">
          <div class="metric-title">Frame Time</div>
          <div class="metric-value" id="frame-time-value">0 ms</div>
          <div class="metric-bar-container">
            <div class="metric-bar" id="frame-time-bar" style="width: 0%;"></div>
          </div>
          <div class="metric-description">Target: &lt;16.7 ms</div>
        </div>
        
        <div class="metric-card">
          <div class="metric-title">Jank Score</div>
          <div class="metric-value" id="jank-value">0%</div>
          <div class="metric-bar-container">
            <div class="metric-bar" id="jank-bar" style="width: 0%;"></div>
          </div>
          <div class="metric-description">% of frames that took significantly longer than average</div>
        </div>
      </div>
      
      <div class="performance-charts">
        <div class="chart-card">
          <div class="chart-title">FPS History</div>
          <canvas id="fps-chart" height="150"></canvas>
        </div>
        
        <div class="chart-card">
          <div class="chart-title">Frame Time History</div>
          <canvas id="frame-time-chart" height="150"></canvas>
        </div>
      </div>
      
      <div class="performance-card detailed-metrics-container">
        <h3>Detailed Metrics</h3>
        <table class="details-table" id="detailed-metrics">
          <tr>
            <td class="info-label">Min Frame Time:</td>
            <td class="info-value">0 ms</td>
          </tr>
          <tr>
            <td class="info-label">Max Frame Time:</td>
            <td class="info-value">0 ms</td>
          </tr>
          <tr>
            <td class="info-label">Frame Time Variance:</td>
            <td class="info-value">0 ms</td>
          </tr>
          <tr>
            <td class="info-label">Last Updated:</td>
            <td class="info-value">--</td>
          </tr>
        </table>
      </div>
      
      <div class="suggestions-container">
        <h3 class="suggestions-title">Optimization Suggestions</h3>
        <ul class="suggestions-list" id="optimization-suggestions">
          <li class="no-suggestions">Performance monitoring starting up...</li>
        </ul>
      </div>
    `;
    
    tabsContainer.appendChild(renderingTab);
  }
  
  // Performance data for charts
  const performanceData = {
    fps: [],
    frameTime: [],
    timestamps: [],
    maxDataPoints: 60
  };
  
  // Start monitoring performance
  performanceMonitor.start();
  
  // Set up metrics update callback
  performanceMonitor.onUpdate((metrics) => {
    // Add metrics to chart data
    performanceData.fps.push(metrics.fps);
    performanceData.frameTime.push(metrics.frameTime);
    performanceData.timestamps.push(Date.now());
    
    // Keep only the last maxDataPoints
    if (performanceData.fps.length > performanceData.maxDataPoints) {
      performanceData.fps.shift();
      performanceData.frameTime.shift();
      performanceData.timestamps.shift();
    }
    
    // Update rendering tab if it's active
    const renderingTab = container.querySelector('#tab-rendering');
    if (renderingTab && renderingTab.classList.contains('active')) {
      updateRenderingMetrics();
    }
  });
  
  /**
   * Update rendering tab with performance metrics
   */
  function updateRenderingMetrics() {
    const metrics = performanceMonitor.getMetrics();
    
    // Update FPS and frame time values
    const fpsValue = container.querySelector('#fps-value');
    const fpsBar = container.querySelector('#fps-bar');
    const frameTimeValue = container.querySelector('#frame-time-value');
    const frameTimeBar = container.querySelector('#frame-time-bar');
    const jankValue = container.querySelector('#jank-value');
    const jankBar = container.querySelector('#jank-bar');
    
    if (fpsValue) fpsValue.textContent = `${Math.round(metrics.fps)} FPS`;
    if (fpsBar) {
      // Calculate percentage of target FPS (60)
      const fpsPercentage = Math.min(100, (metrics.fps / 60) * 100);
      fpsBar.style.width = `${fpsPercentage}%`;
      fpsBar.style.backgroundColor = getColorForFps(metrics.fps);
      
      // Add class for color indication
      fpsValue.className = 'metric-value ' + getFpsClass(metrics.fps);
    }
    
    if (frameTimeValue) frameTimeValue.textContent = `${metrics.averageFrameTime.toFixed(2)} ms`;
    if (frameTimeBar) {
      // Calculate percentage based on critical frame time (33.33ms for 30fps)
      const frameTimePercentage = Math.min(100, (metrics.averageFrameTime / 33.33) * 100);
      frameTimeBar.style.width = `${frameTimePercentage}%`;
      frameTimeBar.style.backgroundColor = getColorForFrameTime(metrics.averageFrameTime);
      
      // Add class for color indication
      frameTimeValue.className = 'metric-value ' + getFrameTimeClass(metrics.averageFrameTime);
    }
    
    if (jankValue) jankValue.textContent = `${metrics.jank.toFixed(1)}%`;
    if (jankBar) {
      // Jank score as percentage (capped at 30%)
      const jankPercentage = Math.min(100, metrics.jank * 3);
      jankBar.style.width = `${jankPercentage}%`;
      jankBar.style.backgroundColor = getColorForJank(metrics.jank);
      
      // Add class for color indication
      jankValue.className = 'metric-value ' + getJankClass(metrics.jank);
    }
    
    // Update performance charts
    const fpsChart = container.querySelector('#fps-chart');
    const frameTimeChart = container.querySelector('#frame-time-chart');
    
    if (fpsChart) drawChart(fpsChart, performanceData.fps, '#0e639c');
    if (frameTimeChart) drawChart(frameTimeChart, performanceData.frameTime, '#e6a23c');
    
    // Update suggestions list
    const suggestionsList = container.querySelector('#optimization-suggestions');
    if (suggestionsList && performanceMonitor) {
      const suggestions = performanceMonitor.getOptimizationSuggestions();
      
      if (suggestions.length === 0) {
        suggestionsList.innerHTML = '<li class="no-suggestions">Performance looks good! No issues detected.</li>';
      } else {
        suggestionsList.innerHTML = suggestions.map(suggestion => {
          // Determine suggestion severity for styling
          let severity = 'warning';
          if (suggestion.toLowerCase().includes('high jank') || 
              suggestion.toLowerCase().includes('below target') ||
              suggestion.toLowerCase().includes('exceeds target')) {
            severity = 'critical';
          }
          
          return `<li class="${severity}">${suggestion}</li>`;
        }).join('');
      }
    }
    
    // Update detailed metrics
    const detailedMetrics = container.querySelector('#detailed-metrics');
    if (detailedMetrics) {
      detailedMetrics.innerHTML = `
        <tr>
          <td class="info-label">Min Frame Time:</td>
          <td class="info-value">${metrics.minFrameTime.toFixed(2)} ms</td>
        </tr>
        <tr>
          <td class="info-label">Max Frame Time:</td>
          <td class="info-value">${metrics.maxFrameTime.toFixed(2)} ms</td>
        </tr>
        <tr>
          <td class="info-label">Frame Time Variance:</td>
          <td class="info-value">${(metrics.maxFrameTime - metrics.minFrameTime).toFixed(2)} ms</td>
        </tr>
        <tr>
          <td class="info-label">Last Updated:</td>
          <td class="info-value">${new Date(metrics.lastUpdate).toLocaleTimeString()}</td>
        </tr>
      `;
    }
  }
  
  /**
   * Draw a simple line chart on a canvas
   */
  function drawChart(canvas, data, color) {
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    
    const width = canvas.width;
    const height = canvas.height;
    
    // Clear canvas
    ctx.clearRect(0, 0, width, height);
    
    // No data to display
    if (data.length === 0) return;
    
    // Find min and max values
    let max, min;
    
    if (color === '#0e639c') { // FPS chart
      max = 70; // Allow some headroom above 60fps
      min = 0;
    } else { // Frame time chart
      max = 50; // Allow for frames up to 50ms (20fps)
      min = 0;
    }
    
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
    
    // Draw target line for FPS chart (60fps)
    if (color === '#0e639c') {
      const targetFpsY = height - ((60 - min) / (max - min)) * height;
      ctx.strokeStyle = '#3eb83e';
      ctx.lineWidth = 1;
      ctx.setLineDash([5, 3]);
      ctx.beginPath();
      ctx.moveTo(0, targetFpsY);
      ctx.lineTo(width, targetFpsY);
      ctx.stroke();
      ctx.setLineDash([]);
    }
    
    // Draw critical line for Frame Time chart (16.67ms - 60fps)
    if (color !== '#0e639c') {
      const criticalFrameTimeY = height - ((16.67 - min) / (max - min)) * height;
      ctx.strokeStyle = '#e6a23c';
      ctx.lineWidth = 1;
      ctx.setLineDash([5, 3]);
      ctx.beginPath();
      ctx.moveTo(0, criticalFrameTimeY);
      ctx.lineTo(width, criticalFrameTimeY);
      ctx.stroke();
      ctx.setLineDash([]);
    }
    
    // Draw the line
    ctx.strokeStyle = color;
    ctx.lineWidth = 2;
    ctx.beginPath();
    
    // Calculate point coordinates
    const points = data.map((value, index) => {
      const x = (index / (data.length - 1)) * width;
      const y = height - ((Math.min(value, max) - min) / (max - min)) * height;
      return { x, y };
    });
    
    // Draw path
    if (points.length > 0) {
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
  }
  
  /**
   * Get color for FPS value
   */
  function getColorForFps(fps) {
    if (fps >= 55) {
      return '#3eb83e'; // Green for good FPS
    } else if (fps >= 30) {
      return '#e6a23c'; // Yellow for acceptable FPS
    } else {
      return '#cc4444'; // Red for poor FPS
    }
  }
  
  /**
   * Get CSS class for FPS value
   */
  function getFpsClass(fps) {
    if (fps >= 55) {
      return 'metric-good';
    } else if (fps >= 30) {
      return 'metric-warning';
    } else {
      return 'metric-critical';
    }
  }
  
  /**
   * Get color for Frame Time value
   */
  function getColorForFrameTime(frameTime) {
    if (frameTime <= 16.67) {
      return '#3eb83e'; // Green for good frame time (60fps+)
    } else if (frameTime <= 33.33) {
      return '#e6a23c'; // Yellow for acceptable frame time (30-60fps)
    } else {
      return '#cc4444'; // Red for poor frame time (<30fps)
    }
  }
  
  /**
   * Get CSS class for Frame Time value
   */
  function getFrameTimeClass(frameTime) {
    if (frameTime <= 16.67) {
      return 'metric-good';
    } else if (frameTime <= 33.33) {
      return 'metric-warning';
    } else {
      return 'metric-critical';
    }
  }
  
  /**
   * Get color for Jank score
   */
  function getColorForJank(jankScore) {
    if (jankScore <= 2) {
      return '#3eb83e'; // Green for low jank
    } else if (jankScore <= 5) {
      return '#e6a23c'; // Yellow for moderate jank
    } else {
      return '#cc4444'; // Red for high jank
    }
  }
  
  /**
   * Get CSS class for Jank score
   */
  function getJankClass(jankScore) {
    if (jankScore <= 2) {
      return 'metric-good';
    } else if (jankScore <= 5) {
      return 'metric-warning';
    } else {
      return 'metric-critical';
    }
  }
  
  // Return an object with cleanup function
  return {
    dispose: () => {
      performanceMonitor.dispose();
    }
  };
}
