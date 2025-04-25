/**
 * Gesture Recognition System for HackerSimulator
 * Provides touch gesture detection for mobile devices
 */

/**
 * Recognized gesture types
 */
export enum GestureType {
  Tap = 'tap',
  DoubleTap = 'double-tap',
  LongPress = 'long-press',
  Swipe = 'swipe',
  Pinch = 'pinch',
  Rotate = 'rotate',
  Pan = 'pan'
}

/**
 * Swipe directions
 */
export enum SwipeDirection {
  Up = 'up',
  Down = 'down',
  Left = 'left',
  Right = 'right'
}

/**
 * Base gesture event data
 */
export interface GestureEventData {
  type: GestureType;
  target: EventTarget | null;
  originalEvent: TouchEvent | MouseEvent;
  timestamp: number;
  position: { x: number, y: number }; // Center position of the gesture
}

/**
 * Tap gesture event data
 */
export interface TapEventData extends GestureEventData {
  type: GestureType.Tap | GestureType.DoubleTap | GestureType.LongPress;
  tapCount: number; // For single tap and double tap
  duration?: number; // For long press
}

/**
 * Swipe gesture event data
 */
export interface SwipeEventData extends GestureEventData {
  type: GestureType.Swipe;
  direction: SwipeDirection;
  distance: number;
  velocity: number; // pixels per millisecond
  startPosition: { x: number, y: number };
  endPosition: { x: number, y: number };
}

/**
 * Pinch gesture event data
 */
export interface PinchEventData extends GestureEventData {
  type: GestureType.Pinch;
  scale: number; // Scale factor (1 = no change, >1 = zoom in, <1 = zoom out)
  velocity: number; // Scale change per millisecond
  center: { x: number, y: number }; // Center point of the pinch
}

/**
 * Rotate gesture event data
 */
export interface RotateEventData extends GestureEventData {
  type: GestureType.Rotate;
  rotation: number; // Rotation in degrees
  velocity: number; // Rotation speed in degrees per millisecond
  center: { x: number, y: number }; // Center point of the rotation
}

/**
 * Pan gesture event data
 */
export interface PanEventData extends GestureEventData {
  type: GestureType.Pan;
  deltaX: number;
  deltaY: number;
  velocity: { x: number, y: number }; // Velocity in pixels per millisecond
  isFirst: boolean; // Whether this is the first pan event
  isFinal: boolean; // Whether this is the final pan event
}

/**
 * Union type for all gesture event data
 */
export type GestureEventResult = 
  TapEventData | 
  SwipeEventData | 
  PinchEventData | 
  RotateEventData | 
  PanEventData;

/**
 * Gesture handler function type
 */
export type GestureHandler = (event: GestureEventResult) => void;

/**
 * Gesture configuration options
 */
export interface GestureOptions {
  // Tap related options
  tapMaxDistance?: number; // Maximum allowed movement for a tap
  doubleTapMaxDelay?: number; // Maximum time between taps for double tap
  longPressMinTime?: number; // Minimum hold time for long press
  
  // Swipe related options
  swipeMinDistance?: number; // Minimum distance for swipe recognition
  swipeMaxTime?: number; // Maximum time for swipe to complete
  
  // Pinch related options
  pinchMinScale?: number; // Minimum scale change for pinch recognition
  
  // Rotate related options
  rotateMinAngle?: number; // Minimum angle for rotation recognition

  // Enable/disable specific gesture types
  enableTap?: boolean;
  enableDoubleTap?: boolean;
  enableLongPress?: boolean;
  enableSwipe?: boolean;
  enablePinch?: boolean;
  enableRotate?: boolean;
  enablePan?: boolean;
}

/**
 * Default gesture options
 */
const DEFAULT_GESTURE_OPTIONS: GestureOptions = {
  // Tap related defaults
  tapMaxDistance: 10,
  doubleTapMaxDelay: 300,
  longPressMinTime: 500,
  
  // Swipe related defaults
  swipeMinDistance: 30,
  swipeMaxTime: 300,
  
  // Pinch related defaults
  pinchMinScale: 0.1,
  
  // Rotate related defaults
  rotateMinAngle: 15,
  
  // All gestures enabled by default
  enableTap: true,
  enableDoubleTap: true,
  enableLongPress: true,
  enableSwipe: true,
  enablePinch: true,
  enableRotate: true,
  enablePan: true
};

/**
 * Touch tracker to store touch points
 */
interface TouchTracker {
  startTime: number;
  startTouches: Touch[];
  lastTouches: Touch[];
  lastTime: number;
  lastDistance?: number;
  lastAngle?: number;
  isLongPressing: boolean;
  longPressTimer?: number;
  isPanning: boolean;
}

/**
 * Gesture Detector class
 * Detects various touch gestures on elements
 */
export class GestureDetector {
  private element: HTMLElement;
  private options: GestureOptions;
  private handlers: Map<GestureType, GestureHandler[]> = new Map();
  private touchTracker: TouchTracker | null = null;
  private lastTapTime: number = 0;
  private tapCount: number = 0;
  
  // Visualization elements for feedback
  private visualizer: HTMLElement | null = null;
  private showVisualFeedback: boolean = false;
  
  /**
   * Constructor
   * @param element Element to detect gestures on
   * @param options Gesture detection options
   */
  constructor(element: HTMLElement, options: GestureOptions = {}) {
    this.element = element;
    this.options = { ...DEFAULT_GESTURE_OPTIONS, ...options };
    
    // Initialize event handlers
    this.setupEventHandlers();
  }
  
  /**
   * Set up event handlers for the element
   */
  private setupEventHandlers(): void {
    // Touch event handlers
    this.element.addEventListener('touchstart', this.handleTouchStart.bind(this), { passive: false });
    this.element.addEventListener('touchmove', this.handleTouchMove.bind(this), { passive: false });
    this.element.addEventListener('touchend', this.handleTouchEnd.bind(this), { passive: false });
    this.element.addEventListener('touchcancel', this.handleTouchCancel.bind(this), { passive: false });
    
    // Mouse fallbacks for testing on desktop
    this.element.addEventListener('mousedown', this.handleMouseDown.bind(this));
    document.addEventListener('mousemove', this.handleMouseMove.bind(this));
    document.addEventListener('mouseup', this.handleMouseUp.bind(this));
  }
  
  /**
   * Enable visual feedback for gestures
   */
  public enableVisualFeedback(): void {
    this.showVisualFeedback = true;
    
    // Create visualizer element if it doesn't exist
    if (!this.visualizer) {
      this.visualizer = document.createElement('div');
      this.visualizer.className = 'gesture-visualizer';
      document.body.appendChild(this.visualizer);
    }
  }
  
  /**
   * Disable visual feedback
   */
  public disableVisualFeedback(): void {
    this.showVisualFeedback = false;
    
    // Remove visualizer if it exists
    if (this.visualizer) {
      document.body.removeChild(this.visualizer);
      this.visualizer = null;
    }
  }
  
  /**
   * Add a gesture handler
   */
  public on(type: GestureType, handler: GestureHandler): void {
    if (!this.handlers.has(type)) {
      this.handlers.set(type, []);
    }
    
    this.handlers.get(type)!.push(handler);
  }
  
  /**
   * Remove a gesture handler
   */
  public off(type: GestureType, handler: GestureHandler): void {
    if (!this.handlers.has(type)) return;
    
    const handlers = this.handlers.get(type)!;
    const index = handlers.indexOf(handler);
    
    if (index !== -1) {
      handlers.splice(index, 1);
    }
  }
  
  /**
   * Handle touch start event
   */
  private handleTouchStart(event: TouchEvent): void {
    // Store initial touch data
    this.touchTracker = {
      startTime: Date.now(),
      startTouches: Array.from(event.touches),
      lastTouches: Array.from(event.touches),
      lastTime: Date.now(),
      isLongPressing: false,
      isPanning: false
    };
    
    // Set up long press detection
    if (this.options.enableLongPress) {
      // Clear any existing timer
      if (this.touchTracker.longPressTimer) {
        window.clearTimeout(this.touchTracker.longPressTimer);
      }
      
      // Set timer for long press
      this.touchTracker.longPressTimer = window.setTimeout(() => {
        if (this.touchTracker && !this.touchTracker.isPanning) {
          this.touchTracker.isLongPressing = true;
          
          // Get the position of the first touch
          const touch = this.touchTracker.lastTouches[0];
          const position = { x: touch.clientX, y: touch.clientY };
          
          // Create long press event
          const longPressEvent: TapEventData = {
            type: GestureType.LongPress,
            target: event.target,
            originalEvent: event,
            timestamp: Date.now(),
            position,
            tapCount: 1,
            duration: Date.now() - this.touchTracker.startTime
          };
          
          // Show visual feedback
          this.showFeedback(GestureType.LongPress, position);
          
          // Trigger handlers
          this.triggerHandlers(GestureType.LongPress, longPressEvent);
        }
      }, this.options.longPressMinTime);
    }
    
    // Start pan detection
    if (this.options.enablePan) {
      this.detectPanStart(event);
    }
  }
  
  /**
   * Handle touch move event
   */
  private handleTouchMove(event: TouchEvent): void {
    // If no active touch tracking, ignore
    if (!this.touchTracker) return;
    
    // Update latest touch data
    this.touchTracker.lastTouches = Array.from(event.touches);
    this.touchTracker.lastTime = Date.now();
    
    // Check if touch has moved beyond tap threshold
    if (this.hasTouchMovedBeyondThreshold()) {
      // Cancel long press if touch moves too much
      if (this.touchTracker.longPressTimer) {
        window.clearTimeout(this.touchTracker.longPressTimer);
        this.touchTracker.longPressTimer = undefined;
      }
      
      // Detect multi-touch gestures (pinch/rotate)
      if (event.touches.length >= 2) {
        // Detect pinch gesture
        if (this.options.enablePinch) {
          this.detectPinch(event);
        }
        
        // Detect rotation
        if (this.options.enableRotate) {
          this.detectRotation(event);
        }
      } 
      // Single touch - detect pan
      else if (event.touches.length === 1 && this.options.enablePan) {
        this.detectPan(event);
      }
    }
  }
  
  /**
   * Handle touch end event
   */
  private handleTouchEnd(event: TouchEvent): void {
    // If no active touch tracking, ignore
    if (!this.touchTracker) return;
    
    // Cancel long press timer
    if (this.touchTracker.longPressTimer) {
      window.clearTimeout(this.touchTracker.longPressTimer);
      this.touchTracker.longPressTimer = undefined;
    }
    
    // If this was a long press, we're done
    if (this.touchTracker.isLongPressing) {
      this.touchTracker = null;
      return;
    }
    
    // Finalize pan if active
    if (this.touchTracker.isPanning && this.options.enablePan) {
      this.detectPanEnd(event);
    }
    
    // Detect swipe gesture
    if (this.options.enableSwipe) {
      this.detectSwipe(event);
    }
    
    // Detect tap/double tap
    if (this.options.enableTap && !this.hasTouchMovedBeyondThreshold()) {
      this.detectTap(event);
    }
    
    // Reset touch tracker
    this.touchTracker = null;
  }
  
  /**
   * Handle touch cancel event
   */
  private handleTouchCancel(event: TouchEvent): void {
    // Clean up any timers
    if (this.touchTracker && this.touchTracker.longPressTimer) {
      window.clearTimeout(this.touchTracker.longPressTimer);
    }
    
    // Reset touch tracker
    this.touchTracker = null;
  }
  
  /**
   * Handle mouse down (for desktop testing)
   */
  private handleMouseDown(event: MouseEvent): void {
    // Simulate touch with mouse
    const touch = this.createTouchFromMouse(event);
    
    this.touchTracker = {
      startTime: Date.now(),
      startTouches: [touch],
      lastTouches: [touch],
      lastTime: Date.now(),
      isLongPressing: false,
      isPanning: false
    };
    
    // Set up long press detection
    if (this.options.enableLongPress) {
      // Set timer for long press
      this.touchTracker.longPressTimer = window.setTimeout(() => {
        if (this.touchTracker && !this.touchTracker.isPanning) {
          this.touchTracker.isLongPressing = true;
          
          // Get the position
          const position = { x: touch.clientX, y: touch.clientY };
          
          // Create long press event
          const longPressEvent: TapEventData = {
            type: GestureType.LongPress,
            target: event.target,
            originalEvent: event,
            timestamp: Date.now(),
            position,
            tapCount: 1,
            duration: Date.now() - this.touchTracker.startTime
          };
          
          // Show visual feedback
          this.showFeedback(GestureType.LongPress, position);
          
          // Trigger handlers
          this.triggerHandlers(GestureType.LongPress, longPressEvent);
        }
      }, this.options.longPressMinTime);
    }
    
    // Start pan detection
    if (this.options.enablePan) {
      this.detectPanStart(event as any); // Cast to any as we're simulating
    }
  }
  
  /**
   * Handle mouse move (for desktop testing)
   */
  private handleMouseMove(event: MouseEvent): void {
    // If no active tracking, ignore
    if (!this.touchTracker) return;
    
    // Update with mouse position
    const touch = this.createTouchFromMouse(event);
    this.touchTracker.lastTouches = [touch];
    this.touchTracker.lastTime = Date.now();
    
    // Check if mouse has moved beyond tap threshold
    if (this.hasTouchMovedBeyondThreshold()) {
      // Cancel long press if mouse moves too much
      if (this.touchTracker.longPressTimer) {
        window.clearTimeout(this.touchTracker.longPressTimer);
        this.touchTracker.longPressTimer = undefined;
      }
      
      // Detect pan with mouse
      if (this.options.enablePan) {
        this.detectPan(event as any); // Cast to any as we're simulating
      }
    }
  }
  
  /**
   * Handle mouse up (for desktop testing)
   */
  private handleMouseUp(event: MouseEvent): void {
    // If no active tracking, ignore
    if (!this.touchTracker) return;
    
    // Cancel long press timer
    if (this.touchTracker.longPressTimer) {
      window.clearTimeout(this.touchTracker.longPressTimer);
      this.touchTracker.longPressTimer = undefined;
    }
    
    // If this was a long press, we're done
    if (this.touchTracker.isLongPressing) {
      this.touchTracker = null;
      return;
    }
    
    // Finalize pan if active
    if (this.touchTracker.isPanning && this.options.enablePan) {
      this.detectPanEnd(event as any); // Cast to any as we're simulating
    }
    
    // Detect swipe
    if (this.options.enableSwipe) {
      this.detectSwipe(event as any); // Cast to any as we're simulating
    }
    
    // Detect tap/double tap
    if (this.options.enableTap && !this.hasTouchMovedBeyondThreshold()) {
      this.detectTap(event as any); // Cast to any as we're simulating
    }
    
    // Reset touch tracker
    this.touchTracker = null;
  }
  
  /**
   * Create a simulated Touch object from a mouse event
   */
  private createTouchFromMouse(event: MouseEvent): Touch {
    return {
      identifier: 0,
      target: event.target,
      clientX: event.clientX,
      clientY: event.clientY,
      screenX: event.screenX,
      screenY: event.screenY,
      pageX: event.pageX,
      pageY: event.pageY,
      radiusX: 1,
      radiusY: 1,
      rotationAngle: 0,
      force: 1
    } as Touch;
  }
  
  /**
   * Check if touch has moved beyond the tap threshold
   */
  private hasTouchMovedBeyondThreshold(): boolean {
    if (!this.touchTracker || this.touchTracker.startTouches.length === 0) {
      return false;
    }
    
    const startTouch = this.touchTracker.startTouches[0];
    const currentTouch = this.touchTracker.lastTouches[0];
    
    if (!currentTouch) return false;
    
    const distance = this.getDistance(
      { x: startTouch.clientX, y: startTouch.clientY },
      { x: currentTouch.clientX, y: currentTouch.clientY }
    );
    
    return distance > (this.options.tapMaxDistance || DEFAULT_GESTURE_OPTIONS.tapMaxDistance!);
  }
  
  /**
   * Detect tap and double-tap gestures
   */
  private detectTap(event: TouchEvent | MouseEvent): void {
    const now = Date.now();
    const touch = 'touches' in event 
      ? (event.changedTouches[0] || this.touchTracker!.lastTouches[0])
      : this.createTouchFromMouse(event as MouseEvent);
    
    const position = { x: touch.clientX, y: touch.clientY };
    
    // Check if this could be a double tap
    const doubleTapPossible = (now - this.lastTapTime) < (this.options.doubleTapMaxDelay || DEFAULT_GESTURE_OPTIONS.doubleTapMaxDelay!);
    
    // If previous tap was recent, this is a double tap
    if (doubleTapPossible && this.options.enableDoubleTap) {
      this.tapCount = 2;
      
      // Create double tap event
      const doubleTapEvent: TapEventData = {
        type: GestureType.DoubleTap,
        target: event.target,
        originalEvent: event,
        timestamp: now,
        position,
        tapCount: 2
      };
      
      // Show visual feedback
      this.showFeedback(GestureType.DoubleTap, position);
      
      // Trigger handlers
      this.triggerHandlers(GestureType.DoubleTap, doubleTapEvent);
      
      // Reset tap tracking
      this.lastTapTime = 0;
      this.tapCount = 0;
    } 
    // This is a single tap
    else {
      this.tapCount = 1;
      
      // If double tap is enabled, delay single tap to check for double
      if (this.options.enableDoubleTap) {
        this.lastTapTime = now;
        
        setTimeout(() => {
          // If tap count is still 1 after the delay, it's a confirmed single tap
          if (this.tapCount === 1) {
            // Create tap event
            const tapEvent: TapEventData = {
              type: GestureType.Tap,
              target: event.target,
              originalEvent: event,
              timestamp: now,
              position,
              tapCount: 1
            };
            
            // Show visual feedback
            this.showFeedback(GestureType.Tap, position);
            
            // Trigger handlers
            this.triggerHandlers(GestureType.Tap, tapEvent);
            
            // Reset tap count
            this.tapCount = 0;
          }
        }, this.options.doubleTapMaxDelay || DEFAULT_GESTURE_OPTIONS.doubleTapMaxDelay!);
      } 
      // If double tap is not enabled, trigger single tap immediately
      else {
        // Create tap event
        const tapEvent: TapEventData = {
          type: GestureType.Tap,
          target: event.target,
          originalEvent: event,
          timestamp: now,
          position,
          tapCount: 1
        };
        
        // Show visual feedback
        this.showFeedback(GestureType.Tap, position);
        
        // Trigger handlers
        this.triggerHandlers(GestureType.Tap, tapEvent);
        
        // Reset tap count
        this.tapCount = 0;
      }
    }
  }
  
  /**
   * Detect swipe gestures
   */
  private detectSwipe(event: TouchEvent | MouseEvent): void {
    if (!this.touchTracker) return;
    
    const touch = 'changedTouches' in event 
      ? (event.changedTouches[0] || this.touchTracker.lastTouches[0]) 
      : this.createTouchFromMouse(event as MouseEvent);
    
    if (!touch || this.touchTracker.startTouches.length === 0) return;
    
    const startTouch = this.touchTracker.startTouches[0];
    const startPosition = { x: startTouch.clientX, y: startTouch.clientY };
    const endPosition = { x: touch.clientX, y: touch.clientY };
    
    // Calculate swipe distance
    const distance = this.getDistance(startPosition, endPosition);
    
    // Calculate swipe duration
    const duration = Date.now() - this.touchTracker.startTime;
    
    // Check if swipe meets minimum distance and maximum time requirements
    if (
      distance >= (this.options.swipeMinDistance || DEFAULT_GESTURE_OPTIONS.swipeMinDistance!) &&
      duration <= (this.options.swipeMaxTime || DEFAULT_GESTURE_OPTIONS.swipeMaxTime!)
    ) {
      // Determine swipe direction
      const direction = this.getSwipeDirection(startPosition, endPosition);
      
      // Calculate velocity (pixels per ms)
      const velocity = distance / duration;
      
      // Center position of the swipe
      const position = {
        x: (startPosition.x + endPosition.x) / 2,
        y: (startPosition.y + endPosition.y) / 2
      };
      
      // Create swipe event
      const swipeEvent: SwipeEventData = {
        type: GestureType.Swipe,
        target: event.target,
        originalEvent: event,
        timestamp: Date.now(),
        position,
        direction,
        distance,
        velocity,
        startPosition,
        endPosition
      };
      
      // Show visual feedback
      this.showFeedback(GestureType.Swipe, endPosition, direction);
      
      // Trigger handlers
      this.triggerHandlers(GestureType.Swipe, swipeEvent);
    }
  }
  
  /**
   * Detect pinch gestures
   */
  private detectPinch(event: TouchEvent): void {
    if (!this.touchTracker || event.touches.length < 2) return;
    
    // Get the touch points
    const touch1 = event.touches[0];
    const touch2 = event.touches[1];
    
    // Calculate the current distance between touch points
    const currentDistance = this.getDistance(
      { x: touch1.clientX, y: touch1.clientY },
      { x: touch2.clientX, y: touch2.clientY }
    );
    
    // If this is the first move event, initialize the last distance
    if (this.touchTracker.lastDistance === undefined) {
      this.touchTracker.lastDistance = currentDistance;
      return;
    }
    
    // Calculate the scale change
    const scale = currentDistance / this.touchTracker.lastDistance;
    
    // If scale change is significant enough
    if (Math.abs(scale - 1) > (this.options.pinchMinScale || DEFAULT_GESTURE_OPTIONS.pinchMinScale!)) {
      // Calculate center point
      const center = {
        x: (touch1.clientX + touch2.clientX) / 2,
        y: (touch1.clientY + touch2.clientY) / 2
      };
      
      // Calculate velocity
      const timeDelta = Date.now() - this.touchTracker.lastTime;
      const velocity = (scale - 1) / timeDelta;
      
      // Create pinch event
      const pinchEvent: PinchEventData = {
        type: GestureType.Pinch,
        target: event.target,
        originalEvent: event,
        timestamp: Date.now(),
        position: center,
        scale,
        velocity,
        center
      };
      
      // Show visual feedback
      this.showFeedback(GestureType.Pinch, center, undefined, scale);
      
      // Trigger handlers
      this.triggerHandlers(GestureType.Pinch, pinchEvent);
      
      // Update last distance
      this.touchTracker.lastDistance = currentDistance;
    }
  }
  
  /**
   * Detect rotation gestures
   */
  private detectRotation(event: TouchEvent): void {
    if (!this.touchTracker || event.touches.length < 2) return;
    
    // Get the touch points
    const touch1 = event.touches[0];
    const touch2 = event.touches[1];
    
    // Calculate the current angle between touch points
    const currentAngle = this.getAngle(
      { x: touch1.clientX, y: touch1.clientY },
      { x: touch2.clientX, y: touch2.clientY }
    );
    
    // If this is the first move event, initialize the last angle
    if (this.touchTracker.lastAngle === undefined) {
      this.touchTracker.lastAngle = currentAngle;
      return;
    }
    
    // Calculate the angle change
    let rotation = currentAngle - this.touchTracker.lastAngle;
    
    // Normalize rotation to range -180 to 180
    if (rotation > 180) rotation -= 360;
    if (rotation < -180) rotation += 360;
    
    // If rotation is significant enough
    if (Math.abs(rotation) > (this.options.rotateMinAngle || DEFAULT_GESTURE_OPTIONS.rotateMinAngle!)) {
      // Calculate center point
      const center = {
        x: (touch1.clientX + touch2.clientX) / 2,
        y: (touch1.clientY + touch2.clientY) / 2
      };
      
      // Calculate velocity
      const timeDelta = Date.now() - this.touchTracker.lastTime;
      const velocity = rotation / timeDelta;
      
      // Create rotate event
      const rotateEvent: RotateEventData = {
        type: GestureType.Rotate,
        target: event.target,
        originalEvent: event,
        timestamp: Date.now(),
        position: center,
        rotation,
        velocity,
        center
      };
      
      // Show visual feedback
      this.showFeedback(GestureType.Rotate, center, undefined, undefined, rotation);
      
      // Trigger handlers
      this.triggerHandlers(GestureType.Rotate, rotateEvent);
      
      // Update last angle
      this.touchTracker.lastAngle = currentAngle;
    }
  }
  
  /**
   * Start tracking pan gesture
   */
  private detectPanStart(event: TouchEvent | MouseEvent): void {
    if (!this.touchTracker) return;
    
    // Mark as panning
    this.touchTracker.isPanning = true;
    
    // Get the touch/mouse position
    const touch = 'touches' in event 
      ? event.touches[0] 
      : this.createTouchFromMouse(event as MouseEvent);
    
    const position = { x: touch.clientX, y: touch.clientY };
    
    // Create pan start event
    const panEvent: PanEventData = {
      type: GestureType.Pan,
      target: event.target,
      originalEvent: event,
      timestamp: Date.now(),
      position,
      deltaX: 0,
      deltaY: 0,
      velocity: { x: 0, y: 0 },
      isFirst: true,
      isFinal: false
    };
    
    // Trigger handlers
    this.triggerHandlers(GestureType.Pan, panEvent);
  }
  
  /**
   * Track ongoing pan gesture
   */
  private detectPan(event: TouchEvent | MouseEvent): void {
    if (!this.touchTracker || !this.touchTracker.isPanning) return;
    
    // Get the touch/mouse position
    const touch = 'touches' in event 
      ? event.touches[0] 
      : this.createTouchFromMouse(event as MouseEvent);
    
    const startTouch = this.touchTracker.startTouches[0];
    const position = { x: touch.clientX, y: touch.clientY };
    
    // Calculate delta from start
    const deltaX = touch.clientX - startTouch.clientX;
    const deltaY = touch.clientY - startTouch.clientY;
    
    // Calculate velocity
    const timeDelta = Date.now() - this.touchTracker.lastTime;
    const lastTouch = this.touchTracker.lastTouches[0];
    let velocityX = 0;
    let velocityY = 0;
    
    if (timeDelta > 0) {
      velocityX = (touch.clientX - lastTouch.clientX) / timeDelta;
      velocityY = (touch.clientY - lastTouch.clientY) / timeDelta;
    }
    
    // Create pan event
    const panEvent: PanEventData = {
      type: GestureType.Pan,
      target: event.target,
      originalEvent: event,
      timestamp: Date.now(),
      position,
      deltaX,
      deltaY,
      velocity: { x: velocityX, y: velocityY },
      isFirst: false,
      isFinal: false
    };
    
    // Show visual feedback
    this.showFeedback(GestureType.Pan, position);
    
    // Trigger handlers
    this.triggerHandlers(GestureType.Pan, panEvent);
  }
  
  /**
   * End pan gesture tracking
   */
  private detectPanEnd(event: TouchEvent | MouseEvent): void {
    if (!this.touchTracker || !this.touchTracker.isPanning) return;
    
    // Get the touch/mouse position
    const touch = 'changedTouches' in event 
      ? (event.changedTouches[0] || this.touchTracker.lastTouches[0])
      : this.createTouchFromMouse(event as MouseEvent);
    
    const startTouch = this.touchTracker.startTouches[0];
    const position = { x: touch.clientX, y: touch.clientY };
    
    // Calculate delta from start
    const deltaX = touch.clientX - startTouch.clientX;
    const deltaY = touch.clientY - startTouch.clientY;
    
    // Calculate velocity
    const timeDelta = Date.now() - this.touchTracker.lastTime;
    const lastTouch = this.touchTracker.lastTouches[0];
    let velocityX = 0;
    let velocityY = 0;
    
    if (timeDelta > 0) {
      velocityX = (touch.clientX - lastTouch.clientX) / timeDelta;
      velocityY = (touch.clientY - lastTouch.clientY) / timeDelta;
    }
    
    // Create pan end event
    const panEvent: PanEventData = {
      type: GestureType.Pan,
      target: event.target,
      originalEvent: event,
      timestamp: Date.now(),
      position,
      deltaX,
      deltaY,
      velocity: { x: velocityX, y: velocityY },
      isFirst: false,
      isFinal: true
    };
    
    // Trigger handlers
    this.triggerHandlers(GestureType.Pan, panEvent);
    
    // Reset panning flag
    this.touchTracker.isPanning = false;
  }
  
  /**
   * Calculate distance between two points
   */
  private getDistance(p1: { x: number, y: number }, p2: { x: number, y: number }): number {
    const dx = p2.x - p1.x;
    const dy = p2.y - p1.y;
    return Math.sqrt(dx * dx + dy * dy);
  }
  
  /**
   * Calculate angle between two points (in degrees)
   */
  private getAngle(p1: { x: number, y: number }, p2: { x: number, y: number }): number {
    return Math.atan2(p2.y - p1.y, p2.x - p1.x) * 180 / Math.PI;
  }
  
  /**
   * Determine swipe direction based on start and end points
   */
  private getSwipeDirection(start: { x: number, y: number }, end: { x: number, y: number }): SwipeDirection {
    const dx = end.x - start.x;
    const dy = end.y - start.y;
    
    // Determine if horizontal or vertical swipe
    if (Math.abs(dx) > Math.abs(dy)) {
      return dx > 0 ? SwipeDirection.Right : SwipeDirection.Left;
    } else {
      return dy > 0 ? SwipeDirection.Down : SwipeDirection.Up;
    }
  }
  
  /**
   * Trigger handlers for a specific gesture type
   */
  private triggerHandlers(type: GestureType, event: GestureEventResult): void {
    if (!this.handlers.has(type)) return;
    
    // Call all registered handlers for this gesture type
    const handlers = this.handlers.get(type)!;
    handlers.forEach(handler => handler(event));
  }
  
  /**
   * Show visual feedback for gestures
   */
  private showFeedback(
    type: GestureType, 
    position: { x: number, y: number },
    direction?: SwipeDirection,
    scale?: number,
    rotation?: number
  ): void {
    if (!this.showVisualFeedback || !this.visualizer) return;
    
    // Clear previous feedback
    this.visualizer.innerHTML = '';
    this.visualizer.className = 'gesture-visualizer';
    
    // Position visualizer
    this.visualizer.style.left = `${position.x}px`;
    this.visualizer.style.top = `${position.y}px`;
    
    // Add type-specific class and effects
    this.visualizer.classList.add(`${type}-gesture`);
    
    // Add direction class for swipe
    if (type === GestureType.Swipe && direction) {
      this.visualizer.classList.add(`swipe-${direction}`);
    }
    
    // Add scale transform for pinch
    if (type === GestureType.Pinch && scale !== undefined) {
      this.visualizer.style.transform = `scale(${scale})`;
    }
    
    // Add rotation transform for rotate
    if (type === GestureType.Rotate && rotation !== undefined) {
      this.visualizer.style.transform = `rotate(${rotation}deg)`;
    }
    
    // Show visualizer with fade effect
    this.visualizer.classList.add('visible');
    
    // Hide after animation
    setTimeout(() => {
      if (this.visualizer) {
        this.visualizer.classList.remove('visible');
      }
    }, 500);
  }
  
  /**
   * Clean up resources and event listeners
   */
  public destroy(): void {
    // Remove event listeners
    this.element.removeEventListener('touchstart', this.handleTouchStart.bind(this));
    this.element.removeEventListener('touchmove', this.handleTouchMove.bind(this));
    this.element.removeEventListener('touchend', this.handleTouchEnd.bind(this));
    this.element.removeEventListener('touchcancel', this.handleTouchCancel.bind(this));
    this.element.removeEventListener('mousedown', this.handleMouseDown.bind(this));
    document.removeEventListener('mousemove', this.handleMouseMove.bind(this));
    document.removeEventListener('mouseup', this.handleMouseUp.bind(this));
    
    // Remove visualizer if it exists
    if (this.visualizer && document.body.contains(this.visualizer)) {
      document.body.removeChild(this.visualizer);
    }
    
    // Clear handlers
    this.handlers.clear();
  }
}
