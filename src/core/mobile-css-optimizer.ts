// filepath: c:\Users\clefw\source\repos\HackerSimulator\src\core\mobile-css-optimizer.ts
/**
 * Mobile CSS Optimization Module
 * Provides utilities for optimizing CSS rendering performance on mobile devices
 */

/**
 * CSS properties that trigger layout/reflow when changed
 * Avoid animating these properties for better performance
 */
const LAYOUT_THRASHING_PROPERTIES: string[] = [
  'width', 'height', 'margin', 'padding', 'border',
  'display', 'position', 'top', 'right', 'bottom', 'left',
  'float', 'overflow', 'font-size', 'font-family', 'font-weight',
  'text-align', 'vertical-align', 'line-height',
  'white-space', 'word-spacing', 'letter-spacing',
  'min-height', 'min-width', 'max-height', 'max-width'
];

/**
 * CSS properties that are GPU-accelerated and good for animations
 */
const GPU_ACCELERATED_PROPERTIES: string[] = [
  'transform', 'opacity', 'filter'
];

/**
 * Issue found during CSS analysis
 */
interface CSSIssue {
  type: string;
  rule: string;
  properties?: string[];
  index?: number;
  suggestion: string;
}

/**
 * Find sub-optimal CSS rules in a stylesheet
 * @param styleSheet - CSS stylesheet to analyze
 * @returns - List of problematic rules with suggestions
 */
export function analyzeStylesheet(styleSheet: CSSStyleSheet): CSSIssue[] {
  const issues: CSSIssue[] = [];
  
  try {
    const rules = Array.from(styleSheet.cssRules || styleSheet.rules || []);
    
    rules.forEach((rule, index) => {
      // Only process style rules
      if (rule.type === CSSRule.STYLE_RULE) {
        const styleRule = rule as CSSStyleRule;
        const selector = styleRule.selectorText;
        const styleText = styleRule.style.cssText;
        
        // Check if rule uses layout-thrashing properties in animations
        const hasAnimation = styleText.includes('transition') || 
                             styleText.includes('animation') ||
                             selector.includes(':hover');
                             
        if (hasAnimation) {
          // Check if any layout thrashing properties are being animated
          const animatedProperties = LAYOUT_THRASHING_PROPERTIES.filter(prop => 
            styleText.includes(`${prop}:`) && 
            (styleRule.style.getPropertyValue(`transition-property`).includes(prop) ||
             styleRule.style.getPropertyValue(`animation-name`))
          );
          
          if (animatedProperties.length > 0) {
            issues.push({
              type: 'animation-performance',
              rule: selector,
              properties: animatedProperties,
              index,
              suggestion: `Consider using transform/opacity instead of ${animatedProperties.join(', ')} for better performance.`
            });
          }
        }
        
        // Check for expensive selectors
        if (isExpensiveSelector(selector)) {
          issues.push({
            type: 'expensive-selector',
            rule: selector,
            index,
            suggestion: 'This selector may be expensive to compute. Consider simplifying it.'
          });
        }
        
        // Check for !important overrides
        const importantProps: string[] = [];
        for (let i = 0; i < styleRule.style.length; i++) {
          const prop = styleRule.style[i];
          if (styleRule.style.getPropertyPriority(prop) === 'important') {
            importantProps.push(prop);
          }
        }
        
        if (importantProps.length > 0) {
          issues.push({
            type: 'important-override',
            rule: selector,
            properties: importantProps,
            index,
            suggestion: 'Using !important hinders stylesheet performance and maintainability.'
          });
        }
      }
      
      // Check @keyframes for performance
      if (rule.type === CSSRule.KEYFRAMES_RULE) {
        const keyframesRule = rule as CSSKeyframesRule;
        const keyframesRules = Array.from(keyframesRule.cssRules);
        const layoutPropsInKeyframes = new Set<string>();
        
        keyframesRules.forEach(keyframe => {
          if (keyframe.type === CSSRule.KEYFRAME_RULE) {
            const keyframeRule = keyframe as CSSKeyframeRule;
            LAYOUT_THRASHING_PROPERTIES.forEach(prop => {
              if (keyframeRule.style.getPropertyValue(prop)) {
                layoutPropsInKeyframes.add(prop);
              }
            });
          }
        });
        
        if (layoutPropsInKeyframes.size > 0) {
          issues.push({
            type: 'keyframes-performance',
            rule: `@keyframes ${keyframesRule.name}`,
            properties: Array.from(layoutPropsInKeyframes),
            suggestion: 'Animating layout properties in keyframes causes jank. Use transform/opacity instead.'
          });
        }
      }
      
      // Check media queries for mobile optimization
      if (rule.type === CSSRule.MEDIA_RULE) {
        const mediaRule = rule as CSSMediaRule;
        // Look for device-pixel-ratio media queries that might be inefficient
        if (mediaRule.media.mediaText.includes('min-device-pixel-ratio') && 
            !mediaRule.media.mediaText.includes('max-width')) {
          issues.push({
            type: 'media-query-optimization',
            rule: `@media ${mediaRule.media.mediaText}`,
            suggestion: 'Consider combining pixel ratio queries with max-width for better mobile performance.'
          });
        }
      }
    });
  } catch (error) {
    console.error('Error analyzing stylesheet:', error);
  }
  
  return issues;
}

/**
 * Check if a selector is expensive to compute
 * @param selector - CSS selector to check
 * @returns - Whether the selector is expensive
 */
function isExpensiveSelector(selector: string): boolean {
  if (!selector) return false;
  
  // Universal selectors are expensive
  if (selector.includes('*')) return true;
  
  // Many pseudo-classes/elements can be expensive
  const expensivePseudos = [':nth-child', ':nth-of-type', ':nth-last-child', ':not'];
  if (expensivePseudos.some(pseudo => selector.includes(pseudo))) return true;
  
  // Deep descendant selectors are expensive
  const descendantCount = (selector.match(/ /g) || []).length;
  if (descendantCount > 3) return true;
  
  // Many attribute selectors can be expensive
  const attrSelectorCount = (selector.match(/\[/g) || []).length;
  if (attrSelectorCount > 2) return true;
  
  return false;
}

/**
 * Optimize a CSS rule for mobile performance
 * @param cssText - CSS rule to optimize
 * @returns - Optimized CSS rule
 */
export function optimizeCSSRule(cssText: string): string {
  let optimizedCSS = cssText;
  
  // Replace layout-thrashing animations with transform equivalents
  LAYOUT_THRASHING_PROPERTIES.forEach(prop => {
    if (prop === 'width' || prop === 'height') {
      // Replace width/height animations with transform: scale()
      const regex = new RegExp(`transition([^;]*)(${prop})`, 'g');
      optimizedCSS = optimizedCSS.replace(regex, 'transition$1transform');
      
      // Add a comment explaining the optimization
      if (optimizedCSS !== cssText) {
        optimizedCSS += '/* Optimized: Use transform:scale() instead of animating width/height */';
      }
    } else if (prop === 'top' || prop === 'left' || prop === 'right' || prop === 'bottom') {
      // Replace position animations with transform: translate()
      const regex = new RegExp(`transition([^;]*)(${prop})`, 'g');
      optimizedCSS = optimizedCSS.replace(regex, 'transition$1transform');
      
      // Add a comment explaining the optimization
      if (optimizedCSS !== cssText) {
        optimizedCSS += '/* Optimized: Use transform:translate() instead of animating position */';
      }
    }
  });
  
  // Add will-change for animated elements
  if ((optimizedCSS.includes('transition') || optimizedCSS.includes('animation')) && 
     !optimizedCSS.includes('will-change')) {
    // Determine appropriate will-change value
    let willChangeValue = 'transform, opacity';
    
    // Check which properties are being animated
    GPU_ACCELERATED_PROPERTIES.forEach(prop => {
      if (optimizedCSS.includes(`${prop}:`)) {
        willChangeValue = prop;
      }
    });
    
    // Add will-change property
    optimizedCSS = optimizedCSS.replace(
      /([^}]*)(})/, 
      `$1\n  will-change: ${willChangeValue};$2\n  /* Added will-change for better performance */`
    );
  }
  
  return optimizedCSS;
}

/**
 * Generate a critical CSS subset for mobile devices
 * @param rules - CSS rules to process
 * @param rootElement - Root element to check for usage
 * @returns - Critical CSS string
 */
export function generateCriticalCSS(rules: CSSRule[], rootElement: HTMLElement = document.body): string[] {
  const criticalCSS: string[] = [];
  
  // Check if selectors are used in the current view
  rules.forEach(rule => {
    if (rule.type === CSSRule.STYLE_RULE) {
      const styleRule = rule as CSSStyleRule;
      try {
        // Check if any elements match this rule
        const matchedElements = rootElement.querySelectorAll(styleRule.selectorText);
          if (matchedElements.length > 0) {
          // Check if matched elements are in the viewport
          const inViewport = Array.from(matchedElements).some(el => isElementInViewport(el as HTMLElement));
          
          if (inViewport) {
            criticalCSS.push(styleRule.cssText);
          }
        }
      } catch (e) {
        // Some selectors might not be valid in querySelectorAll
        console.warn('Error processing selector:', styleRule.selectorText, e);
      }
    } else if (rule.type === CSSRule.MEDIA_RULE) {
      const mediaRule = rule as CSSMediaRule;
      // Include mobile-specific media queries
      if (mediaRule.media.mediaText.includes('max-width') || 
          mediaRule.media.mediaText.includes('orientation') ||
          mediaRule.media.mediaText.includes('hover: none')) {
        
        const nestedRules = Array.from(mediaRule.cssRules);
        const criticalRules = generateCriticalCSS(nestedRules, rootElement);
        
        if (criticalRules.length > 0) {
          criticalCSS.push(`@media ${mediaRule.media.mediaText} {
            ${criticalRules.join('\n')}
          }`);
        }
      }
    } else if (rule.type === CSSRule.FONT_FACE_RULE || 
               rule.type === CSSRule.KEYFRAMES_RULE ||
               rule.type === CSSRule.IMPORT_RULE) {
      // Always include these rule types in critical CSS
      criticalCSS.push(rule.cssText);
    }
  });
  
  return criticalCSS;
}

/**
 * Check if an element is in the viewport
 * @param element - Element to check
 * @returns - Whether element is in viewport
 */
function isElementInViewport(element: HTMLElement): boolean {
  const rect = element.getBoundingClientRect();
  return (
    rect.top >= 0 &&
    rect.left >= 0 &&
    rect.bottom <= (window.innerHeight || document.documentElement.clientHeight) &&
    rect.right <= (window.innerWidth || document.documentElement.clientWidth)
  );
}

/**
 * Create CSS overrides for better mobile performance
 * @returns - CSS overrides for mobile performance
 */
export function createMobilePerformanceCSS(): string {
  return `
    /* Mobile CSS Performance Optimizations */
    
    /* Enable momentum scrolling on iOS */
    * {
      -webkit-overflow-scrolling: touch;
    }
    
    /* Reduce repaints for fixed elements */
    .fixed, 
    [style*="position: fixed"],
    .header, 
    .footer, 
    .navigation {
      will-change: transform;
      transform: translateZ(0);
    }
    
    /* Optimize transitions and animations */
    .animate, 
    .transition, 
    [class*="fade"], 
    [class*="slide"] {
      will-change: transform, opacity;
      transform: translateZ(0);
    }
    
    /* Prevent expensive hover styles on touch devices */
    @media (hover: none) {
      /* Reset hover styles on touch devices */
      [class*="hover"],
      [class*="-hover"] {
        transition: none !important;
      }
    }
    
    /* Optimize image rendering on mobile */
    img, 
    canvas, 
    video {
      image-rendering: auto;
    }
    
    /* Reduce paint complexity for scrolling areas */
    .scroll-container,
    .overflow-auto,
    .overflow-scroll,
    [style*="overflow: auto"],
    [style*="overflow: scroll"],
    [style*="overflow-y: auto"],
    [style*="overflow-y: scroll"] {
      will-change: transform;
      -webkit-overflow-scrolling: touch;
    }
    
    /* Optimize rendering layers for large tables */
    table {
      transform: translateZ(0);
    }
    
    /* Make sure fixed elements are properly composited */
    .modal,
    .dialog,
    .popup {
      will-change: transform, opacity;
    }
  `;
}

/**
 * Create inline performance-optimized styles for an element
 * @param element - Element to optimize
 * @returns - Optimized inline styles
 */
export function createOptimizedInlineStyles(element: HTMLElement): string {
  if (!element) return '';
  
  const styles = window.getComputedStyle(element);
  const optimizedStyles: string[] = [];
  
  // Check if element is animated
  const isAnimated = 
    styles.transition !== 'none' || 
    styles.animation !== 'none' ||
    styles.transform !== 'none';
  
  if (isAnimated) {
    optimizedStyles.push('will-change: transform, opacity;');
    optimizedStyles.push('transform: translateZ(0);');
  }
  
  // Check if element is fixed or sticky
  if (styles.position === 'fixed' || styles.position === 'sticky') {
    optimizedStyles.push('will-change: transform;');
    optimizedStyles.push('transform: translateZ(0);');
  }
  
  // Check if element has overflow
  if (styles.overflow === 'auto' || styles.overflow === 'scroll' ||
      styles.overflowY === 'auto' || styles.overflowY === 'scroll') {
    optimizedStyles.push('-webkit-overflow-scrolling: touch;');
  }
  
  return optimizedStyles.join(' ');
}
