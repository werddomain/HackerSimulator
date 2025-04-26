/**
 * WeakMap Helper Functions
 * Provides compatibility functions for WeakMap across different environments
 */

/**
 * Safely iterate over the entries of a WeakMap
 * This is a workaround for environments where WeakMap doesn't support entries()
 * 
 * @param map The WeakMap to iterate over
 * @param callback Function to call for each entry with key and value
 */
export function forEachWeakMapEntry<K extends object, V>(
  map: WeakMap<K, V>,
  callback: (key: K, value: V) => void
): void {
  // Using a proxy array to keep track of keys that have values in the WeakMap
  // This is not a perfect solution, but works for most cases where we control
  // additions to the WeakMap
  const knownKeys = (map as any)._keys as K[] || [];
  
  if (knownKeys && knownKeys.length > 0) {
    knownKeys.forEach(key => {
      const value = map.get(key);
      if (value !== undefined) {
        callback(key, value);
      }
    });
    return;
  }
  
  // Alternative approach: If _keys isn't available, we need to provide it ourselves
  // The caller will need to manage registration of keys
  console.warn('WeakMap keys are not tracked. Some entries might be missed in the iteration.');
}

/**
 * Register a key to be tracked for iteration in a WeakMap
 * This is necessary for environments where WeakMap doesn't support entries()
 * 
 * @param map The WeakMap to register the key for
 * @param key The key to register
 */
export function registerWeakMapKey<K extends object, V>(
  map: WeakMap<K, V>,
  key: K
): void {
  if (!(map as any)._keys) {
    (map as any)._keys = [];
  }
  
  if (!(map as any)._keys.includes(key)) {
    (map as any)._keys.push(key);
  }
}

/**
 * Unregister a key from being tracked for iteration in a WeakMap
 * This is necessary when deleting entries to keep the key list clean
 * 
 * @param map The WeakMap to unregister the key from
 * @param key The key to unregister
 */
export function unregisterWeakMapKey<K extends object, V>(
  map: WeakMap<K, V>,
  key: K
): void {
  if ((map as any)._keys) {
    const index = (map as any)._keys.indexOf(key);
    if (index !== -1) {
      (map as any)._keys.splice(index, 1);
    }
  }
}
