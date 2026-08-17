/**
 * Executes native PBKDF2-HMAC-SHA256 key derivation via browser Web Crypto API (crypto.subtle).
 * 
 * @param {string} password Plaintext password to hash.
 * @param {Uint8Array|Array<number>} saltBytes Salt byte array.
 * @param {number} iterations PBKDF2 iteration work factor.
 * @param {number} keyLengthBytes Output verifier length in bytes.
 * @returns {Promise<Uint8Array>} Derived key bytes.
 */
export async function derivePbkdf2Key(password, saltBytes, iterations, keyLengthBytes) {
    if (!password) {
        throw new Error("Password cannot be empty.");
    }
    const encoder = new TextEncoder();
    const passwordBytes = encoder.encode(password);
    const keyMaterial = await crypto.subtle.importKey(
        "raw",
        passwordBytes,
        "PBKDF2",
        false,
        ["deriveBits"]
    );
    const salt = saltBytes instanceof Uint8Array ? saltBytes : new Uint8Array(saltBytes);
    const derivedBits = await crypto.subtle.deriveBits(
        {
            name: "PBKDF2",
            salt: salt,
            iterations: iterations,
            hash: "SHA-256"
        },
        keyMaterial,
        keyLengthBytes * 8
    );
    return new Uint8Array(derivedBits);
}
