// HackerOS IndexedDB operations for file system persistence
window.hackerOSIndexedDB = (function() {
    'use strict';

    let db = null;
    const DB_NAME = 'HackerOSFileSystem';
    const DB_VERSION = 1;

    // Object store names
    const STORES = {
        FILESYSTEM: 'filesystem',
        FILES: 'files',
        METADATA: 'metadata'
    };

    /**
     * Initializes the IndexedDB database with required object stores.
     * @param {string} databaseName - Name of the database
     * @param {number} version - Database version
     * @returns {Promise<boolean>} - True if initialization succeeded
     */
    async function initializeDatabase(databaseName = DB_NAME, version = DB_VERSION) {
        return new Promise((resolve, reject) => {
            const request = indexedDB.open(databaseName, version);

            request.onerror = () => {
                console.error('Failed to open IndexedDB:', request.error);
                resolve(false);
            };

            request.onsuccess = () => {
                db = request.result;
                console.log('IndexedDB initialized successfully');
                resolve(true);
            };

            request.onupgradeneeded = (event) => {
                db = event.target.result;
                
                // Create object stores if they don't exist
                if (!db.objectStoreNames.contains(STORES.FILESYSTEM)) {
                    db.createObjectStore(STORES.FILESYSTEM, { keyPath: 'id' });
                }
                
                if (!db.objectStoreNames.contains(STORES.FILES)) {
                    db.createObjectStore(STORES.FILES, { keyPath: 'path' });
                }
                
                if (!db.objectStoreNames.contains(STORES.METADATA)) {
                    db.createObjectStore(STORES.METADATA, { keyPath: 'type' });
                }
                
                console.log('IndexedDB object stores created');
            };
        });
    }

    /**
     * Saves data to the specified object store.
     * @param {string} storeName - Name of the object store
     * @param {string} key - Key for the data
     * @param {any} data - Data to save
     * @returns {Promise<boolean>} - True if save succeeded
     */
    async function saveData(storeName, key, data) {
        if (!db) {
            console.error('Database not initialized');
            return false;
        }

        return new Promise((resolve, reject) => {
            const transaction = db.transaction([storeName], 'readwrite');
            const store = transaction.objectStore(storeName);
            
            const dataObj = storeName === STORES.FILES ? 
                { path: key, content: data } : 
                storeName === STORES.METADATA ?
                { type: key, data: data } :
                { id: key, data: data };

            const request = store.put(dataObj);

            request.onsuccess = () => {
                resolve(true);
            };

            request.onerror = () => {
                console.error(`Failed to save data to ${storeName}:`, request.error);
                resolve(false);
            };
        });
    }

    /**
     * Loads data from the specified object store.
     * @param {string} storeName - Name of the object store
     * @param {string} key - Key for the data
     * @returns {Promise<any>} - The loaded data, or null if not found
     */
    async function loadData(storeName, key) {
        if (!db) {
            console.error('Database not initialized');
            return null;
        }

        return new Promise((resolve, reject) => {
            const transaction = db.transaction([storeName], 'readonly');
            const store = transaction.objectStore(storeName);
            const request = store.get(key);

            request.onsuccess = () => {
                if (request.result) {
                    const data = storeName === STORES.FILES ? 
                        request.result.content : 
                        storeName === STORES.METADATA ?
                        request.result.data :
                        request.result.data;
                    resolve(data);
                } else {
                    resolve(null);
                }
            };

            request.onerror = () => {
                console.error(`Failed to load data from ${storeName}:`, request.error);
                resolve(null);
            };
        });
    }

    /**
     * Deletes data from the specified object store.
     * @param {string} storeName - Name of the object store
     * @param {string} key - Key for the data to delete
     * @returns {Promise<boolean>} - True if deletion succeeded
     */
    async function deleteData(storeName, key) {
        if (!db) {
            console.error('Database not initialized');
            return false;
        }

        return new Promise((resolve, reject) => {
            const transaction = db.transaction([storeName], 'readwrite');
            const store = transaction.objectStore(storeName);
            const request = store.delete(key);

            request.onsuccess = () => {
                resolve(true);
            };

            request.onerror = () => {
                console.error(`Failed to delete data from ${storeName}:`, request.error);
                resolve(false);
            };
        });
    }

    /**
     * Clears all data from all object stores.
     * @returns {Promise<boolean>} - True if clear succeeded
     */
    async function clearAllData() {
        if (!db) {
            console.error('Database not initialized');
            return false;
        }

        try {
            const storeNames = [STORES.FILESYSTEM, STORES.FILES, STORES.METADATA];
            const promises = storeNames.map(storeName => {
                return new Promise((resolve, reject) => {
                    const transaction = db.transaction([storeName], 'readwrite');
                    const store = transaction.objectStore(storeName);
                    const request = store.clear();

                    request.onsuccess = () => resolve(true);
                    request.onerror = () => {
                        console.error(`Failed to clear ${storeName}:`, request.error);
                        resolve(false);
                    };
                });
            });

            const results = await Promise.all(promises);
            return results.every(result => result === true);
        } catch (error) {
            console.error('Failed to clear all data:', error);
            return false;
        }
    }

    /**
     * Gets all keys from the specified object store.
     * @param {string} storeName - Name of the object store
     * @returns {Promise<string[]>} - Array of keys
     */
    async function getAllKeys(storeName) {
        if (!db) {
            console.error('Database not initialized');
            return [];
        }

        return new Promise((resolve, reject) => {
            const transaction = db.transaction([storeName], 'readonly');
            const store = transaction.objectStore(storeName);
            const request = store.getAllKeys();

            request.onsuccess = () => {
                resolve(request.result || []);
            };

            request.onerror = () => {
                console.error(`Failed to get keys from ${storeName}:`, request.error);
                resolve([]);
            };
        });
    }

    /**
     * Gets the size of the database in bytes (approximate).
     * @returns {Promise<number>} - Database size in bytes
     */
    async function getDatabaseSize() {
        if (!db) {
            return 0;
        }

        try {
            const storeNames = [STORES.FILESYSTEM, STORES.FILES, STORES.METADATA];
            let totalSize = 0;

            for (const storeName of storeNames) {
                const keys = await getAllKeys(storeName);
                for (const key of keys) {
                    const data = await loadData(storeName, key);
                    if (data) {
                        totalSize += JSON.stringify(data).length * 2; // Rough estimate
                    }
                }
            }

            return totalSize;
        } catch (error) {
            console.error('Failed to calculate database size:', error);
            return 0;
        }
    }

    // Public API
    return {
        initializeDatabase,
        saveData,
        loadData,
        deleteData,
        clearAllData,
        getAllKeys,
        getDatabaseSize
    };
})();
