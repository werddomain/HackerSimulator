# Responsive Design Patterns and Best Practices

This document outlines the responsive design patterns and best practices used in the HackerSimulator project to ensure consistency across mobile and desktop platforms.

## 1. Responsive Layout Examples and Templates

### Fluid Container Template

Use this template for content that should scale with the viewport but maintain maximum readability:

```less
.fluid-container {
  width: 100%;
  padding: 0 1rem;
  margin: 0 auto;
  
  @media (min-width: 30em) {
    padding: 0 1.5rem;
  }
  
  @media (min-width: 48em) {
    padding: 0 2rem;
    max-width: 46em;
  }
  
  @media (min-width: 62em) {
    max-width: 60em;
  }
  
  @media (min-width: 80em) {
    max-width: 76em;
  }
}
```

### Card Grid Template

Use this pattern for displaying multiple items in a grid that adapts to screen size:

```less
.card-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 1rem;
  width: 100%;
  
  // Single column on very small screens
  @media (max-width: 320px) {
    grid-template-columns: 1fr;
  }
  
  .card {
    background: var(--card-background);
    border-radius: 8px;
    padding: 1rem;
    box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
    
    // Stack card content on mobile
    display: flex;
    flex-direction: column;
    
    // Add horizontal layout on larger screens if needed
    @media (min-width: 62em) {
      &.horizontal {
        flex-direction: row;
        
        .card-media {
          flex: 0 0 240px;
          margin-right: 1rem;
        }
        
        .card-content {
          flex: 1;
        }
      }
    }
  }
}
```

### Split View Template

For layouts that have a sidebar and main content area:

```less
.split-view {
  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100%;
  
  .sidebar {
    // Full-width by default on mobile
    width: 100%;
    height: auto;
    
    // Hide by default on mobile if not the primary content
    &.collapsible {
      display: none;
      
      &.visible {
        display: block;
      }
    }
  }
  
  .main-content {
    flex: 1;
    width: 100%;
    overflow: auto;
  }
  
  // Switch to side-by-side on larger screens
  @media (min-width: 48em) {
    flex-direction: row;
    
    .sidebar {
      width: 300px;
      height: 100%;
      overflow: auto;
      
      &.collapsible {
        display: block;
      }
    }
    
    .main-content {
      flex: 1;
    }
  }
}
```

### Full Screen App Template

For app layouts that need to use the full viewport:

```less
.fullscreen-app {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  display: flex;
  flex-direction: column;
  
  .app-header {
    height: 56px;
    display: flex;
    align-items: center;
    padding: 0 1rem;
    background: var(--header-background);
    color: var(--header-text);
    z-index: 10;
  }
  
  .app-content {
    flex: 1;
    overflow: auto;
    -webkit-overflow-scrolling: touch; // Smooth scrolling on iOS
  }
  
  .app-footer {
    height: 56px;
    display: flex;
    align-items: center;
    justify-content: space-around;
    background: var(--footer-background);
    color: var(--footer-text);
    z-index: 10;
  }
  
  // Adjust for larger screens
  @media (min-width: 62em) {
    .app-header {
      height: 64px;
    }
    
    .app-content {
      padding: 1rem;
    }
  }
}
```

## 2. Breakpoint Usage and Implementation Guidelines

### Standard Breakpoints

HackerSimulator uses these standard breakpoints throughout the application:

| Name | Size (em) | Size (px) | Description |
|------|-----------|-----------|-------------|
| xs | < 30em | < 480px | Extra small devices (small phones) |
| sm | 30em - 48em | 480px - 768px | Small devices (large phones) |
| md | 48em - 62em | 768px - 992px | Medium devices (tablets) |
| lg | 62em - 80em | 992px - 1280px | Large devices (desktops) |
| xl | > 80em | > 1280px | Extra large devices (large desktops) |

### Media Query Mixins

Use these LESS mixins to maintain consistent breakpoint usage:

```less
// Breakpoint mixins
.media-xs() {
  @media (max-width: 29.9375em) {
    @content;
  }
}

.media-sm() {
  @media (min-width: 30em) and (max-width: 47.9375em) {
    @content;
  }
}

.media-md() {
  @media (min-width: 48em) and (max-width: 61.9375em) {
    @content;
  }
}

.media-lg() {
  @media (min-width: 62em) and (max-width: 79.9375em) {
    @content;
  }
}

.media-xl() {
  @media (min-width: 80em) {
    @content;
  }
}

// Usage direction mixins
.media-up(@breakpoint) {
  & when (@breakpoint = sm) {
    @media (min-width: 30em) { @content; }
  }
  & when (@breakpoint = md) {
    @media (min-width: 48em) { @content; }
  }
  & when (@breakpoint = lg) {
    @media (min-width: 62em) { @content; }
  }
  & when (@breakpoint = xl) {
    @media (min-width: 80em) { @content; }
  }
}

.media-down(@breakpoint) {
  & when (@breakpoint = xs) {
    @media (max-width: 29.9375em) { @content; }
  }
  & when (@breakpoint = sm) {
    @media (max-width: 47.9375em) { @content; }
  }
  & when (@breakpoint = md) {
    @media (max-width: 61.9375em) { @content; }
  }
  & when (@breakpoint = lg) {
    @media (max-width: 79.9375em) { @content; }
  }
}
```

### Implementation Guidelines

1. **Always use em-based media queries** for consistent behavior across different browsers and text sizes
2. **Mobile-first approach**: Start with styles for mobile devices, then use `min-width` queries to add styles for larger screens
3. **Use breakpoint mixins** instead of writing media queries directly
4. **Avoid device-specific breakpoints** - focus on content needs rather than specific devices
5. **Test breakpoints by resizing** - confirm that layouts work at all sizes, not just at the defined breakpoints

## 3. Mobile-First Development Workflow

### Development Principles

1. **Start Simple**: Begin with the core functionality and minimal UI
2. **Progressive Enhancement**: Add complexity and features as screen size increases
3. **Content Priority**: Identify and maintain focus on the most important content
4. **Performance First**: Optimize for mobile performance, then enhance for desktop

### Workflow Steps

1. **Content Inventory**:
   - Identify all content needed for the feature
   - Prioritize content from essential to optional

2. **Mobile Design**:
   - Design the mobile UI first
   - Focus on core functionality and essential content
   - Implement simplified interactions

3. **Responsive Expansion**:
   - Scale up to larger breakpoints
   - Introduce additional content and enhanced layouts
   - Add more sophisticated interactions for desktop

4. **Testing**:
   - Test on real mobile devices
   - Verify at each breakpoint
   - Ensure performance meets targets at all sizes

### Example Mobile-First Implementation

```typescript
// Example of a mobile-first component
class ResponsiveComponent {
  constructor(container: HTMLElement) {
    // Base implementation for mobile
    this.renderMobileLayout(container);
    
    // Add resize listener to adapt to larger screens
    const mediaQuery = window.matchMedia('(min-width: 48em)');
    
    // Initial check
    if (mediaQuery.matches) {
      this.enhanceForDesktop(container);
    }
    
    // Listen for changes
    mediaQuery.addEventListener('change', (e) => {
      if (e.matches) {
        this.enhanceForDesktop(container);
      } else {
        this.revertToMobile(container);
      }
    });
  }
  
  private renderMobileLayout(container: HTMLElement): void {
    // Implement mobile layout
    // Focus on core functionality
  }
  
  private enhanceForDesktop(container: HTMLElement): void {
    // Add desktop enhancements
    // Show additional content
    // Enable more complex interactions
  }
  
  private revertToMobile(container: HTMLElement): void {
    // Handle reverting to mobile view if window is resized smaller
  }
}
```

## 4. Responsive Testing Checklist

Use this checklist to verify responsive design implementation:

### Visual Testing

- [ ] Content is fully visible and properly sized at all breakpoints
- [ ] No horizontal scrolling appears on any screen size (unless intentional)
- [ ] Text is readable without zooming (minimum 16px font size)
- [ ] Touch targets meet minimum size requirements (44px × 44px)
- [ ] Images and media scale appropriately
- [ ] Padding and margins adjust properly between breakpoints
- [ ] No content overlaps at any viewport size

### Functionality Testing

- [ ] All interactive elements work at every breakpoint
- [ ] Navigation is accessible and usable at all sizes
- [ ] Forms submit correctly on all devices
- [ ] Modals and dialogs appear correctly positioned
- [ ] Touch interactions work on mobile devices
- [ ] Mouse interactions work on desktop devices
- [ ] Keyboard navigation works consistently

### Performance Testing

- [ ] Page load time meets targets on mobile (under 3 seconds)
- [ ] Animations run smoothly (60fps) on target devices
- [ ] Lazy loading works correctly for off-screen content
- [ ] Memory usage stays within acceptable limits
- [ ] Battery impact is minimized on mobile devices

### Orientation Testing

- [ ] Layout adapts properly when switching between portrait and landscape
- [ ] Content reflows appropriately when orientation changes
- [ ] Fixed elements remain correctly positioned
- [ ] No unnecessary scrolling is introduced

### Cross-Browser Testing

- [ ] Verify functionality in Chrome, Firefox, Safari, and Edge
- [ ] Test on iOS and Android devices
- [ ] Check for browser-specific rendering issues
- [ ] Validate consistent behavior across platforms

## 5. Common Responsive Design Patterns

### Mostly Fluid

- Content reflows and resizes to fit the screen width
- Maintains the same layout but with fluid columns
- Add breakpoints to adjust column widths and spacing

### Column Drop

- Start with stacked columns on mobile
- Columns move side-by-side as screen width increases
- Order columns by importance for mobile stacking

### Layout Shifter

- Different layouts for different screen sizes
- More dramatic layout changes at breakpoints
- Requires more CSS but provides better UX at each size

### Off Canvas

- Hide non-essential content off-screen on mobile
- Reveal off-canvas content with gesture or button
- Display all content on-screen for larger viewports

## 6. Responsive Design Best Practices

### Flexible Images

```css
.responsive-image {
  max-width: 100%;
  height: auto;
}
```

### Fluid Typography

```less
html {
  font-size: 16px;
  
  @media (min-width: 48em) {
    font-size: calc(16px + 0.5vw);
  }
  
  @media (min-width: 80em) {
    font-size: 20px; // Cap the maximum size
  }
}

// Use rem units for consistent scaling
h1 { font-size: 2rem; }
h2 { font-size: 1.75rem; }
h3 { font-size: 1.5rem; }
p { font-size: 1rem; }
```

### Content-Based Breakpoints

Rather than using device-specific breakpoints, add breakpoints where your content needs them:

```css
.content-container {
  display: flex;
  flex-wrap: wrap;
}

.content-item {
  flex: 1 1 100%;
}

/* Add breakpoint when content items can fit side by side comfortably */
@media (min-width: 600px) {
  .content-item {
    flex: 1 1 calc(50% - 1rem);
  }
}

/* Add another breakpoint when content benefits from three columns */
@media (min-width: 900px) {
  .content-item {
    flex: 1 1 calc(33.333% - 1rem);
  }
}
```

### Hidden Content Considerations

When hiding content at different breakpoints:

```css
/* Visually hidden but accessible to screen readers */
.visually-hidden {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}

/* Hidden at specific breakpoints */
@media (max-width: 48em) {
  .hidden-mobile {
    display: none;
  }
}

@media (min-width: 48em) {
  .hidden-desktop {
    display: none;
  }
}
```

## 7. Performance Considerations

### Critical CSS

- Inline critical CSS in the `<head>` for faster rendering
- Load non-critical CSS asynchronously
- Keep critical CSS under 14KB to fit in first TCP packet

### Image Optimization

- Use responsive images with `srcset` and `sizes` attributes
- Serve appropriately sized images based on viewport size
- Use modern formats like WebP with fallbacks

```html
<picture>
  <source srcset="image-small.webp 480w, image-medium.webp 800w, image-large.webp 1200w" 
          sizes="(max-width: 600px) 480px, (max-width: 900px) 800px, 1200px"
          type="image/webp">
  <img src="image-medium.jpg" alt="Description" 
       srcset="image-small.jpg 480w, image-medium.jpg 800w, image-large.jpg 1200w"
       sizes="(max-width: 600px) 480px, (max-width: 900px) 800px, 1200px">
</picture>
```

### Resource Loading

- Lazy load non-critical resources
- Prioritize loading of visible content
- Use `media` attributes on CSS link tags to load device-specific styles

```html
<!-- Mobile-specific styles -->
<link rel="stylesheet" href="mobile.css" media="(max-width: 48em)">

<!-- Desktop-specific styles -->
<link rel="stylesheet" href="desktop.css" media="(min-width: 48em)">
```

## 8. Accessibility in Responsive Design

- Ensure keyboard navigation works at all viewport sizes
- Maintain proper focus management when layouts change
- Use appropriate ARIA attributes for custom responsive components
- Test with screen readers at different viewport sizes
- Ensure sufficient color contrast at all breakpoints

```css
/* Example of maintaining focus visibility across viewports */
:focus {
  outline: 3px solid var(--focus-color);
  outline-offset: 2px;
}

@media (prefers-reduced-motion: reduce) {
  * {
    animation-duration: 0.001s !important;
    transition-duration: 0.001s !important;
  }
}
```

---

By following these responsive design patterns and best practices, the HackerSimulator project will maintain a consistent and high-quality user experience across all devices and viewport sizes.
