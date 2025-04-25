/**
 * Skeleton Screen System
 * Provides loading placeholders for content that mimic the shape of the content being loaded
 */

/**
 * Configuration for skeleton screens
 */
export interface SkeletonConfig {
  // Animation type (pulse, wave, none)
  animation?: 'pulse' | 'wave' | 'none';
  
  // Base color (default: #e0e0e0)
  baseColor?: string;
  
  // Highlight color for animations (default: #f0f0f0)
  highlightColor?: string;
  
  // Animation duration in ms (default: 1500)
  duration?: number;
  
  // Border radius for skeleton elements (default: '4px')
  borderRadius?: string;
  
  // Whether to use SVG-based rendering (smoother but more complex)
  useSVG?: boolean;
}

/**
 * Default configuration for skeleton screens
 */
const DEFAULT_CONFIG: Required<SkeletonConfig> = {
  animation: 'pulse',
  baseColor: '#e0e0e0',
  highlightColor: '#f0f0f0',
  duration: 1500,
  borderRadius: '4px',
  useSVG: false
};

/**
 * Class to create and manage skeleton screens
 */
export class SkeletonScreen {
  private config: Required<SkeletonConfig>;
  private container: HTMLElement;
  private elements: HTMLElement[] = [];
  private svgElements: SVGElement[] = [];
  private styleElement: HTMLStyleElement | null = null;
  private uniqueClassName: string;
  
  /**
   * Create a new skeleton screen
   * @param container Container element for the skeleton
   * @param config Configuration options
   */
  constructor(container: HTMLElement, config: SkeletonConfig = {}) {
    this.container = container;
    this.config = { ...DEFAULT_CONFIG, ...config };
    this.uniqueClassName = `skeleton-${Math.random().toString(36).substring(2, 9)}`;
    
    // Initialize skeleton screen
    this.initialize();
  }
  
  /**
   * Initialize the skeleton screen
   */
  private initialize(): void {
    // Create unique styles for this skeleton screen
    this.createStyles();
    
    // Add class to container
    this.container.classList.add('skeleton-container');
  }
  
  /**
   * Create CSS styles for the skeleton screen
   */
  private createStyles(): void {
    // Create style element if it doesn't exist yet
    if (!document.getElementById('skeleton-base-styles')) {
      const baseStyles = document.createElement('style');
      baseStyles.id = 'skeleton-base-styles';
      baseStyles.textContent = `
        .skeleton-container {
          position: relative;
          overflow: hidden;
        }
        
        .skeleton-element {
          background-color: var(--skeleton-base-color);
          border-radius: var(--skeleton-border-radius);
          min-height: 1em;
          position: relative;
          overflow: hidden;
        }
        
        @keyframes skeleton-pulse {
          0% {
            opacity: 1;
          }
          50% {
            opacity: 0.5;
          }
          100% {
            opacity: 1;
          }
        }
        
        @keyframes skeleton-wave {
          0% {
            transform: translateX(-100%);
          }
          50% {
            transform: translateX(100%);
          }
          100% {
            transform: translateX(100%);
          }
        }
        
        .skeleton-wave::after {
          content: '';
          position: absolute;
          top: 0;
          left: 0;
          right: 0;
          bottom: 0;
          background: linear-gradient(90deg, transparent, var(--skeleton-highlight-color), transparent);
          animation: skeleton-wave var(--skeleton-duration) infinite;
        }
        
        .skeleton-pulse {
          animation: skeleton-pulse var(--skeleton-duration) infinite;
        }
      `;
      document.head.appendChild(baseStyles);
    }
    
    // Create instance-specific styles
    this.styleElement = document.createElement('style');
    this.styleElement.textContent = `
      .${this.uniqueClassName} {
        --skeleton-base-color: ${this.config.baseColor};
        --skeleton-highlight-color: ${this.config.highlightColor};
        --skeleton-duration: ${this.config.duration}ms;
        --skeleton-border-radius: ${this.config.borderRadius};
      }
    `;
    document.head.appendChild(this.styleElement);
    
    // Add class to container
    this.container.classList.add(this.uniqueClassName);
  }
  
  /**
   * Add a skeleton element with specified dimensions
   * @param options Element options
   */
  public addElement(options: {
    width?: string | number;
    height?: string | number;
    borderRadius?: string;
    left?: string | number;
    top?: string | number;
    marginBottom?: string | number;
  }): HTMLElement {
    const element = document.createElement('div');
    element.className = `skeleton-element ${this.config.animation !== 'none' ? `skeleton-${this.config.animation}` : ''}`;
    
    // Set dimensions and position
    if (options.width !== undefined) {
      element.style.width = typeof options.width === 'number' ? `${options.width}px` : options.width;
    }
    
    if (options.height !== undefined) {
      element.style.height = typeof options.height === 'number' ? `${options.height}px` : options.height;
    }
    
    if (options.borderRadius !== undefined) {
      element.style.borderRadius = options.borderRadius;
    }
    
    if (options.left !== undefined) {
      element.style.left = typeof options.left === 'number' ? `${options.left}px` : options.left;
    }
    
    if (options.top !== undefined) {
      element.style.top = typeof options.top === 'number' ? `${options.top}px` : options.top;
    }
    
    if (options.marginBottom !== undefined) {
      element.style.marginBottom = typeof options.marginBottom === 'number' ? 
        `${options.marginBottom}px` : options.marginBottom;
    }
    
    // Add to container and track
    this.container.appendChild(element);
    this.elements.push(element);
    
    return element;
  }
  
  /**
   * Create a text skeleton with multiple lines
   * @param options Text options
   */
  public addTextBlock(options: {
    lines?: number;
    lineHeight?: number;
    fontSize?: string | number;
    lastLineWidth?: string | number;
    spacing?: number;
    variant?: 'paragraph' | 'heading';
  } = {}): HTMLElement[] {
    const {
      lines = 3,
      lineHeight = 16,
      fontSize = '1em',
      lastLineWidth = '80%',
      spacing = 8,
      variant = 'paragraph'
    } = options;
    
    const elements: HTMLElement[] = [];
    const wrapper = document.createElement('div');
    wrapper.className = 'skeleton-text-block';
    
    for (let i = 0; i < lines; i++) {
      const line = document.createElement('div');
      line.className = `skeleton-element ${this.config.animation !== 'none' ? `skeleton-${this.config.animation}` : ''}`;
      
      // Set line styles
      line.style.height = `${lineHeight}px`;
      line.style.width = i === lines - 1 && lastLineWidth ? 
        (typeof lastLineWidth === 'number' ? `${lastLineWidth}px` : lastLineWidth) : '100%';
      line.style.marginBottom = `${spacing}px`;
      
      // Adjust size for headings
      if (variant === 'heading' && i === 0) {
        line.style.height = `${lineHeight * 1.5}px`;
      }
      
      wrapper.appendChild(line);
      elements.push(line);
    }
    
    this.container.appendChild(wrapper);
    this.elements.push(...elements);
    
    return elements;
  }
  
  /**
   * Create a skeleton for an image or card
   */
  public addImagePlaceholder(options: {
    width?: string | number;
    height?: string | number;
    borderRadius?: string;
  } = {}): HTMLElement {
    const {
      width = '100%',
      height = 200,
      borderRadius = this.config.borderRadius
    } = options;
    
    return this.addElement({
      width,
      height,
      borderRadius,
      marginBottom: 16
    });
  }
  
  /**
   * Create a skeleton for a card with image and text
   */
  public addCard(options: {
    width?: string | number;
    height?: string | number;
    imageHeight?: number;
    titleLines?: number;
    contentLines?: number;
    borderRadius?: string;
    padding?: number;
  } = {}): HTMLElement {
    const {
      width = '100%',
      height = 'auto',
      imageHeight = 200,
      titleLines = 1,
      contentLines = 2,
      borderRadius = this.config.borderRadius,
      padding = 16
    } = options;
    
    // Create card container
    const card = document.createElement('div');
    card.className = 'skeleton-card';
    card.style.width = typeof width === 'number' ? `${width}px` : width;
    card.style.height = typeof height === 'number' ? `${height}px` : height;
    card.style.borderRadius = borderRadius;
    card.style.overflow = 'hidden';
    card.style.border = '1px solid #e0e0e0';
    
    // Add image placeholder
    const image = this.addElement({
      width: '100%',
      height: imageHeight
    });
    
    // Create content container
    const content = document.createElement('div');
    content.style.padding = `${padding}px`;
    
    // Add to card
    card.appendChild(image);
    card.appendChild(content);
    
    // Add title and content
    const titleElements = this.addTextBlock({
      lines: titleLines,
      lineHeight: 24,
      spacing: 12,
      variant: 'heading'
    });
    
    const contentElements = this.addTextBlock({
      lines: contentLines,
      lineHeight: 16,
      spacing: 8
    });
    
    // Move elements to content div
    titleElements.forEach(el => content.appendChild(el));
    contentElements.forEach(el => content.appendChild(el));
    
    // Add card to container
    this.container.appendChild(card);
    this.elements.push(card);
    
    return card;
  }
  
  /**
   * Create a skeleton for a list
   */
  public addList(options: {
    items?: number;
    itemHeight?: number;
    spacing?: number;
    avatar?: boolean;
    avatarSize?: number;
    lines?: number;
  } = {}): HTMLElement[] {
    const {
      items = 3,
      itemHeight = 64,
      spacing = 8,
      avatar = true,
      avatarSize = 40,
      lines = 2
    } = options;
    
    const listItems: HTMLElement[] = [];
    
    for (let i = 0; i < items; i++) {
      // Create list item container
      const item = document.createElement('div');
      item.className = 'skeleton-list-item';
      item.style.display = 'flex';
      item.style.marginBottom = `${spacing}px`;
      item.style.height = `${itemHeight}px`;
      item.style.padding = `${spacing}px`;
      item.style.alignItems = 'center';
      
      // Add avatar if enabled
      if (avatar) {
        const avatarElement = this.addElement({
          width: avatarSize,
          height: avatarSize,
          borderRadius: '50%'
        });
        avatarElement.style.flexShrink = '0';
        avatarElement.style.marginRight = `${spacing}px`;
        item.appendChild(avatarElement);
      }
      
      // Add content container
      const content = document.createElement('div');
      content.style.flex = '1';
      
      // Add lines
      for (let j = 0; j < lines; j++) {
        const line = this.addElement({
          width: j === 0 ? '70%' : '100%',
          height: 16,
          marginBottom: 8
        });
        content.appendChild(line);
      }
      
      item.appendChild(content);
      this.container.appendChild(item);
      listItems.push(item);
      this.elements.push(item);
    }
    
    return listItems;
  }
  
  /**
   * Create a skeleton for a table
   */
  public addTable(options: {
    rows?: number;
    columns?: number;
    headerHeight?: number;
    rowHeight?: number;
    showHeader?: boolean;
  } = {}): HTMLElement {
    const {
      rows = 5,
      columns = 4,
      headerHeight = 48,
      rowHeight = 40,
      showHeader = true
    } = options;
    
    // Create table container
    const table = document.createElement('div');
    table.className = 'skeleton-table';
    table.style.width = '100%';
    table.style.borderCollapse = 'collapse';
    
    // Add header if enabled
    if (showHeader) {
      const header = document.createElement('div');
      header.className = 'skeleton-table-header';
      header.style.display = 'flex';
      header.style.height = `${headerHeight}px`;
      header.style.marginBottom = '1px';
      
      // Add header cells
      for (let i = 0; i < columns; i++) {
        const width = i === 0 ? '20%' : `${80 / (columns - 1)}%`;
        const cell = this.addElement({
          width,
          height: headerHeight - 16,
          marginBottom: 0
        });
        cell.style.margin = '8px';
        header.appendChild(cell);
      }
      
      table.appendChild(header);
    }
    
    // Add rows
    for (let i = 0; i < rows; i++) {
      const row = document.createElement('div');
      row.className = 'skeleton-table-row';
      row.style.display = 'flex';
      row.style.height = `${rowHeight}px`;
      row.style.marginBottom = '1px';
      
      // Add row cells
      for (let j = 0; j < columns; j++) {
        const width = j === 0 ? '20%' : `${80 / (columns - 1)}%`;
        const cell = this.addElement({
          width,
          height: rowHeight - 16,
          marginBottom: 0
        });
        cell.style.margin = '8px';
        row.appendChild(cell);
      }
      
      table.appendChild(row);
    }
    
    this.container.appendChild(table);
    this.elements.push(table);
    
    return table;
  }
  
  /**
   * Create a complex layout skeleton
   * @param type Layout type
   */
  public addLayout(type: 'profile' | 'dashboard' | 'article' | 'feed' = 'article'): void {
    switch (type) {
      case 'profile':
        this.createProfileLayout();
        break;
      case 'dashboard':
        this.createDashboardLayout();
        break;
      case 'article':
        this.createArticleLayout();
        break;
      case 'feed':
        this.createFeedLayout();
        break;
    }
  }
  
  /**
   * Create a profile page skeleton
   */
  private createProfileLayout(): void {
    // Header with avatar and name
    const header = document.createElement('div');
    header.style.display = 'flex';
    header.style.alignItems = 'center';
    header.style.marginBottom = '24px';
    
    // Add avatar
    const avatar = this.addElement({
      width: 120,
      height: 120,
      borderRadius: '50%'
    });
    avatar.style.marginRight = '24px';
    header.appendChild(avatar);
    
    // Info container
    const info = document.createElement('div');
    info.style.flex = '1';
    
    // Add title and content
    this.addTextBlock({
      lines: 1,
      lineHeight: 32,
      spacing: 12,
      variant: 'heading'
    }).forEach(el => info.appendChild(el));
    
    this.addTextBlock({
      lines: 2,
      lineHeight: 16,
      spacing: 8
    }).forEach(el => info.appendChild(el));
    
    header.appendChild(info);
    this.container.appendChild(header);
    
    // Stats row
    const stats = document.createElement('div');
    stats.style.display = 'flex';
    stats.style.justifyContent = 'space-between';
    stats.style.marginBottom = '32px';
    
    for (let i = 0; i < 3; i++) {
      const stat = document.createElement('div');
      stat.style.flex = '1';
      stat.style.margin = '0 8px';
      stat.style.textAlign = 'center';
      
      const value = this.addElement({
        width: '80%',
        height: 32,
        marginBottom: 8
      });
      value.style.margin = '0 auto 8px auto';
      
      const label = this.addElement({
        width: '60%',
        height: 16,
        marginBottom: 0
      });
      label.style.margin = '0 auto';
      
      stat.appendChild(value);
      stat.appendChild(label);
      stats.appendChild(stat);
    }
    
    this.container.appendChild(stats);
    
    // Content tabs
    const tabs = document.createElement('div');
    tabs.style.display = 'flex';
    tabs.style.borderBottom = '1px solid #e0e0e0';
    tabs.style.marginBottom = '24px';
    
    for (let i = 0; i < 4; i++) {
      const tab = this.addElement({
        width: 100,
        height: 32,
        marginBottom: 0
      });
      tab.style.marginRight = '16px';
      tabs.appendChild(tab);
    }
    
    this.container.appendChild(tabs);
    
    // Grid content
    const grid = document.createElement('div');
    grid.style.display = 'grid';
    grid.style.gridTemplateColumns = 'repeat(3, 1fr)';
    grid.style.gap = '16px';
    
    for (let i = 0; i < 6; i++) {
      const card = this.addElement({
        width: '100%',
        height: 200,
        borderRadius: this.config.borderRadius
      });
      grid.appendChild(card);
    }
    
    this.container.appendChild(grid);
  }
  
  /**
   * Create a dashboard skeleton
   */
  private createDashboardLayout(): void {
    // Header with title and actions
    const header = document.createElement('div');
    header.style.display = 'flex';
    header.style.justifyContent = 'space-between';
    header.style.alignItems = 'center';
    header.style.marginBottom = '24px';
    
    // Title
    const title = this.addElement({
      width: 200,
      height: 32,
      marginBottom: 0
    });
    header.appendChild(title);
    
    // Actions
    const actions = document.createElement('div');
    actions.style.display = 'flex';
    
    for (let i = 0; i < 2; i++) {
      const action = this.addElement({
        width: 100,
        height: 40,
        borderRadius: '20px',
        marginBottom: 0
      });
      action.style.marginLeft = '16px';
      actions.appendChild(action);
    }
    
    header.appendChild(actions);
    this.container.appendChild(header);
    
    // Stats cards
    const statsRow = document.createElement('div');
    statsRow.style.display = 'flex';
    statsRow.style.marginBottom = '24px';
    
    for (let i = 0; i < 4; i++) {
      const card = document.createElement('div');
      card.style.flex = '1';
      card.style.margin = '0 8px';
      card.style.padding = '16px';
      card.style.borderRadius = this.config.borderRadius;
      card.style.border = '1px solid #e0e0e0';
      
      const value = this.addElement({
        width: '60%',
        height: 40,
        marginBottom: 8
      });
      
      const label = this.addElement({
        width: '80%',
        height: 16,
        marginBottom: 0
      });
      
      card.appendChild(value);
      card.appendChild(label);
      statsRow.appendChild(card);
    }
    
    this.container.appendChild(statsRow);
    
    // Charts row
    const chartsRow = document.createElement('div');
    chartsRow.style.display = 'flex';
    chartsRow.style.marginBottom = '24px';
    
    // Line chart
    const lineChart = document.createElement('div');
    lineChart.style.flex = '2';
    lineChart.style.marginRight = '16px';
    lineChart.style.border = '1px solid #e0e0e0';
    lineChart.style.borderRadius = this.config.borderRadius;
    lineChart.style.padding = '16px';
    
    // Chart title
    const chartTitle = this.addElement({
      width: '50%',
      height: 24,
      marginBottom: 16
    });
    lineChart.appendChild(chartTitle);
    
    // Chart content
    const chartContent = this.addElement({
      width: '100%',
      height: 200,
      marginBottom: 0
    });
    lineChart.appendChild(chartContent);
    
    // Pie chart
    const pieChart = document.createElement('div');
    pieChart.style.flex = '1';
    pieChart.style.border = '1px solid #e0e0e0';
    pieChart.style.borderRadius = this.config.borderRadius;
    pieChart.style.padding = '16px';
    
    // Chart title
    const pieTitle = this.addElement({
      width: '50%',
      height: 24,
      marginBottom: 16
    });
    pieChart.appendChild(pieTitle);
    
    // Pie content
    const pieContent = this.addElement({
      width: 150,
      height: 150,
      borderRadius: '50%',
      marginBottom: 16
    });
    pieContent.style.margin = '0 auto 16px auto';
    pieChart.appendChild(pieContent);
    
    // Legend
    for (let i = 0; i < 3; i++) {
      const legend = document.createElement('div');
      legend.style.display = 'flex';
      legend.style.alignItems = 'center';
      legend.style.marginBottom = '8px';
      
      const dot = this.addElement({
        width: 16,
        height: 16,
        borderRadius: '50%',
        marginBottom: 0
      });
      dot.style.marginRight = '8px';
      
      const label = this.addElement({
        width: '60%',
        height: 16,
        marginBottom: 0
      });
      
      legend.appendChild(dot);
      legend.appendChild(label);
      pieChart.appendChild(legend);
    }
    
    chartsRow.appendChild(lineChart);
    chartsRow.appendChild(pieChart);
    this.container.appendChild(chartsRow);
    
    // Table
    this.addTable({
      rows: 5,
      columns: 5
    });
  }
  
  /**
   * Create an article skeleton
   */
  private createArticleLayout(): void {
    // Header
    const header = this.addTextBlock({
      lines: 1,
      lineHeight: 48,
      fontSize: '2em',
      spacing: 24,
      variant: 'heading'
    });
    
    // Meta info
    const meta = document.createElement('div');
    meta.style.display = 'flex';
    meta.style.alignItems = 'center';
    meta.style.marginBottom = '24px';
    
    const avatar = this.addElement({
      width: 40,
      height: 40,
      borderRadius: '50%',
      marginBottom: 0
    });
    avatar.style.marginRight = '12px';
    
    const info = document.createElement('div');
    
    const author = this.addElement({
      width: 120,
      height: 16,
      marginBottom: 4
    });
    
    const date = this.addElement({
      width: 80,
      height: 14,
      marginBottom: 0
    });
    
    info.appendChild(author);
    info.appendChild(date);
    
    meta.appendChild(avatar);
    meta.appendChild(info);
    this.container.appendChild(meta);
    
    // Featured image
    this.addImagePlaceholder({
      height: 300,
      borderRadius: '8px'
    });
    
    // Content
    for (let i = 0; i < 3; i++) {
      this.addTextBlock({
        lines: 4,
        lineHeight: 20,
        spacing: 12
      });
    }
    
    // Subheading
    this.addTextBlock({
      lines: 1,
      lineHeight: 32,
      spacing: 16,
      variant: 'heading'
    });
    
    // More content
    for (let i = 0; i < 2; i++) {
      this.addTextBlock({
        lines: 3,
        lineHeight: 20,
        spacing: 12
      });
    }
    
    // Quote
    const quote = document.createElement('div');
    quote.style.margin = '24px 0';
    quote.style.padding = '16px 32px';
    quote.style.borderLeft = `4px solid ${this.config.baseColor}`;
    
    this.addTextBlock({
      lines: 2,
      lineHeight: 24,
      spacing: 12
    }).forEach(el => quote.appendChild(el));
    
    this.container.appendChild(quote);
    
    // Final paragraphs
    this.addTextBlock({
      lines: 3,
      lineHeight: 20,
      spacing: 12
    });
  }
  
  /**
   * Create a feed layout
   */
  private createFeedLayout(): void {
    // Stories/highlights row
    const stories = document.createElement('div');
    stories.style.display = 'flex';
    stories.style.overflowX = 'hidden';
    stories.style.marginBottom = '24px';
    
    for (let i = 0; i < 6; i++) {
      const story = document.createElement('div');
      story.style.marginRight = '16px';
      story.style.display = 'flex';
      story.style.flexDirection = 'column';
      story.style.alignItems = 'center';
      
      const circle = this.addElement({
        width: 64,
        height: 64,
        borderRadius: '50%',
        marginBottom: 8
      });
      
      const label = this.addElement({
        width: 64,
        height: 16,
        marginBottom: 0
      });
      
      story.appendChild(circle);
      story.appendChild(label);
      stories.appendChild(story);
    }
    
    this.container.appendChild(stories);
    
    // Feed items
    for (let i = 0; i < 3; i++) {
      const post = document.createElement('div');
      post.style.marginBottom = '24px';
      post.style.border = '1px solid #e0e0e0';
      post.style.borderRadius = this.config.borderRadius;
      
      // Header
      const header = document.createElement('div');
      header.style.display = 'flex';
      header.style.alignItems = 'center';
      header.style.padding = '12px';
      
      const avatar = this.addElement({
        width: 40,
        height: 40,
        borderRadius: '50%',
        marginBottom: 0
      });
      avatar.style.marginRight = '12px';
      
      const username = this.addElement({
        width: 120,
        height: 16,
        marginBottom: 0
      });
      
      header.appendChild(avatar);
      header.appendChild(username);
      post.appendChild(header);
      
      // Image
      const image = this.addElement({
        width: '100%',
        height: 300,
        marginBottom: 0
      });
      post.appendChild(image);
      
      // Actions
      const actions = document.createElement('div');
      actions.style.display = 'flex';
      actions.style.padding = '12px';
      
      for (let j = 0; j < 3; j++) {
        const action = this.addElement({
          width: 24,
          height: 24,
          marginBottom: 0
        });
        action.style.marginRight = '16px';
        actions.appendChild(action);
      }
      
      post.appendChild(actions);
      
      // Caption
      const caption = document.createElement('div');
      caption.style.padding = '0 12px 12px 12px';
      
      this.addTextBlock({
        lines: 2,
        lineHeight: 16,
        spacing: 8
      }).forEach(el => caption.appendChild(el));
      
      post.appendChild(caption);
      this.container.appendChild(post);
    }
  }
  
  /**
   * Hide the skeleton and reveal the actual content
   * @param content Content to show (optional)
   * @param fadeOut Whether to fade out the skeleton (default: true)
   */
  public hide(content?: HTMLElement | DocumentFragment, fadeOut: boolean = true): void {
    if (fadeOut) {
      // Add fade out transition
      const transition = 'opacity 0.3s ease-out';
      this.elements.forEach(element => {
        element.style.transition = transition;
        element.style.opacity = '0';
      });
      
      // Wait for transition to complete
      setTimeout(() => {
        this.clear();
        
        // Show content if provided
        if (content) {
          if (content instanceof DocumentFragment) {
            this.container.appendChild(content);
          } else {
            this.container.appendChild(content);
          }
        }
      }, 300);
    } else {
      this.clear();
      
      // Show content if provided
      if (content) {
        if (content instanceof DocumentFragment) {
          this.container.appendChild(content);
        } else {
          this.container.appendChild(content);
        }
      }
    }
  }
  
  /**
   * Clear the skeleton elements
   */
  public clear(): void {
    // Remove all elements
    this.elements.forEach(element => {
      if (element.parentNode) {
        element.parentNode.removeChild(element);
      }
    });
    
    this.elements = [];
    
    // Remove SVG elements
    this.svgElements.forEach(element => {
      if (element.parentNode) {
        element.parentNode.removeChild(element);
      }
    });
    
    this.svgElements = [];
    
    // Remove classes from container
    this.container.classList.remove('skeleton-container');
    this.container.classList.remove(this.uniqueClassName);
  }
  
  /**
   * Destroy the skeleton screen and clean up resources
   */
  public destroy(): void {
    this.clear();
    
    // Remove style element
    if (this.styleElement && this.styleElement.parentNode) {
      this.styleElement.parentNode.removeChild(this.styleElement);
    }
    
    this.styleElement = null;
  }
}

/**
 * Create a skeleton screen with common layouts
 * @param container Container element
 * @param type Layout type
 * @param config Configuration options
 */
export function createSkeletonScreen(
  container: HTMLElement,
  type: 'card' | 'list' | 'table' | 'profile' | 'dashboard' | 'article' | 'feed' = 'card',
  config: SkeletonConfig = {}
): SkeletonScreen {
  const skeleton = new SkeletonScreen(container, config);
  
  switch (type) {
    case 'card':
      skeleton.addCard();
      break;
    case 'list':
      skeleton.addList();
      break;
    case 'table':
      skeleton.addTable();
      break;
    default:
      skeleton.addLayout(type as any);
      break;
  }
  
  return skeleton;
}
