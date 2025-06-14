using System;
using System.Threading;
using System.Threading.Tasks;

namespace HackerOs.OS.System.Threading
{
    /// <summary>
    /// Provides a mechanism for executing a method at specified intervals
    /// </summary>
    public sealed class Timer : IDisposable
    {
        private readonly System.Threading.Timer _timer;
        private bool _disposed = false;

        /// <summary>
        /// Initializes a new instance of the Timer class
        /// </summary>
        public Timer(TimerCallback callback, object? state, int dueTime, int period)
        {
            _timer = new System.Threading.Timer(callback, state, dueTime, period);
        }

        /// <summary>
        /// Initializes a new instance of the Timer class
        /// </summary>
        public Timer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _timer = new System.Threading.Timer(callback, state, dueTime, period);
        }

        /// <summary>
        /// Changes the start time and the interval between method invocations
        /// </summary>
        public bool Change(int dueTime, int period)
        {
            return _timer.Change(dueTime, period);
        }

        /// <summary>
        /// Changes the start time and the interval between method invocations
        /// </summary>
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            return _timer.Change(dueTime, period);
        }

        /// <summary>
        /// Releases all resources used by the Timer
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _timer.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Represents the method that handles calls from a Timer
    /// </summary>
    public delegate void TimerCallback(object? state);    /// <summary>
    /// Cancellation token that can be used to cancel operations
    /// </summary>
    public struct CancellationToken
    {
        private readonly bool _isCancellationRequested;        public CancellationToken(bool isCancellationRequested)
        {
            _isCancellationRequested = isCancellationRequested;
        }

        public CancellationToken(System.Threading.CancellationToken token)
        {
            _isCancellationRequested = token.IsCancellationRequested;
        }

        public bool IsCancellationRequested => _isCancellationRequested;

        public void ThrowIfCancellationRequested()
        {
            if (_isCancellationRequested)
                throw new OperationCanceledException();
        }        public static CancellationToken None => new CancellationToken(false);

        /// <summary>
        /// Get the underlying System.Threading.CancellationToken
        /// </summary>
        public global::System.Threading.CancellationToken GetSystemToken()
        {
            return _isCancellationRequested ?
                new global::System.Threading.CancellationToken(true) :
                global::System.Threading.CancellationToken.None;
        }
    }

    /// <summary>
    /// Signals to a CancellationToken that it should be canceled
    /// </summary>
    public class CancellationTokenSource : IDisposable
    {
        private readonly System.Threading.CancellationTokenSource _source;

        public CancellationTokenSource()
        {
            _source = new System.Threading.CancellationTokenSource();
        }

        public CancellationTokenSource(TimeSpan delay)
        {
            _source = new System.Threading.CancellationTokenSource(delay);
        }

        public CancellationToken Token => new CancellationToken(_source.Token.IsCancellationRequested);

        public bool IsCancellationRequested => _source.IsCancellationRequested;

        public void Cancel()
        {
            _source.Cancel();
        }

        public void Cancel(bool throwOnFirstException)
        {
            _source.Cancel(throwOnFirstException);
        }

        public void CancelAfter(TimeSpan delay)
        {
            _source.CancelAfter(delay);
        }

        public void CancelAfter(int millisecondsDelay)
        {
            _source.CancelAfter(millisecondsDelay);
        }

        public void Dispose()
        {
            _source.Dispose();
        }
    }

    /// <summary>
    /// Provides support for lazy initialization
    /// </summary>
    public class Lazy<T> : System.Lazy<T>
    {
        public Lazy() : base() { }
        public Lazy(Func<T> valueFactory) : base(valueFactory) { }
        public Lazy(bool isThreadSafe) : base(isThreadSafe) { }
        public Lazy(Func<T> valueFactory, bool isThreadSafe) : base(valueFactory, isThreadSafe) { }
        public Lazy(LazyThreadSafetyMode mode) : base(mode) { }
        public Lazy(Func<T> valueFactory, LazyThreadSafetyMode mode) : base(valueFactory, mode) { }
    }

    /// <summary>
    /// Specifies how a Lazy instance synchronizes access among multiple threads
    /// </summary>
    public enum LazyThreadSafetyMode
    {
        None = 0,
        PublicationOnly = 1,
        ExecutionAndPublication = 2
    }

    /// <summary>
    /// Provides a thread-safe object pool
    /// </summary>
    public abstract class ObjectPool<T> where T : class
    {
        public abstract T Get();
        public abstract void Return(T obj);
    }    /// <summary>
    /// Represents a thread-safe collection of key/value pairs
    /// </summary>
    public class ConcurrentDictionary<TKey, TValue> : HackerOs.OS.System.Collections.IDictionary<TKey, TValue>
        where TKey : notnull
    {
        private readonly Dictionary<TKey, TValue> _dictionary = new();
        private readonly object _lock = new();

        public TValue this[TKey key]
        {
            get
            {
                lock (_lock)
                {
                    return _dictionary[key];
                }
            }
            set
            {
                lock (_lock)
                {
                    _dictionary[key] = value;
                }
            }
        }

        public HackerOs.OS.System.Collections.ICollection<TKey> Keys
        {            get
            {
                lock (_lock)
                {
                    return (HackerOs.OS.System.Collections.ICollection<TKey>)new List<TKey>(_dictionary.Keys);
                }
            }
        }

        public HackerOs.OS.System.Collections.ICollection<TValue> Values
        {            get
            {
                lock (_lock)
                {
                    return (HackerOs.OS.System.Collections.ICollection<TValue>)new List<TValue>(_dictionary.Values);
                }
            }
        }

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _dictionary.Count;
                }
            }
        }

        public bool IsReadOnly => false;

        public void Add(TKey key, TValue value)
        {
            lock (_lock)
            {
                _dictionary.Add(key, value);
            }
        }

        public void Add(HackerOs.OS.System.Collections.KeyValuePair<TKey, TValue> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            lock (_lock)
            {
                _dictionary.Clear();
            }
        }

        public bool Contains(HackerOs.OS.System.Collections.KeyValuePair<TKey, TValue> item)
        {
            lock (_lock)
            {
                return _dictionary.ContainsKey(item.Key) && EqualityComparer<TValue>.Default.Equals(_dictionary[item.Key], item.Value);
            }
        }

        public bool ContainsKey(TKey key)
        {
            lock (_lock)
            {
                return _dictionary.ContainsKey(key);
            }
        }

        public void CopyTo(HackerOs.OS.System.Collections.KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            lock (_lock)
            {
                int index = arrayIndex;
                foreach (var kvp in _dictionary)
                {
                    array[index++] = new HackerOs.OS.System.Collections.KeyValuePair<TKey, TValue>(kvp.Key, kvp.Value);
                }
            }
        }        public HackerOs.OS.System.Collections.IEnumerator<HackerOs.OS.System.Collections.KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            List<HackerOs.OS.System.Collections.KeyValuePair<TKey, TValue>> items;
            lock (_lock)
            {
                items = _dictionary.Select(kvp => new HackerOs.OS.System.Collections.KeyValuePair<TKey, TValue>(kvp.Key, kvp.Value)).ToList();
            }
            return new HackerOs.OS.System.Collections.ListEnumerator<HackerOs.OS.System.Collections.KeyValuePair<TKey, TValue>>(items);
        }

        HackerOs.OS.System.Collections.IEnumerator HackerOs.OS.System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Remove(TKey key)
        {
            lock (_lock)
            {
                return _dictionary.Remove(key);
            }
        }

        public bool Remove(HackerOs.OS.System.Collections.KeyValuePair<TKey, TValue> item)
        {
            lock (_lock)
            {
                if (_dictionary.TryGetValue(item.Key, out var value) && EqualityComparer<TValue>.Default.Equals(value, item.Value))
                {
                    return _dictionary.Remove(item.Key);
                }
                return false;
            }
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            lock (_lock)
            {
                return _dictionary.TryGetValue(key, out value!);
            }
        }

        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
        {
            lock (_lock)
            {
                if (_dictionary.TryGetValue(key, out var existingValue))
                    return existingValue;

                var newValue = valueFactory(key);
                _dictionary[key] = newValue;
                return newValue;
            }
        }
    }

    /// <summary>
    /// Represents a thread-safe first in-first out (FIFO) collection
    /// </summary>
    public class ConcurrentQueue<T>
    {
        private readonly Queue<T> _queue = new();
        private readonly object _lock = new();

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _queue.Count;
                }
            }
        }

        public bool IsEmpty
        {
            get
            {
                lock (_lock)
                {
                    return _queue.Count == 0;
                }
            }
        }

        public void Enqueue(T item)
        {
            lock (_lock)
            {
                _queue.Enqueue(item);
            }
        }

        public bool TryDequeue(out T result)
        {
            lock (_lock)
            {
                if (_queue.Count > 0)
                {
                    result = _queue.Dequeue();
                    return true;
                }
                result = default!;
                return false;
            }
        }

        public bool TryPeek(out T result)
        {
            lock (_lock)
            {
                if (_queue.Count > 0)
                {
                    result = _queue.Peek();
                    return true;
                }
                result = default!;
                return false;
            }
        }
    }

    /// <summary>
    /// Provides data for threading events
    /// </summary>
    public class ThreadingEventArgs : EventArgs
    {
        public int ThreadId { get; }
        public string ThreadName { get; }

        public ThreadingEventArgs(int threadId, string threadName)
        {
            ThreadId = threadId;
            ThreadName = threadName;
        }
    }
}
