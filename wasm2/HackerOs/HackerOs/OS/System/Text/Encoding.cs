using System;

namespace HackerOs.OS.System.Text
{
    /// <summary>
    /// Represents a character encoding in the HackerOS system.
    /// This is a virtual implementation that wraps .NET's System.Text.Encoding.
    /// </summary>
    public abstract class Encoding
    {
        private readonly global::System.Text.Encoding _systemEncoding;

        protected Encoding(global::System.Text.Encoding systemEncoding)
        {
            _systemEncoding = systemEncoding ?? throw new ArgumentNullException(nameof(systemEncoding));
        }

        /// <summary>
        /// Gets the UTF-8 encoding.
        /// </summary>
        public static Encoding UTF8 => new UTF8Encoding();

        /// <summary>
        /// Gets the ASCII encoding.
        /// </summary>
        public static Encoding ASCII => new ASCIIEncoding();

        /// <summary>
        /// Gets the Unicode (UTF-16) encoding.
        /// </summary>
        public static Encoding Unicode => new UnicodeEncoding();

        /// <summary>
        /// Gets the default encoding for the system.
        /// </summary>
        public static Encoding Default => UTF8;

        /// <summary>
        /// Converts bytes to a string using this encoding.
        /// </summary>
        public virtual string GetString(byte[] bytes) => _systemEncoding.GetString(bytes);

        /// <summary>
        /// Converts a string to bytes using this encoding.
        /// </summary>
        public virtual byte[] GetBytes(string s) => _systemEncoding.GetBytes(s);

        /// <summary>
        /// Gets the maximum number of bytes produced by encoding the specified number of characters.
        /// </summary>
        public virtual int GetMaxByteCount(int charCount) => _systemEncoding.GetMaxByteCount(charCount);

        /// <summary>
        /// Gets the maximum number of characters produced by decoding the specified number of bytes.
        /// </summary>
        public virtual int GetMaxCharCount(int byteCount) => _systemEncoding.GetMaxCharCount(byteCount);
    }

    /// <summary>
    /// UTF-8 encoding implementation.
    /// </summary>
    public class UTF8Encoding : Encoding
    {
        public UTF8Encoding() : base(global::System.Text.Encoding.UTF8) { }
    }

    /// <summary>
    /// ASCII encoding implementation.
    /// </summary>
    public class ASCIIEncoding : Encoding
    {
        public ASCIIEncoding() : base(global::System.Text.Encoding.ASCII) { }
    }

    /// <summary>
    /// Unicode (UTF-16) encoding implementation.
    /// </summary>
    public class UnicodeEncoding : Encoding
    {
        public UnicodeEncoding() : base(global::System.Text.Encoding.Unicode) { }
    }
}
