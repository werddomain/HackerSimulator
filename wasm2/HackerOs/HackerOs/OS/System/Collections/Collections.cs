using System;
using System.Collections.Generic;

namespace HackerOs.OS.System.Collections
{
    /// <summary>
    /// Base interface for enumerators
    /// </summary>
    public interface IEnumerator
    {
        /// <summary>
        /// Gets the current element in the collection
        /// </summary>
        object? Current { get; }

        /// <summary>
        /// Advances the enumerator to the next element
        /// </summary>
        bool MoveNext();

        /// <summary>
        /// Sets the enumerator to its initial position
        /// </summary>
        void Reset();
    }

    /// <summary>
    /// Generic enumerator interface
    /// </summary>
    /// <typeparam name="T">The type of objects to enumerate</typeparam>
    public interface IEnumerator<out T> : IEnumerator, IDisposable
    {
        /// <summary>
        /// Gets the current element in the collection
        /// </summary>
        new T Current { get; }
    }

    /// <summary>
    /// Base interface for enumerable collections
    /// </summary>
    public interface IEnumerable
    {
        /// <summary>
        /// Returns an enumerator that iterates through a collection
        /// </summary>
        IEnumerator GetEnumerator();
    }

    /// <summary>
    /// Generic enumerable interface
    /// </summary>
    /// <typeparam name="T">The type of objects to enumerate</typeparam>
    public interface IEnumerable<out T> : IEnumerable
    {
        /// <summary>
        /// Returns an enumerator that iterates through the collection
        /// </summary>
        new IEnumerator<T> GetEnumerator();
    }

    /// <summary>
    /// Interface for collections
    /// </summary>
    /// <typeparam name="T">The type of elements in the collection</typeparam>
    public interface ICollection<T> : IEnumerable<T>
    {
        /// <summary>
        /// Gets the number of elements contained in the collection
        /// </summary>
        int Count { get; }

        /// <summary>
        /// Gets a value indicating whether the collection is read-only
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>
        /// Adds an item to the collection
        /// </summary>
        void Add(T item);

        /// <summary>
        /// Removes all items from the collection
        /// </summary>
        void Clear();

        /// <summary>
        /// Determines whether the collection contains a specific value
        /// </summary>
        bool Contains(T item);

        /// <summary>
        /// Copies the elements of the collection to an Array
        /// </summary>
        void CopyTo(T[] array, int arrayIndex);

        /// <summary>
        /// Removes the first occurrence of a specific object from the collection
        /// </summary>
        bool Remove(T item);
    }

    /// <summary>
    /// Interface for lists
    /// </summary>
    /// <typeparam name="T">The type of elements in the list</typeparam>
    public interface IList<T> : ICollection<T>
    {
        /// <summary>
        /// Gets or sets the element at the specified index
        /// </summary>
        T this[int index] { get; set; }

        /// <summary>
        /// Determines the index of a specific item in the list
        /// </summary>
        int IndexOf(T item);

        /// <summary>
        /// Inserts an item to the list at the specified index
        /// </summary>
        void Insert(int index, T item);

        /// <summary>
        /// Removes the list item at the specified index
        /// </summary>
        void RemoveAt(int index);
    }

    /// <summary>
    /// Interface for dictionaries
    /// </summary>
    /// <typeparam name="TKey">The type of keys in the dictionary</typeparam>
    /// <typeparam name="TValue">The type of values in the dictionary</typeparam>
    public interface IDictionary<TKey, TValue> : ICollection<KeyValuePair<TKey, TValue>>
    {
        /// <summary>
        /// Gets or sets the element with the specified key
        /// </summary>
        TValue this[TKey key] { get; set; }

        /// <summary>
        /// Gets a collection containing the keys of the dictionary
        /// </summary>
        ICollection<TKey> Keys { get; }

        /// <summary>
        /// Gets a collection containing the values in the dictionary
        /// </summary>
        ICollection<TValue> Values { get; }

        /// <summary>
        /// Adds an element with the provided key and value to the dictionary
        /// </summary>
        void Add(TKey key, TValue value);

        /// <summary>
        /// Determines whether the dictionary contains an element with the specified key
        /// </summary>
        bool ContainsKey(TKey key);

        /// <summary>
        /// Removes the element with the specified key from the dictionary
        /// </summary>
        bool Remove(TKey key);

        /// <summary>
        /// Gets the value associated with the specified key
        /// </summary>
        bool TryGetValue(TKey key, out TValue value);
    }

    /// <summary>
    /// Represents a key-value pair
    /// </summary>
    /// <typeparam name="TKey">The type of the key</typeparam>
    /// <typeparam name="TValue">The type of the value</typeparam>
    public struct KeyValuePair<TKey, TValue>
    {
        /// <summary>
        /// Gets the key in the key-value pair
        /// </summary>
        public TKey Key { get; }

        /// <summary>
        /// Gets the value in the key-value pair
        /// </summary>
        public TValue Value { get; }

        /// <summary>
        /// Initializes a new instance of the KeyValuePair structure
        /// </summary>
        public KeyValuePair(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }

        /// <summary>
        /// Returns a string representation of the key-value pair
        /// </summary>
        public override string ToString()
        {
            return $"[{Key}, {Value}]";
        }
    }

    /// <summary>
    /// An enumerator implementation for lists.
    /// </summary>
    /// <typeparam name="T">The type of elements in the list</typeparam>
    public class ListEnumerator<T> : IEnumerator<T>
    {
        private List<T> _list;
        private int _index;
        private T _current;

        /// <summary>
        /// Initializes a new instance of the ListEnumerator class.
        /// </summary>
        /// <param name="list">The list to enumerate</param>
        public ListEnumerator(List<T> list)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
            _index = -1;
            _current = default!;
        }

        /// <summary>
        /// Gets the current element in the collection
        /// </summary>
        public T Current => _current;

        object? IEnumerator.Current => Current;

        /// <summary>
        /// Advances the enumerator to the next element
        /// </summary>
        /// <returns>true if the enumerator was successfully advanced to the next element; 
        /// false if the enumerator has passed the end of the collection.</returns>
        public bool MoveNext()
        {
            if (++_index >= _list.Count)
            {
                return false;
            }
            
            _current = _list[_index];
            return true;
        }

        /// <summary>
        /// Sets the enumerator to its initial position
        /// </summary>
        public void Reset()
        {
            _index = -1;
            _current = default!;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            // No unmanaged resources to dispose
        }
    }
}
