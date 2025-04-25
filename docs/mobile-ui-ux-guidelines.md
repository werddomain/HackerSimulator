# Mobile UI/UX Guidelines for HackerSimulator

This document provides comprehensive guidelines for developing mobile-friendly components and interfaces for the HackerSimulator project. These standards ensure a consistent and high-quality user experience across all mobile implementations.

## 1. Touch Target Sizing

Touch targets must be appropriately sized to ensure users can interact with them accurately on mobile devices.

### Requirements:

- **Minimum touch target size**: 44px × 44px for all interactive elements
- **Recommended touch target size**: 48px × 48px or larger for primary actions
- **Touch target spacing**: Minimum 8px between adjacent touch targets
- **Hit area expansion**: When visual elements are smaller than 44px, expand the hit area using padding or transparent borders

### Implementation:

```css
/* Example of proper touch target sizing */
.touch-button {
  min-width: 44px;
  min-height: 44px;
  padding: 12px;
  margin: 4px;
}

/* For smaller visual elements with expanded hit area */
.small-icon-button {
  width: 24px;
  height: 24px;
  padding: 12px; /* Creates a 48px hit target */
}
```

## 2. Gesture Interaction Standards

The following standard gestures should be implemented consistently across the application:

### Basic Gestures:

| Gesture | Action | Implementation |
|---------|--------|----------------|
| Tap | Primary action or selection | Used for buttons, links, and list items |
| Double tap | Zoom in/out or special action | Used for images or maps |
| Long press | Context menu or secondary actions | Used for additional options |
| Swipe (horizontal) | Navigation, reveal actions | Used for navigation between views, revealing delete/edit actions |
| Swipe (vertical) | Scroll content | Used for scrolling lists and content |
| Pinch | Zoom in/out | Used for images, maps, or documents |
| Rotate | Rotate content | Used for images or specialized content |

### Application-Specific Gestures:

| App | Gesture | Action |
|-----|---------|--------|
| Terminal | Swipe left | Access command history |
| File Explorer | Swipe down | Refresh directory |
| Browser | Swipe right/left | Navigate back/forward |
| Code Editor | Pinch | Zoom code text |
| System Monitor | Swipe left/right | Switch between system metrics |

### Implementation Guidelines:

- Use the `GestureDetector` class for all gesture implementations
- Provide visual feedback for all gestures (animations, highlights)
- Include haptic feedback when available on the device
- Ensure gestures don't conflict with browser/system gestures
- Implement a fallback UI method for all gesture-based actions

## 3. Mobile Layout Best Practices

### Viewport Configuration:

```html
<meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=5.0, user-scalable=yes">
```

### Layout Hierarchy:

1. **Primary Content Focus**:
   - Present the most important content first
   - Minimize chrome/UI elements that aren't essential
   - Use full width of the screen for content where possible

2. **Navigation Patterns**:
   - Use bottom navigation for primary app navigation
   - Implement tabs for within-app section navigation
   - Use modals sparingly for focused tasks

3. **Form Design**:
   - Place labels above input fields, not beside them
   - Group related fields visually
   - Use appropriate input types (email, tel, number, etc.)
   - Implement autofill and autocomplete where possible
   - Display validation feedback inline

### Responsive Layout Techniques:

- Use CSS Grid and Flexbox for fluid layouts
- Implement appropriate breakpoints (see Breakpoint System below)
- Define content priority for different viewport sizes
- Optimize for both portrait and landscape orientations
- Ensure content is readable without zooming

```css
/* Example of mobile-first responsive layout */
.container {
  display: flex;
  flex-direction: column;
  padding: 1rem;
}

/* Tablet and larger */
@media (min-width: 48em) {
  .container {
    flex-direction: row;
    flex-wrap: wrap;
  }
  
  .container > * {
    flex: 1 1 300px;
  }
}
```

### Breakpoint System:

| Breakpoint Name | Size (em) | Size (px) | Typical Device |
|-----------------|-----------|-----------|----------------|
| xs | < 30em | < 480px | Small phone |
| sm | 30em - 48em | 480px - 768px | Large phone |
| md | 48em - 62em | 768px - 992px | Tablet |
| lg | 62em - 80em | 992px - 1280px | Desktop |
| xl | > 80em | > 1280px | Large desktop |

## 4. Mobile Component Usage Rules

### Window Components:

- **Mobile Window**: Always use fullscreen for active application
- **Dialog**: Center on screen, use at least 1rem padding
- **Alerts**: Should be dismissible by tapping overlay or close button

### Navigation Components:

- **Bottom Bar**: Main navigation component on mobile, 5 items maximum
- **Tabs**: Secondary navigation, swipeable, with visible indicator
- **Back Button**: Always provide a visible back/close option

### Input Components:

- **Text Input**: Full width, clear button when text is present
- **Buttons**: Minimum 44px height, descriptive labels
- **Toggle/Switch**: Accompany with clear label, provide visual state
- **Checkbox/Radio**: Minimum 44px tap area, provide visual feedback

### Content Components:

- **Lists**: Support swipe actions when applicable
- **Cards**: Use for discrete content blocks, support tap for details
- **Images**: Lazy load, provide loading state, support pinch-zoom

### Example Implementation:

```typescript
// Example of proper mobile button implementation
class MobileButton {
  constructor(element: HTMLElement) {
    // Ensure minimum size
    element.style.minHeight = '44px';
    element.style.minWidth = '44px';
    
    // Add touch feedback
    element.addEventListener('touchstart', () => {
      element.classList.add('active');
    });
    
    element.addEventListener('touchend', () => {
      element.classList.remove('active');
      // Add small delay to ensure user sees the feedback
      setTimeout(() => {
        // Perform action
      }, 50);
    });
    
    // Add support for haptic feedback if available
    if ('vibrate' in navigator) {
      element.addEventListener('click', () => {
        navigator.vibrate(10);
      });
    }
  }
}
```

## 5. Accessibility Considerations

- Ensure color contrast ratios of at least 4.5:1 for text
- Support dynamic text sizing for users with visual impairments
- Implement proper ARIA roles and labels for custom UI components
- Provide alternative input methods for gesture-based interactions
- Support screen readers by using semantic HTML and ARIA attributes

## 6. Performance Guidelines

- Keep total page weight under 1MB for initial load
- Aim for Time to Interactive (TTI) under 5 seconds on 3G
- Lazy load non-critical components and resources
- Optimize animations for 60fps on mid-range devices
- Implement debouncing and throttling for resource-intensive operations

## 7. Testing Requirements

All mobile UI implementations must be tested on:

- At least 3 different physical device sizes
- Both portrait and landscape orientations
- Low-end and high-end device configurations
- Touch and keyboard input methods
- Various network conditions (wifi, 4G, 3G)

## 8. Common Pitfalls to Avoid

- Touch targets too small or too close together
- Relying on hover states for important functionality
- Horizontal scrolling for primary content
- Keyboard covering input fields without adjustment
- Long load times without feedback
- Assuming all devices support the same touch events

## 9. Resources and Tools

- **Design Inspection**: Use Chrome DevTools Device Mode
- **Testing**: BrowserStack or real device testing lab
- **Performance**: Lighthouse for performance metrics
- **Gestures**: Use the HackerSimulator `GestureDetector` class
- **Components**: Refer to the Mobile component library

---

These guidelines should be followed for all mobile implementations within the HackerSimulator project. Any exceptions must be documented and approved by the development team lead.
