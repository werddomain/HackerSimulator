using System;

namespace HackerOs.OS.System
{
    /// <summary>
    /// Provides support for lazy initialization
    /// </summary>
    public class Lazy<T>
    {
        private readonly System.Lazy<T> _inner;
        
        public Lazy() 
        {
            _inner = new System.Lazy<T>();
        }
        
        public Lazy(Func<T> valueFactory) 
        {
            _inner = new System.Lazy<T>(valueFactory);
        }
        
        public Lazy(bool isThreadSafe) 
        {
            _inner = new System.Lazy<T>(isThreadSafe);
        }
        
        public Lazy(Func<T> valueFactory, bool isThreadSafe) 
        {
            _inner = new System.Lazy<T>(valueFactory, isThreadSafe);
        }
        
        public Lazy(Threading.LazyThreadSafetyMode mode) 
        {
            _inner = new System.Lazy<T>((System.Threading.LazyThreadSafetyMode)(int)mode);
        }
        
        public Lazy(Func<T> valueFactory, Threading.LazyThreadSafetyMode mode) 
        {
            _inner = new System.Lazy<T>(valueFactory, (System.Threading.LazyThreadSafetyMode)(int)mode);        }
        
        public bool IsValueCreated => _inner.IsValueCreated;
        public T Value => _inner.Value;
    }
}
