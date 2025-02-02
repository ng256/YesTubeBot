using System.Collections;
using System.Runtime.CompilerServices;
using VideoLibrary;

namespace VideoDownloader;

/// <summary>
/// Represents a read-only collection of YouTube videos that can be accessed by index
/// </summary>
/// <remarks>
/// Provides type-safe enumeration and direct access to video elements
/// </remarks>
internal class YouTubeVideoCollection : IEnumerable<YouTubeVideo>, IEnumerable
{
    private readonly int _count;
    private readonly YouTubeVideo[] _collection;

    /// <summary>
    /// Initializes a new instance of the YouTubeVideoCollection class
    /// </summary>
    /// <param name="collection">The collection of YouTube videos to wrap</param>
    /// <exception cref="ArgumentNullException">Thrown when collection parameter is null</exception>
    internal YouTubeVideoCollection(IEnumerable<YouTubeVideo> collection)
    {
        _collection = collection.ToArray() ?? throw new ArgumentNullException(nameof(collection));
        _count = _collection.Length;
    }

    /// <summary>
    /// Gets the number of elements contained in the collection
    /// </summary>
    /// <value>The total number of YouTube videos in the collection</value>
    public int Count => _count;

    /// <summary>
    /// Gets the YouTubeVideo at the specified index
    /// </summary>
    /// <param name="n">The zero-based index of the element to get</param>
    /// <returns>The YouTubeVideo at the specified index</returns>
    /// <exception cref="ArgumentException">Thrown when index is out of range</exception>
    public YouTubeVideo this[int n] => Get(n);

    /// <summary>
    /// Determines if the specified index is valid for the collection
    /// </summary>
    /// <param name="n">The index to validate</param>
    /// <returns>
    /// <c>true</c> if the index is within the valid range; <c>false</c> otherwise
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ValidIndex(int n) => n >= 0 && n < _collection.Length;

    /// <summary>
    /// Retrieves the YouTubeVideo at the specified index
    /// </summary>
    /// <param name="n">The zero-based index of the video to retrieve</param>
    /// <returns>The YouTubeVideo at the specified position</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when index is less than 0 or equal to/greater than collection size
    /// </exception>
    public YouTubeVideo Get(int n)
    {
        if (!ValidIndex(n))
            throw new ArgumentException("Index was outside of the bounds.");

        return _collection[n];
    }

    /// <summary>
    /// Returns an enumerator that iterates through the collection
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the collection</returns>
    public IEnumerator<YouTubeVideo> GetEnumerator()
    {
        return new Enumerator(_collection);
    }

    /// <summary>
    /// Returns a non-generic enumerator that iterates through the collection
    /// </summary>
    /// <returns>An enumerator that can be used to iterate through the collection</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Provides iteration functionality for the YouTubeVideoCollection
    /// </summary>
    /// <remarks>
    /// Implements both generic and non-generic enumeration interfaces
    /// </remarks>
    private class Enumerator : IEnumerator<YouTubeVideo>
    {
        private readonly YouTubeVideo[] _collection;
        private int _position = -1;

        /// <summary>
        /// Initializes a new instance of the Enumerator class
        /// </summary>
        /// <param name="collection">The array of YouTube videos to enumerate</param>
        /// <exception cref="ArgumentNullException">Thrown when collection parameter is null</exception>
        internal Enumerator(YouTubeVideo[] collection)
        {
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }

        /// <summary>
        /// Gets the current element in the collection
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when enumeration has not started or has finished
        /// </exception>
        public YouTubeVideo Current
        {
            get
            {
                if (_position < 0 || _position >= _collection.Length)
                    throw new InvalidOperationException();
                return _collection[_position];
            }
        }

        /// <summary>
        /// Gets the current element in the collection (non-generic interface implementation)
        /// </summary>
        object IEnumerator.Current => Current;

        /// <summary>
        /// Advances the enumerator to the next element of the collection
        /// </summary>
        /// <returns>
        /// <c>true</c> if the enumerator was successfully advanced to the next element; 
        /// <c>false</c> if the enumerator has passed the end of the collection
        /// </returns>
        public bool MoveNext()
        {
            if (_position < _collection.Length - 1)
            {
                _position++;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sets the enumerator to its initial position, before the first element in the collection
        /// </summary>
        public void Reset()
        {
            _position = -1;
        }

        /// <summary>
        /// Releases all resources used by the enumerator
        /// </summary>
        /// <remarks>
        /// This implementation does not require resource cleanup
        /// </remarks>
        public void Dispose()
        {
            // No resources to dispose in current implementation
        }
    }
}
