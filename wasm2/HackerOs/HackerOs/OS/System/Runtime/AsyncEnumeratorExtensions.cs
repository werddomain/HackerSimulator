using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace HackerOs.OS.System.Runtime
{
    /// <summary>
    /// Provides functionality for asynchronous operations in the HackerOS system.
    /// </summary>
    public static class AsyncEnumeratorExtensions
    {
        /// <summary>
        /// Configures how awaits on the tasks returned from an async disposable are performed.
        /// </summary>
        public static ConfiguredCancelableAsyncEnumerable<T> ConfigureAwait<T>(
            this IAsyncEnumerable<T> enumerable, 
            bool continueOnCapturedContext)
        {
            return new ConfiguredCancelableAsyncEnumerable<T>(enumerable, continueOnCapturedContext, default);
        }

        /// <summary>
        /// Enables cancellation and configured awaits on an async enumerable.
        /// </summary>
        public static ConfiguredCancelableAsyncEnumerable<T> WithCancellation<T>(
            this IAsyncEnumerable<T> enumerable,
            global::System.Threading.CancellationToken cancellationToken)
        {
            return new ConfiguredCancelableAsyncEnumerable<T>(enumerable, true, cancellationToken);
        }
    }

    /// <summary>
    /// Provides a configurable wrapper around an async enumerable.
    /// </summary>
    public readonly struct ConfiguredCancelableAsyncEnumerable<T>
    {
        private readonly IAsyncEnumerable<T> _enumerable;
        private readonly bool _continueOnCapturedContext;
        private readonly global::System.Threading.CancellationToken _cancellationToken;

        internal ConfiguredCancelableAsyncEnumerable(
            IAsyncEnumerable<T> enumerable, 
            bool continueOnCapturedContext, 
            global::System.Threading.CancellationToken cancellationToken)
        {
            _enumerable = enumerable ?? throw new ArgumentNullException(nameof(enumerable));
            _continueOnCapturedContext = continueOnCapturedContext;
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Returns an enumerator that iterates asynchronously through the collection.
        /// </summary>
        public ConfiguredCancelableAsyncEnumerator<T> GetAsyncEnumerator()
        {
            return new ConfiguredCancelableAsyncEnumerator<T>(
                _enumerable.GetAsyncEnumerator(_cancellationToken), 
                _continueOnCapturedContext);
        }
    }

    /// <summary>
    /// Provides a configurable wrapper around an async enumerator.
    /// </summary>
    public readonly struct ConfiguredCancelableAsyncEnumerator<T>
    {
        private readonly IAsyncEnumerator<T> _enumerator;
        private readonly bool _continueOnCapturedContext;

        internal ConfiguredCancelableAsyncEnumerator(IAsyncEnumerator<T> enumerator, bool continueOnCapturedContext)
        {
            _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
            _continueOnCapturedContext = continueOnCapturedContext;
        }

        /// <summary>
        /// Gets the current element in the iteration.
        /// </summary>
        public T Current => _enumerator.Current;

        /// <summary>
        /// Advances the enumerator asynchronously to the next element of the collection.
        /// </summary>
        public ConfiguredValueTaskAwaitable<bool> MoveNextAsync()
        {
            return _enumerator.MoveNextAsync().ConfigureAwait(_continueOnCapturedContext);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources asynchronously.
        /// </summary>
        public ConfiguredValueTaskAwaitable DisposeAsync()
        {
            return _enumerator.DisposeAsync().ConfigureAwait(_continueOnCapturedContext);
        }
    }
}

namespace HackerOs.OS.System.Runtime.CompilerServices
{
    /// <summary>
    /// Indicates that a parameter captures the cancellation token passed to an async enumerable method.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    public sealed class EnumeratorCancellationAttribute : Attribute
    {
        public EnumeratorCancellationAttribute() { }
    }
}
