/**
 * Mobile Animation Optimizer
 * Provides utilities for creating efficient animations on mobile devices
 */

import { platformDetector, PlatformType } from './platform-detector';
import { optimizeForAnimation, resetOptimization } from './dom-optimizer';

/**
 * Animation properties configuration
 */
export interface AnimationConfig {
  // Duration in milliseconds
  duration: number;
  
  // Delay before starting animation (ms)
  delay?: number;
  
  // Easing function
  easing?: string;
  
  // Number of iterations (Infinity for endless)
  iterations?: number;
  
  // Whether to alternate direction on iterations
  alternate?: boolean;
  
  // Reduces complexity on mobile devices
  reduceOnMobile?: boolean;
  
  // How much to reduce properties on mobile (0-1)
  reductionFactor?: number;
  
  // Elements to animate
  elements: HTMLElement[];
}

/**
 * Mobile-optimized animation presets
 */
export const AnimationPresets = {
  // Simple fade in animation
  FADE_IN: (element: HTMLElement, duration: number = 300): AnimationConfig => ({
    duration,
    easing: 'ease-out',
    elements: [element],
    reduceOnMobile: true,
    reductionFactor: 0.7
  }),
  
  // Simple fade out animation
  FADE_OUT: (element: HTMLElement, duration: number = 300): AnimationConfig => ({
    duration,
    easing: 'ease-in',
    elements: [element],
    reduceOnMobile: true,
    reductionFactor: 0.7
  }),
  
  // Slide down animation
  SLIDE_DOWN: (element: HTMLElement, duration: number = 300): AnimationConfig => ({
    duration,
    easing: 'cubic-bezier(0.1, 0.9, 0.2, 1.0)',
    elements: [element],
    reduceOnMobile: true,
    reductionFactor: 0.6
  }),
  
  // Window minimize animation
  MINIMIZE: (element: HTMLElement, duration: number = 200): AnimationConfig => ({
    duration,
    easing: 'cubic-bezier(0.1, 0.7, 1.0, 0.1)',
    elements: [element],
    reduceOnMobile: true,
    reductionFactor: 0.5
  }),
  
  // Dialog appear animation
  DIALOG_APPEAR: (element: HTMLElement, duration: number = 250): AnimationConfig => ({
    duration,
    easing: 'cubic-bezier(0, 0, 0.2, 1.0)',
    elements: [element],
    reduceOnMobile: true,
    reductionFactor: 0.6
  })
};

// Global settings for animation reduction
let globalReducedMotion = false;
let mobileReductionFactor = 0.7; // Default reduction for mobile

/**
 * Set global reduced motion preference
 * @param reduced Whether to enable reduced motion globally
 */
export function setReducedMotion(reduced: boolean): void {
  globalReducedMotion = reduced;
}

/**
 * Set mobile reduction factor
 * @param factor Reduction factor (0-1)
 */
export function setMobileReductionFactor(factor: number): void {
  mobileReductionFactor = Math.min(Math.max(factor, 0), 1);
}

/**
 * Adjust animation configuration based on device capabilities
 * @param config Original animation configuration
 * @returns Adjusted animation configuration
 */
function adjustForMobile(config: AnimationConfig): AnimationConfig {
  const isMobile = platformDetector.getPlatformType() === PlatformType.MOBILE;
  const adjusted = { ...config };
  
  // Apply reduced motion globally if set
  if (globalReducedMotion) {
    adjusted.duration = Math.min(100, adjusted.duration * 0.3);
    adjusted.easing = 'linear';
    return adjusted;
  }
  
  // Apply mobile adjustments
  if (isMobile && adjusted.reduceOnMobile) {
    const factor = adjusted.reductionFactor ?? mobileReductionFactor;
    
    // Reduce duration by the reduction factor
    adjusted.duration = Math.round(adjusted.duration * factor);
    
    // Simplify easing function for mobile
    if (adjusted.easing && adjusted.easing.includes('cubic-bezier')) {
      adjusted.easing = 'ease-out';
    }
  }
  
  return adjusted;
}

/**
 * Create optimized animation keyframes
 * that use GPU-accelerated properties
 */
export function createOptimizedAnimation(
  keyframes: Keyframe[],
  config: AnimationConfig
): Animation[] {
  // Adjust configuration for mobile
  const adjustedConfig = adjustForMobile(config);
  
  // Optimize keyframes to use transform and opacity when possible
  const optimizedKeyframes = keyframes.map(keyframe => {
    const optimized: any = { ...keyframe };
    
    // Convert left/top to transforms when possible
    if ('left' in optimized || 'top' in optimized) {
      const translateX = 'left' in optimized ? `${optimized.left}` : '0px';
      const translateY = 'top' in optimized ? `${optimized.top}` : '0px';
      
      optimized.transform = `translate(${translateX}, ${translateY})`;
      delete optimized.left;
      delete optimized.top;
    }
    
    // Convert width/height changes to scale transforms when possible
    if ('width' in optimized && typeof optimized.width === 'string' && 
        optimized.width.endsWith('%') && parseFloat(optimized.width) <= 100) {
      const scaleX = parseFloat(optimized.width) / 100;
      optimized.transform = `${optimized.transform || ''} scaleX(${scaleX})`;
      delete optimized.width;
    }
    
    if ('height' in optimized && typeof optimized.height === 'string' && 
        optimized.height.endsWith('%') && parseFloat(optimized.height) <= 100) {
      const scaleY = parseFloat(optimized.height) / 100;
      optimized.transform = `${optimized.transform || ''} scaleY(${scaleY})`;
      delete optimized.height;
    }
    
    return optimized;
  });
  
  // Create animation options
  const options: KeyframeAnimationOptions = {
    duration: adjustedConfig.duration,
    delay: adjustedConfig.delay || 0,
    easing: adjustedConfig.easing || 'ease',
    iterations: adjustedConfig.iterations || 1,
    direction: adjustedConfig.alternate ? 'alternate' : 'normal',
    fill: 'both'
  };
  
  // Create and return animations for all elements
  return adjustedConfig.elements.map(element => {
    // Optimize element for animation
    optimizeForAnimation(element);
    
    // Create and start animation
    const animation = element.animate(optimizedKeyframes, options);
    
    // Cleanup after animation completes
    animation.onfinish = () => {
      resetOptimization(element);
    };
    
    return animation;
  });
}

/**
 * Fade in animation optimized for mobile
 * @param element Element to animate
 * @param duration Animation duration in ms
 * @returns Animation object
 */
export function fadeIn(element: HTMLElement, duration: number = 300): Animation {
  const config = AnimationPresets.FADE_IN(element, duration);
  const keyframes = [
    { opacity: 0 },
    { opacity: 1 }
  ];
  
  return createOptimizedAnimation(keyframes, config)[0];
}

/**
 * Fade out animation optimized for mobile
 * @param element Element to animate
 * @param duration Animation duration in ms
 * @returns Animation object
 */
export function fadeOut(element: HTMLElement, duration: number = 300): Animation {
  const config = AnimationPresets.FADE_OUT(element, duration);
  const keyframes = [
    { opacity: 1 },
    { opacity: 0 }
  ];
  
  return createOptimizedAnimation(keyframes, config)[0];
}

/**
 * Slide down animation optimized for mobile
 * @param element Element to animate
 * @param duration Animation duration in ms
 * @returns Animation object
 */
export function slideDown(element: HTMLElement, duration: number = 300): Animation {
  const config = AnimationPresets.SLIDE_DOWN(element, duration);
  
  // Get final height
  element.style.height = 'auto';
  const height = element.offsetHeight;
  element.style.height = '0px';
  element.style.overflow = 'hidden';
  
  const keyframes = [
    { height: '0px', opacity: 0, transform: 'translateY(-10px)' },
    { height: `${height}px`, opacity: 1, transform: 'translateY(0)' }
  ];
  
  const animation = createOptimizedAnimation(keyframes, config)[0];
  
  // Reset height to auto after animation
  animation.onfinish = () => {
    element.style.height = 'auto';
    element.style.overflow = '';
    resetOptimization(element);
  };
  
  return animation;
}

/**
 * Dialog appear animation optimized for mobile
 * @param element Dialog element to animate
 * @param duration Animation duration in ms
 * @returns Animation object
 */
export function dialogAppear(element: HTMLElement, duration: number = 250): Animation {
  const config = AnimationPresets.DIALOG_APPEAR(element, duration);
  const keyframes = [
    { opacity: 0, transform: 'scale(0.9)' },
    { opacity: 1, transform: 'scale(1)' }
  ];
  
  return createOptimizedAnimation(keyframes, config)[0];
}

/**
 * Dialog disappear animation optimized for mobile
 * @param element Dialog element to animate
 * @param duration Animation duration in ms
 * @returns Animation object
 */
export function dialogDisappear(element: HTMLElement, duration: number = 200): Animation {
  const config = {
    ...AnimationPresets.DIALOG_APPEAR(element, duration),
    easing: 'ease-in'
  };
  
  const keyframes = [
    { opacity: 1, transform: 'scale(1)' },
    { opacity: 0, transform: 'scale(0.9)' }
  ];
  
  return createOptimizedAnimation(keyframes, config)[0];
}

/**
 * Window minimize animation optimized for mobile
 * @param element Window element to animate
 * @param targetX Target X position
 * @param targetY Target Y position
 * @param targetScale Target scale
 * @param duration Animation duration in ms
 * @returns Animation object
 */
export function windowMinimize(
  element: HTMLElement, 
  targetX: number, 
  targetY: number, 
  targetScale: number = 0.1,
  duration: number = 200
): Animation {
  const config = AnimationPresets.MINIMIZE(element, duration);
  
  const keyframes = [
    { 
      opacity: 1, 
      transform: 'translate(0, 0) scale(1)' 
    },
    { 
      opacity: 0.7, 
      transform: `translate(${targetX}px, ${targetY}px) scale(${targetScale})` 
    }
  ];
  
  return createOptimizedAnimation(keyframes, config)[0];
}

/**
 * Apply reduced motion settings based on user preferences
 * Checks prefers-reduced-motion media query
 */
export function applyUserMotionPreferences(): void {
  // Check for reduced motion preference
  const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  setReducedMotion(prefersReducedMotion);
  
  // Set mobile reduction factor based on platform
  const isMobile = platformDetector.getPlatformType() === PlatformType.MOBILE;
  if (isMobile) {
    setMobileReductionFactor(0.7);
  }
}

/**
 * Initialize the animation system
 */
export function initAnimationSystem(): void {
  applyUserMotionPreferences();
  
  // Listen for changes to reduced motion preference
  window.matchMedia('(prefers-reduced-motion: reduce)').addEventListener('change', event => {
    setReducedMotion(event.matches);
  });
}
