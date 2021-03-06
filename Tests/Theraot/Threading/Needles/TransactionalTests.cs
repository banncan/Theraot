﻿#if FAT

using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Theraot.Collections.ThreadSafe;
using Theraot.Core;
using Theraot.Threading.Needles;

namespace Tests.Theraot.Threading.Needles
{
    [TestFixture]
    public class TransactionalTests
    {
        [Test]
        public void AbsentTransaction()
        {
            var needle = Transact.CreateNeedle(5);
            Assert.AreEqual(5, needle.Value);
        }

        [Test]
        public void FreeTest() // TODO: Review
        {
            var needle = Transact.CreateNeedle(1);
            using (var autoResetEvent = new AutoResetEvent(false))
            {
                // This one does not commit
                new Thread(() =>
                {
                    using (var transaction = new Transact())
                    {
                        needle.Free();
                        autoResetEvent.Set();
                    }
                }).Start();

                autoResetEvent.WaitOne();
                Assert.AreEqual(1, needle.Value);

                // This one commits
                new Thread(() =>
                {
                    using (var transaction = new Transact())
                    {
                        needle.Free();
                        transaction.Commit();
                        autoResetEvent.Set();
                    }
                }).Start();

                autoResetEvent.WaitOne();
                Assert.AreEqual(0, needle.Value);
            }
        }

        [Test]
        public void MultipleCommit() // TODO: Review
        {
            var needle = Transact.CreateNeedle(1);
            using (var autoResetEvent = new AutoResetEvent(false))
            {
                new Thread
                (
                    () =>
                    {
                        using (var transaction = new Transact())
                        {
                            needle.Value = 2;

                            transaction.Commit();

                            needle.Value = 3;

                            transaction.Commit();

                            needle.Value = 5;

                            transaction.Rollback();

                            autoResetEvent.Set();
                        }
                    }
                ).Start();
                autoResetEvent.WaitOne();
                Assert.AreEqual(3, needle.Value);
            }
        }

        [Test]
        public void NestedTransaction()
        {
            var needleA = Transact.CreateNeedle(5);
            var needleB = Transact.CreateNeedle(1);
            var thread = new Thread(() =>
            {
                using (var transaction = new Transact())
                {
                    needleB.Value = 2;

                    using (var transact = new Transact())
                    {
                        needleA.Value = 9;
                        Assert.AreEqual(9, needleA.Value);
                        Assert.AreEqual(2, needleB.Value);

                        transact.Commit();
                    }

                    Assert.AreEqual(9, needleA.Value);
                    Assert.AreEqual(2, needleB.Value);

                    transaction.Rollback();

                    Assert.AreEqual(5, needleA.Value);
                    Assert.AreEqual(1, needleB.Value);
                }
            });
            thread.Start();
            thread.Join();

            Assert.AreEqual(5, needleA.Value);
            Assert.AreEqual(1, needleB.Value);
        }

        [Test]
        public void NestedTransactionAndRollback()
        {
            var needleA = Transact.CreateNeedle(5);
            var needleB = Transact.CreateNeedle(1);
            var thread = new Thread(() =>
            {
                using (var transaction = new Transact())
                {
                    needleB.Value = 2;

                    transaction.Commit();

                    using (var transact = new Transact())
                    {
                        needleA.Value = 9;
                        Assert.AreEqual(9, needleA.Value);
                        Assert.AreEqual(2, needleB.Value);

                        transact.Commit();
                    }

                    Assert.AreEqual(9, needleA.Value);
                    Assert.AreEqual(2, needleB.Value);

                    transaction.Rollback();

                    Assert.AreEqual(5, needleA.Value);
                    Assert.AreEqual(2, needleB.Value);

                    using (new Transact())
                    {
                        needleA.Value = 13;
                        Assert.AreEqual(13, needleA.Value);
                        Assert.AreEqual(2, needleB.Value);

                        transaction.Rollback();

                        Assert.AreEqual(5, needleA.Value);
                        Assert.AreEqual(2, needleB.Value);
                    }

                    needleA.Value = 15;
                    transaction.Commit();
                }
            });
            thread.Start();
            thread.Join();

            Assert.AreEqual(15, needleA.Value);
            Assert.AreEqual(2, needleB.Value);
        }

        [Test]
        public void NoRaceCondition() // TODO: Review
        {
            using (var handle = new ManualResetEvent(false))
            {
                int[] count = { 0, 0 };
                var needleA = Transact.CreateNeedle(5);
                var needleB = Transact.CreateNeedle(5);
                Assert.AreEqual(needleA.Value, 5);
                Assert.AreEqual(needleB.Value, 5);
                ThreadPool.QueueUserWorkItem
                (
                    _ =>
                    {
                        using (var transact = new Transact())
                        {
                            Interlocked.Increment(ref count[0]);
                            handle.WaitOne();
                            needleA.Value += 2;
                            transact.Commit();
                        }
                        Interlocked.Increment(ref count[1]);
                    }
                );
                ThreadPool.QueueUserWorkItem
                (
                    _ =>
                    {
                        using (var transact = new Transact())
                        {
                            Interlocked.Increment(ref count[0]);
                            handle.WaitOne();
                            needleB.Value += 5;
                            transact.Commit();
                        }
                        Interlocked.Increment(ref count[1]);
                    }
                );
                global::Theraot.Threading.ThreadingHelper.SpinWaitUntil(ref count[0], 2);
                handle.Set();
                global::Theraot.Threading.ThreadingHelper.SpinWaitUntil(ref count[1], 2);
                // Both
                Assert.AreEqual(7, needleA.Value);
                Assert.AreEqual(10, needleB.Value);
                handle.Close();
            }
        }

        [Test]
        public void NotCommitedTransaction() // TODO: Review
        {
            var needle = Transact.CreateNeedle(5);
            using (var transact = new Transact())
            {
                needle.Value = 7;
            }
            Assert.AreEqual(needle.Value, 5);
        }

        [Test]
        public void RaceAndRetry() // TODO: Review
        {
            using (var handle = new ManualResetEvent(false))
            {
                int[] count = { 0, 0 };
                var needle = Transact.CreateNeedle(5);
                Assert.AreEqual(needle.Value, 5);
                ThreadPool.QueueUserWorkItem
                (
                    _ =>
                    {
                        using (var transact = new Transact())
                        {
                            Interlocked.Increment(ref count[0]);
                            do
                            {
                                Thread.Sleep(0);
                                handle.WaitOne();
                                needle.Value += 2;
                            } while (!transact.Commit());
                        }
                        Interlocked.Increment(ref count[1]);
                    }
                );
                ThreadPool.QueueUserWorkItem
                (
                    _ =>
                    {
                        using (var transact = new Transact())
                        {
                            Interlocked.Increment(ref count[0]);
                            do
                            {
                                Thread.Sleep(0);
                                handle.WaitOne();
                                needle.Value += 5;
                            } while (!transact.Commit());
                        }
                        Interlocked.Increment(ref count[1]);
                    }
                );
                while (Volatile.Read(ref count[0]) != 2)
                {
                    Thread.Sleep(0);
                }
                handle.Set();
                while (Volatile.Read(ref count[1]) != 2)
                {
                    Thread.Sleep(0);
                }
                // Both
                // This is initial 5 with +2 and +5 - that's 12
                Assert.AreEqual(12, needle.Value);
                handle.Close();
            }
        }

        [Test]
        public void RaceCondition() // TODO: Review
        {
            using (var handle = new ManualResetEvent(false))
            {
                int[] count = { 0, 0 };
                var needle = Transact.CreateNeedle(5);
                var winner = 0;
                Assert.AreEqual(needle.Value, 5);
                ThreadPool.QueueUserWorkItem
                (
                    _ =>
                    {
                        using (var transact = new Transact())
                        {
                            Interlocked.Increment(ref count[0]);
                            handle.WaitOne();
                            needle.Value += 2;
                            if (transact.Commit())
                            {
                                winner = 1;
                            }
                            Interlocked.Increment(ref count[1]);
                        }
                    }
                );
                ThreadPool.QueueUserWorkItem
                (
                    _ =>
                    {
                        using (var transact = new Transact())
                        {
                            Interlocked.Increment(ref count[0]);
                            handle.WaitOne();
                            needle.Value += 5;
                            if (transact.Commit())
                            {
                                winner = 2;
                            }
                            Interlocked.Increment(ref count[1]);
                        }
                    }
                );
                while (Volatile.Read(ref count[0]) != 2)
                {
                    Thread.Sleep(0);
                }
                handle.Set();
                while (Volatile.Read(ref count[1]) != 2)
                {
                    Thread.Sleep(0);
                }
                // One, the other, or both
                Assert.IsTrue((winner == 1 && needle.Value == 7) || (winner == 2 && needle.Value == 10) || (needle.Value == 12));
                handle.Close();
            }
        }

        [Test]
        public void ReadonlyTransaction() // TODO: Review
        {
            using (var handle = new ManualResetEvent(false))
            {
                int[] count = { 0, 0 };
                var needle = Transact.CreateNeedle(5);
                Assert.AreEqual(needle.Value, 5);
                ThreadPool.QueueUserWorkItem
                (
                    _ =>
                    {
                        using (var transact = new Transact())
                        {
                            Interlocked.Increment(ref count[0]);
                            handle.WaitOne();
                            // This one only reads
                            GC.KeepAlive(needle.Value);
                            transact.Commit();
                            Interlocked.Increment(ref count[1]);
                        }
                    }
                );
                ThreadPool.QueueUserWorkItem
                (
                    _ =>
                    {
                        using (var transact = new Transact())
                        {
                            Interlocked.Increment(ref count[0]);
                            handle.WaitOne();
                            needle.Value += 5;
                            transact.Commit();
                            Interlocked.Increment(ref count[1]);
                        }
                    }
                );
                while (Volatile.Read(ref count[0]) != 2)
                {
                    Thread.Sleep(0);
                }
                handle.Set();
                while (Volatile.Read(ref count[1]) != 2)
                {
                    Thread.Sleep(0);
                }
                // There is no reason for failure
                Assert.IsTrue(needle.Value == 10);
                handle.Close();
            }
        }

        [Test]
        public void Rollback()
        {
            var needleA = Transact.CreateNeedle(5);
            var needleB = Transact.CreateNeedle(5);
            try
            {
                using (var transact = new Transact())
                {
                    const int Movement = 2;
                    needleA.Value += Movement;
                    ThrowException();
                    // Really, it is evident this code will not run
                    needleB.Value -= Movement;
                    transact.Commit();
                }
            }
            catch (Exception exception)
            {
                Theraot.No.Op(exception);
            }
            // We did not commit
            Assert.AreEqual(5, needleA.Value);
            Assert.AreEqual(5, needleB.Value);

            //---

            using (var transact = new Transact())
            {
                needleA.Value = 9;
                Assert.AreEqual(9, needleA.Value);
                Assert.AreEqual(5, needleB.Value);

                transact.Rollback();

                Assert.AreEqual(5, needleA.Value);
                Assert.AreEqual(5, needleB.Value);
            }
            // We did rollback
            Assert.AreEqual(5, needleA.Value);
            Assert.AreEqual(5, needleB.Value);

            using (var transact = new Transact())
            {
                needleA.Value = 9;
                Assert.AreEqual(9, needleA.Value);
                Assert.AreEqual(5, needleB.Value);

                transact.Rollback();

                Assert.AreEqual(5, needleA.Value);
                Assert.AreEqual(5, needleB.Value);
                needleA.Value = 11;
                Assert.AreEqual(11, needleA.Value);
                Assert.AreEqual(5, needleB.Value);

                transact.Commit();
            }

            // We did rollback and commit again
            Assert.AreEqual(11, needleA.Value);
            Assert.AreEqual(5, needleB.Value);
        }

        [Test]
        public void SimpleTest() // TODO: Review
        {
            var needle = Transact.CreateNeedle(1);
            using (var autoResetEvent = new AutoResetEvent(false))
            {
                // This one does not commit
                new Thread(() =>
                {
                    using (var transaction = new Transact())
                    {
                        needle.Value = 2;
                        autoResetEvent.Set();
                    }
                }).Start();

                autoResetEvent.WaitOne();
                Assert.AreEqual(1, needle.Value);

                // This one commits
                new Thread(() =>
                {
                    using (var transaction = new Transact())
                    {
                        needle.Value = 2;
                        transaction.Commit();
                        autoResetEvent.Set();
                    }
                }).Start();

                autoResetEvent.WaitOne();
                Assert.AreEqual(2, needle.Value);
            }
        }

        [Test]
        public void SimpleTransaction()
        {
            var needle = Transact.CreateNeedle(5);
            using (var transact = new Transact())
            {
                needle.Value = 7;
                transact.Commit();
            }
            Assert.AreEqual(needle.Value, 7);
        }

        [Test]
        public void TransactionalDataStructure()
        {
            var info = new CircularBucket<string>(32);
            var bucket = new NeedleBucket<int, Transact.Needle<int>>(index => index, value => new Transact.Needle<int>(value), 5);
            var didA = false;
            bool didB;
            using (var enteredWorkA = new ManualResetEvent(false))
            {
                using (var enteredWorkB = new ManualResetEvent(false))
                {
                    using (var done = new ManualResetEvent(false))
                    {
                        ManualResetEvent[] handles = { enteredWorkA, enteredWorkB, done };
                        ThreadPool.QueueUserWorkItem
                        (
                            _ =>
                            {
                                info.Add("Work A - start");
                                using (var transact = new Transact())
                                {
                                    info.Add("Work A - enter");
                                    handles[0].Set();
                                    info.Add("Work A - reported, waiting");
                                    handles[1].WaitOne();
                                    info.Add("Work A - going");
                                    // foreach will not trigger the creation of items
                                    var got = new int[5];
                                    var set = new int[5];
                                    for (var index = 0; index < 5; index++)
                                    {
                                        got[index] = bucket.GetNeedle(index).Value;
                                        set[index] = got[index] + 1;
                                        bucket.GetNeedle(index).Value = set[index];
                                    }
                                    info.Add(string.Format("Work A - Got: [{0}, {1}, {2}, {3}, {4}] - Set: [{5}, {6}, {7}, {8}, {9}]", got[0], got[1], got[2], got[3], got[4], set[0], set[1], set[2], set[3], set[4]));
                                    if (!bucket.SequenceEqual(set))
                                    {
                                        info.Add("Work A - ??");
                                    }
                                    info.Add("Work A - before commit");
                                    didA = transact.Commit();
                                    info.Add("Work A - after commit: " + didA.ToString());
                                    if (didA != bucket.SequenceEqual(set))
                                    {
                                        info.Add("Work A - ???");
                                    }
                                    info.Add("Work A - report");
                                    handles[2].Set();
                                    info.Add("Work A - done");
                                }
                            }
                        );
                        {
                            info.Add("Work B - start");
                            using (var transact = new Transact())
                            {
                                info.Add("Work B - waiting A to enter");
                                handles[0].WaitOne();
                                info.Add("Work B - telling Work A to go");
                                handles[1].Set();
                                info.Add("Work B - going");
                                // foreach will not trigger the creation of items
                                var got = new int[5];
                                var set = new int[5];
                                for (var index = 0; index < 5; index++)
                                {
                                    got[index] = bucket.GetNeedle(index).Value;
                                    set[index] = got[index] * 2;
                                    bucket.GetNeedle(index).Value = set[index];
                                }
                                info.Add(string.Format("Work B - Got: [{0}, {1}, {2}, {3}, {4}] - Set: [{5}, {6}, {7}, {8}, {9}]", got[0], got[1], got[2], got[3], got[4], set[0], set[1], set[2], set[3], set[4]));
                                if (!bucket.SequenceEqual(set))
                                {
                                    info.Add("Work B - ??");
                                }
                                info.Add("Work B - before commit");
                                didB = transact.Commit();
                                info.Add("Work B - after commit: " + didB.ToString());
                                if (didB != bucket.SequenceEqual(set))
                                {
                                    info.Add("Work B - ???");
                                }
                                info.Add("Work B - waiting report");
                                handles[2].WaitOne();
                                info.Add("Work B - done");
                            }
                        }
                        var result = bucket;
                        // These are more likely in debug mode
                        // (+1)
                        if (result.SequenceEqual(new[] { 1, 2, 3, 4, 5 }))
                        {
                            Assert.IsTrue(didA);
                            Assert.IsFalse(didB);
                            return;
                        }
                        // (*2)
                        if (result.SequenceEqual(new[] { 0, 2, 4, 6, 8 }))
                        {
                            Assert.IsFalse(didA);
                            Assert.IsTrue(didB);
                            return;
                        }
                        // This are more likely with optimization enabled
                        // (+1) and then (*2)
                        if (result.SequenceEqual(new[] { 2, 4, 6, 8, 10 }))
                        {
                            Assert.IsTrue(didA);
                            Assert.IsTrue(didB);
                            return;
                        }
                        // (*2) and then (+1)
                        if (result.SequenceEqual(new[] { 1, 3, 5, 7, 9 }))
                        {
                            Assert.IsTrue(didA);
                            Assert.IsTrue(didB);
                            return;
                        }
                        //---
                        if (result.SequenceEqual(new[] { 0, 1, 2, 3, 4 }))
                        {
                            Assert.IsFalse(didA);
                            Assert.IsFalse(didB);
                            return;
                        }
                        var found = result.ToArray();
                        Trace.WriteLine(" --- REPORT --- ");
                        foreach (var msg in info)
                        {
                            Trace.WriteLine(msg);
                        }
                        Assert.Fail("T_T - This is what was found: [{0}, {1}, {2}, {3}, {4}]", found[0], found[1], found[2], found[3], found[4]);
                    }
                }
            }
        }

        [Test]
        public void UsingClonable()
        {
            var needle = Transact.CreateNeedle(new ClonableClass(7));
            using (var transact = new Transact())
            {
                needle.Value.Value = 9;
                Assert.AreEqual(9, needle.Value.Value);
                transact.Commit();
            }
            Assert.AreEqual(9, needle.Value.Value);
        }

        private static void ThrowException()
        {
            throw new InvalidOperationException("Oh no!");
        }

        private class ClonableClass : ICloneable<ClonableClass>
        {
            public ClonableClass(int value)
            {
                Value = value;
            }

            public int Value { get; set; }

            public ClonableClass Clone()
            {
                return new ClonableClass(Value);
            }

            object ICloneable.Clone()
            {
                return Clone();
            }
        }
    }
}

#endif