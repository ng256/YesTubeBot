using System.Collections;
using System.Runtime.CompilerServices;
using VideoLibrary;

namespace VideoDownloader;

internal class YouTubeVideoCollection : IEnumerable<YouTubeVideo>, IEnumerable
{
    private readonly int _count;
    private readonly YouTubeVideo[] _collection;

    internal YouTubeVideoCollection(IEnumerable<YouTubeVideo> collection)
    {
        _collection = collection.ToArray() ?? throw new ArgumentNullException(nameof(collection));
        _count = _collection.Length;
    }

    public int Count => _count;

    public YouTubeVideo this[int n] => Get(n);


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ValidIndex(int n) => n >= 0 && n < _collection.Length;

    public YouTubeVideo Get(int n)
    {
        if (!ValidIndex(n))
            throw new ArgumentException("Index was outside of the bounds.");

        return _collection[n];
    }

    public IEnumerator<YouTubeVideo> GetEnumerator()
    {
        return new Enumerator(_collection);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private class Enumerator : IEnumerator<YouTubeVideo>
    {
        private readonly YouTubeVideo[] _collection;
        private int _position = -1;

        internal Enumerator(YouTubeVideo[] collection)
        {
            _collection = collection ?? throw new ArgumentNullException(nameof(collection));
        }

        public YouTubeVideo Current
        {
            get
            {
                if (_position < 0 || _position >= _collection.Length)
                    throw new InvalidOperationException();
                return _collection[_position];
            }
        }

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_position < _collection.Length - 1)
            {
                _position++;
                return true;
            }
            return false;
        }

        public void Reset()
        {
            _position = -1;
        }

        public void Dispose()
        {
        }
    }
}