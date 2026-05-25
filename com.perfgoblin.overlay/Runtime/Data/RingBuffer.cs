#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;

namespace PerfGoblin
{
    public class RingBuffer<T>
    {
        readonly T[] _buffer;
        int _head;
        int _count;

        public int Capacity => _buffer.Length;
        public int Count => _count;

        public T Latest => _count > 0
            ? _buffer[(_head - 1 + _buffer.Length) % _buffer.Length]
            : default;

        public RingBuffer(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _buffer = new T[capacity];
        }

        public void Push(T item)
        {
            _buffer[_head] = item;
            _head = (_head + 1) % _buffer.Length;
            if (_count < _buffer.Length)
                _count++;
        }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                    throw new IndexOutOfRangeException();
                int start = (_head - _count + _buffer.Length) % _buffer.Length;
                return _buffer[(start + index) % _buffer.Length];
            }
        }

        public void Clear()
        {
            _head = 0;
            _count = 0;
            Array.Clear(_buffer, 0, _buffer.Length);
        }

        public IEnumerable<T> AsEnumerable()
        {
            int start = (_head - _count + _buffer.Length) % _buffer.Length;
            for (int i = 0; i < _count; i++)
                yield return _buffer[(start + i) % _buffer.Length];
        }
    }
}
#endif
