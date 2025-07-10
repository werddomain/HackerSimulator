using System;
using System.Collections;
using System.Collections.Generic;

namespace HackerOs.OS.HSystem.Collections
{
    /// <summary>
    /// This file provides compatibility with existing code.
    /// For new code, use System.Collections and System.Collections.Generic directly.
    /// </summary>
    
    /// <summary>
    /// Compatibility type for HackerOs.OS.HSystem.Collections.Generic namespace
    /// </summary>
    namespace Generic
    {
        // Empty namespace to allow imports of HackerOs.OS.HSystem.Collections.Generic to work
        // The actual types should be used from System.Collections.Generic
    }
    
    // Use type aliases to forward to the .NET types
    // This is safer than inheritance which causes cycles
    
    /// <summary>
    /// Type alias functions to create standard collection types
    /// </summary>
    public static class CollectionFactory
    {
        /// <summary>
        /// Creates a new List
        /// </summary>
        public static List<T> CreateList<T>() => new List<T>();
        
        /// <summary>
        /// Creates a new Dictionary
        /// </summary>
        public static Dictionary<TKey, TValue> CreateDictionary<TKey, TValue>() 
            where TKey : notnull => new Dictionary<TKey, TValue>();
            
        /// <summary>
        /// Creates a new HashSet
        /// </summary>
        public static HashSet<T> CreateHashSet<T>() => new HashSet<T>();
    }
}
