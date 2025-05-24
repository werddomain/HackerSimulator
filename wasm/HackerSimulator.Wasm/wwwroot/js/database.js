const DB_NAME = 'hacker-os';
const SCHEMA_STORE = '_schema';
let db = null;

function openDb(version, upgrade){
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, version);
    req.onupgradeneeded = (ev) => {
      const d = req.result;
      if(!d.objectStoreNames.contains(SCHEMA_STORE)){
        d.createObjectStore(SCHEMA_STORE, { keyPath: 'name' });
      }
      upgrade(d, ev.oldVersion);
    };
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error);
  });
}

async function ensureDb(){
  if(db) return db;
  db = await openDb(1, () => {});
  return db;
}

async function getDb(){
  return await ensureDb();
}

function getSchemaVersion(db, name){
  return new Promise((resolve) => {
    const tx = db.transaction(SCHEMA_STORE, 'readonly');
    const req = tx.objectStore(SCHEMA_STORE).get(name);
    req.onsuccess = () => resolve(req.result ? req.result.version : 0);
    req.onerror = () => resolve(0);
  });
}

function setSchemaVersion(db, name, version){
  return new Promise((resolve) => {
    const tx = db.transaction(SCHEMA_STORE, 'readwrite');
    tx.objectStore(SCHEMA_STORE).put({ name, version });
    tx.oncomplete = () => resolve();
  });
}

export async function initTable(name, version){
  let d = await getDb();
  if(!d.objectStoreNames.contains(name)){
    const newVersion = d.version + 1;
    d.close();
    d = await openDb(newVersion, db => {
      if(!db.objectStoreNames.contains(name)) db.createObjectStore(name);
    });
    db = d;
  }
  const current = await getSchemaVersion(d, name);
  if(current < version){
    await setSchemaVersion(d, name, version);
  }
  return current;
}

export async function ensureStore(name){
  let d = await getDb();
  if(!d.objectStoreNames.contains(name)){
    const newVersion = d.version + 1;
    d.close();
    d = await openDb(newVersion, db => {
      if(!db.objectStoreNames.contains(name)) db.createObjectStore(name);
    });
    db = d;
  }
}

export async function getSchemaVersionFor(name){
  const d = await getDb();
  return await getSchemaVersion(d, name);
}

export async function setSchemaVersionFor(name, version){
  const d = await getDb();
  await setSchemaVersion(d, name, version);
}

export async function get(store, key){
  const d = await getDb();
  return new Promise((resolve, reject) => {
    const req = d.transaction(store, 'readonly').objectStore(store).get(key);
    req.onsuccess = () => resolve(req.result || null);
    req.onerror = () => reject(req.error);
  });
}

export async function set(store, key, value){
  const d = await getDb();
  return new Promise((resolve, reject) => {
    const tx = d.transaction(store, 'readwrite');
    tx.objectStore(store).put(value, key);
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
}

export async function remove(store, key){
  const d = await getDb();
  return new Promise((resolve, reject) => {
    const tx = d.transaction(store, 'readwrite');
    tx.objectStore(store).delete(key);
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
}

export async function clear(store){
  const d = await getDb();
  return new Promise((resolve, reject) => {
    const tx = d.transaction(store, 'readwrite');
    tx.objectStore(store).clear();
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
}

export async function getAll(store){
  const d = await getDb();
  return new Promise((resolve, reject) => {
    const req = d.transaction(store, 'readonly').objectStore(store).getAll();
    req.onsuccess = () => resolve(req.result || []);
    req.onerror = () => reject(req.error);
  });
}
