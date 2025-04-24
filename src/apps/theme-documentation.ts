import { getPropertyHelp, getCategoryHelp, CategoryHelp, PropertyHelp } from './theme-editor-help';

/**
 * Theme Documentation Component
 * Displays documentation for theme properties with examples and CSS guidance
 */
export class ThemeDocumentation {
  private container: HTMLElement;
  private currentCategory: string | null = null;
  private currentProperty: string | null = null;

  constructor(container: HTMLElement) {
    this.container = container;
    this.init();
  }

  /**
   * Initialize the documentation component
   */
  private init(): void {
    this.render();
  }

  /**
   * Render the documentation container
   */
  private render(): void {
    if (!this.container) return;

    this.container.innerHTML = `
      <div class="theme-documentation">
        <div class="doc-header">
          <h2 class="doc-title">Theme Property Documentation</h2>
          <button class="doc-close-btn">×</button>
        </div>
        <div class="doc-content">
          <div class="doc-navigation">
            <div class="doc-search">
              <input type="text" class="doc-search-input" placeholder="Search properties...">
            </div>
            <div class="doc-category-list"></div>
          </div>
          <div class="doc-details">
            <div class="doc-welcome">
              <h3>Welcome to Theme Documentation</h3>
              <p>Select a property from the list on the left to view its documentation.</p>
              <p>Documentation includes descriptions, examples, and CSS guidance to help you customize themes effectively.</p>
            </div>
          </div>
        </div>
      </div>
    `;

    // Set up event listeners
    this.setupEventListeners();
    
    // Load categories
    this.loadCategories();
  }

  /**
   * Set up event listeners
   */
  private setupEventListeners(): void {
    const closeBtn = this.container.querySelector('.doc-close-btn');
    const searchInput = this.container.querySelector<HTMLInputElement>('.doc-search-input');
    
    closeBtn?.addEventListener('click', () => {
      this.hide();
    });
    
    searchInput?.addEventListener('input', () => {
      this.filterProperties(searchInput.value);
    });
  }

  /**
   * Load category list
   */
  private loadCategories(): void {
    const categoryList = this.container.querySelector('.doc-category-list');
    if (!categoryList) return;
    
    // Import categorized documentation
    import('./theme-editor-help').then(module => {
      const docs = module.themeHelpDocs;
      
      // Create category list
      docs.forEach(category => {
        const categoryEl = document.createElement('div');
        categoryEl.className = 'doc-category';
        categoryEl.innerHTML = `
          <div class="doc-category-header">
            <span class="doc-category-name">${category.name}</span>
            <span class="doc-category-toggle">+</span>
          </div>
          <div class="doc-property-list" style="display: none;"></div>
        `;
        
        // Add click handler to toggle category
        const header = categoryEl.querySelector('.doc-category-header');
        const propertyList = categoryEl.querySelector('.doc-property-list');
        const toggle = categoryEl.querySelector('.doc-category-toggle');
        
        header?.addEventListener('click', () => {
          const isExpanded = propertyList && (propertyList as HTMLElement).style.display !== 'none';
          if (propertyList) (propertyList as HTMLElement).style.display = isExpanded ? 'none' : 'block';
          if (toggle) toggle.textContent = isExpanded ? '+' : '-';
        });
        
        // Add properties to the list
        if (propertyList) {
          category.properties.forEach(property => {
            const propertyEl = document.createElement('div');
            propertyEl.className = 'doc-property';
            propertyEl.textContent = property.name;
            propertyEl.addEventListener('click', (e) => {
              e.stopPropagation();
              this.showPropertyDetails(property.name);
            });
            
            propertyList.appendChild(propertyEl);
          });
        }
        
        categoryList.appendChild(categoryEl);
      });
    });
  }

  /**
   * Show documentation for a specific property
   */
  public showPropertyDetails(propertyName: string): void {
    this.currentProperty = propertyName;
    this.currentCategory = null;
    
    // Highlight selected property
    this.updateSelectionState();
    
    // Get property details
    const property = getPropertyHelp(propertyName);
    if (!property) return;
    
    const detailsContainer = this.container.querySelector('.doc-details');
    if (!detailsContainer) return;
    
    detailsContainer.innerHTML = `
      <div class="doc-property-details">
        <h3 class="doc-property-name">${property.name}</h3>
        <div class="doc-property-description">${property.description}</div>
        
        ${property.examples ? `
        <div class="doc-property-examples">
          <h4>Examples</h4>
          <div class="doc-example-values">
            ${property.examples.map(example => `
              <span class="doc-example-value">${example}</span>
            `).join('')}
          </div>
        </div>
        ` : ''}
        
        ${property.cssProperty ? `
        <div class="doc-css-property">
          <h4>CSS Variable</h4>
          <code>${property.cssProperty}</code>
        </div>
        ` : ''}
        
        ${property.cssExample ? `
        <div class="doc-css-example">
          <h4>CSS Usage Example</h4>
          <pre><code>${property.cssExample}</code></pre>
        </div>
        ` : ''}
        
        ${property.relatedProperties ? `
        <div class="doc-related-properties">
          <h4>Related Properties</h4>
          <ul>
            ${property.relatedProperties.map(related => `
              <li><a href="#" class="doc-related-link" data-property="${related}">${related}</a></li>
            `).join('')}
          </ul>
        </div>
        ` : ''}
      </div>
    `;
    
    // Set up related property links
    const relatedLinks = detailsContainer.querySelectorAll('.doc-related-link');
    relatedLinks.forEach(link => {
      link.addEventListener('click', (e) => {
        e.preventDefault();
        const relatedProperty = (link as HTMLElement).getAttribute('data-property');
        if (relatedProperty) {
          this.showPropertyDetails(relatedProperty);
        }
      });
    });
  }

  /**
   * Show documentation for a specific category
   */
  public showCategoryDetails(categoryName: string): void {
    this.currentCategory = categoryName;
    this.currentProperty = null;
    
    // Highlight selected category
    this.updateSelectionState();
    
    // Get category details
    const category = getCategoryHelp(categoryName);
    if (!category) return;
    
    const detailsContainer = this.container.querySelector('.doc-details');
    if (!detailsContainer) return;
    
    detailsContainer.innerHTML = `
      <div class="doc-category-details">
        <h3 class="doc-category-name">${category.name}</h3>
        <div class="doc-category-description">${category.description}</div>
        
        <div class="doc-category-properties">
          <h4>Properties</h4>
          <div class="doc-category-property-grid">
            ${category.properties.map(property => `
              <div class="doc-category-property-card">
                <h5 class="doc-property-card-name">${property.name}</h5>
                <p class="doc-property-card-description">${property.description}</p>
                <button class="doc-property-card-button" data-property="${property.name}">View Details</button>
              </div>
            `).join('')}
          </div>
        </div>
      </div>
    `;
    
    // Set up property detail buttons
    const propertyButtons = detailsContainer.querySelectorAll('.doc-property-card-button');
    propertyButtons.forEach(button => {
      button.addEventListener('click', () => {
        const propertyName = (button as HTMLElement).getAttribute('data-property');
        if (propertyName) {
          this.showPropertyDetails(propertyName);
        }
      });
    });
  }

  /**
   * Update the selection state in the navigation
   */
  private updateSelectionState(): void {
    // Remove current selection
    const selectedItems = this.container.querySelectorAll('.doc-property.selected, .doc-category-header.selected');
    selectedItems.forEach(item => {
      item.classList.remove('selected');
    });
    
    // Add selection to current property
    if (this.currentProperty) {
      const propertyElements = this.container.querySelectorAll('.doc-property');
      propertyElements.forEach(el => {
        if (el.textContent === this.currentProperty) {
          el.classList.add('selected');
          
          // Ensure parent category is expanded
          const propertyList = el.parentElement;
          if (propertyList) {
            (propertyList as HTMLElement).style.display = 'block';
            const categoryHeader = propertyList.previousElementSibling;
            const toggle = categoryHeader?.querySelector('.doc-category-toggle');
            if (toggle) toggle.textContent = '-';
          }
        }
      });
    }
    
    // Add selection to current category
    if (this.currentCategory) {
      const categoryHeaders = this.container.querySelectorAll('.doc-category-header');
      categoryHeaders.forEach(header => {
        const categoryName = header.querySelector('.doc-category-name');
        if (categoryName?.textContent === this.currentCategory) {
          header.classList.add('selected');
          
          // Ensure category is expanded
          const propertyList = header.nextElementSibling;
          if (propertyList) {
            (propertyList as HTMLElement).style.display = 'block';
            const toggle = header.querySelector('.doc-category-toggle');
            if (toggle) toggle.textContent = '-';
          }
        }
      });
    }
  }

  /**
   * Filter properties by search query
   */
  private filterProperties(query: string): void {
    if (!query) {
      // Reset all visibility
      this.container.querySelectorAll('.doc-property').forEach(property => {
        (property as HTMLElement).style.display = 'block';
      });
      
      this.container.querySelectorAll('.doc-category').forEach(category => {
        (category as HTMLElement).style.display = 'block';
      });
      
      return;
    }
    
    query = query.toLowerCase();
    
    // Filter properties
    this.container.querySelectorAll('.doc-property').forEach(property => {
      const propertyName = property.textContent?.toLowerCase() || '';
      (property as HTMLElement).style.display = propertyName.includes(query) ? 'block' : 'none';
    });
    
    // Hide empty categories
    this.container.querySelectorAll('.doc-category').forEach(category => {
      const propertyList = category.querySelector('.doc-property-list');
      const visibleProperties = propertyList?.querySelectorAll('.doc-property[style*="display: block"]');
      
      // Check if category name matches
      const categoryName = category.querySelector('.doc-category-name')?.textContent?.toLowerCase() || '';
      const categoryMatches = categoryName.includes(query);
      
      (category as HTMLElement).style.display = (categoryMatches || (visibleProperties && visibleProperties.length > 0)) ? 'block' : 'none';
      
      // If searching and category matches, expand it
      if (query && categoryMatches && propertyList) {
        (propertyList as HTMLElement).style.display = 'block';
        const toggle = category.querySelector('.doc-category-toggle');
        if (toggle) toggle.textContent = '-';
      }
    });
  }

  /**
   * Show the documentation panel
   */
  public show(): void {
    this.container.style.display = 'block';
  }

  /**
   * Hide the documentation panel
   */
  public hide(): void {
    this.container.style.display = 'none';
  }
}
