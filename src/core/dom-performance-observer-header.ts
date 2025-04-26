/**
 * DOM Performance Observer
 * Automatically detects and optimizes problematic UI patterns that cause performance issues
 */

import * as DOMOptimizer from './dom-optimizer';
import { forEachWeakMapEntry, registerWeakMapKey, unregisterWeakMapKey } from './weak-map-helpers';

// Import the PerformanceMetric after fixing the import path
import { PerformanceMetric } from './performance-metric';
import './virtual-list';

// Rest of the file remains unchanged
