using System;
using System.Linq;
using NUnit.Framework;

namespace PerfGoblin.Tests
{
    public class RingBufferTests
    {
        [Test]
        public void NewBuffer_HasZeroCount()
        {
            var buffer = new RingBuffer<int>(10);
            Assert.AreEqual(0, buffer.Count);
            Assert.AreEqual(10, buffer.Capacity);
        }

        [Test]
        public void Push_IncreasesCount()
        {
            var buffer = new RingBuffer<int>(5);
            buffer.Push(42);
            Assert.AreEqual(1, buffer.Count);
            buffer.Push(43);
            Assert.AreEqual(2, buffer.Count);
        }

        [Test]
        public void Push_BeyondCapacity_CountClampsToCapacity()
        {
            var buffer = new RingBuffer<int>(3);
            for (int i = 0; i < 10; i++)
                buffer.Push(i);

            Assert.AreEqual(3, buffer.Count);
        }

        [Test]
        public void Push_BeyondCapacity_OverwritesOldest()
        {
            var buffer = new RingBuffer<int>(3);
            buffer.Push(1);
            buffer.Push(2);
            buffer.Push(3);
            buffer.Push(4);

            Assert.AreEqual(2, buffer[0]);
            Assert.AreEqual(3, buffer[1]);
            Assert.AreEqual(4, buffer[2]);
        }

        [Test]
        public void Indexer_ReturnsItemsInInsertionOrder()
        {
            var buffer = new RingBuffer<int>(5);
            buffer.Push(10);
            buffer.Push(20);
            buffer.Push(30);

            Assert.AreEqual(10, buffer[0]);
            Assert.AreEqual(20, buffer[1]);
            Assert.AreEqual(30, buffer[2]);
        }

        [Test]
        public void Indexer_NegativeIndex_Throws()
        {
            var buffer = new RingBuffer<int>(5);
            buffer.Push(1);

            Assert.Throws<IndexOutOfRangeException>(() => { var _ = buffer[-1]; });
        }

        [Test]
        public void Indexer_OutOfRange_Throws()
        {
            var buffer = new RingBuffer<int>(5);
            buffer.Push(1);

            Assert.Throws<IndexOutOfRangeException>(() => { var _ = buffer[1]; });
        }

        [Test]
        public void Latest_ReturnsLastPushedItem()
        {
            var buffer = new RingBuffer<int>(5);
            buffer.Push(10);
            buffer.Push(20);
            buffer.Push(30);

            Assert.AreEqual(30, buffer.Latest);
        }

        [Test]
        public void Latest_EmptyBuffer_ReturnsDefault()
        {
            var buffer = new RingBuffer<int>(5);
            Assert.AreEqual(0, buffer.Latest);
        }

        [Test]
        public void Clear_ResetsCountToZero()
        {
            var buffer = new RingBuffer<int>(5);
            buffer.Push(1);
            buffer.Push(2);
            buffer.Clear();

            Assert.AreEqual(0, buffer.Count);
        }

        [Test]
        public void Clear_ThenPush_WorksCorrectly()
        {
            var buffer = new RingBuffer<int>(3);
            buffer.Push(1);
            buffer.Push(2);
            buffer.Clear();
            buffer.Push(99);

            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual(99, buffer[0]);
        }

        [Test]
        public void AsEnumerable_ReturnsItemsInOrder()
        {
            var buffer = new RingBuffer<int>(5);
            buffer.Push(10);
            buffer.Push(20);
            buffer.Push(30);

            var items = buffer.AsEnumerable().ToArray();
            Assert.AreEqual(new[] { 10, 20, 30 }, items);
        }

        [Test]
        public void AsEnumerable_AfterWrap_ReturnsCorrectOrder()
        {
            var buffer = new RingBuffer<int>(3);
            buffer.Push(1);
            buffer.Push(2);
            buffer.Push(3);
            buffer.Push(4);
            buffer.Push(5);

            var items = buffer.AsEnumerable().ToArray();
            Assert.AreEqual(new[] { 3, 4, 5 }, items);
        }

        [Test]
        public void Constructor_ZeroCapacity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RingBuffer<int>(0));
        }

        [Test]
        public void Constructor_NegativeCapacity_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new RingBuffer<int>(-1));
        }

        [Test]
        public void SingleCapacity_AlwaysHoldsLastItem()
        {
            var buffer = new RingBuffer<string>(1);
            buffer.Push("a");
            Assert.AreEqual("a", buffer[0]);

            buffer.Push("b");
            Assert.AreEqual(1, buffer.Count);
            Assert.AreEqual("b", buffer[0]);
            Assert.AreEqual("b", buffer.Latest);
        }
    }
}
