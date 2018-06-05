﻿using NUnit.Framework;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Theraot.Collections.ThreadSafe;

namespace Tests.System.Threading
{
    [TestFixtureAttribute]
    public static class SemaphoreSlimTestsEx
    {
        [Test]
        public static void WaitAsyncWaitCorrectly()
        {
            var maxCount = 3;
            var maxTasks = 4;
            var log = new CircularBucket<string>(128);
            var logCount = new CircularBucket<int>(maxTasks);
            using (var source = new CancellationTokenSource(TimeSpan.FromSeconds(100)))
            {
                using (var semaphore = new SemaphoreSlim(0, maxCount))
                {
                    // No task should be able to enter semaphore at this point.
                    // Thus semaphore.CurrentCount should be 0
                    // We can directly check
                    Assert.AreEqual(0, semaphore.CurrentCount);
                    var padding = 0;
                    var tasks = Enumerable.Range(0, maxTasks)
                        .Select
                        (
                            _ =>
                            {
                                return Task.Factory.StartNew
                                (
                                    async () =>
                                    {
                                        var CurrentId = Task.CurrentId;
                                        log.Add(string.Format("Task {0} begins and waits for the semaphore.", CurrentId));
                                        await semaphore.WaitAsync(source.Token);
                                        Interlocked.Add(ref padding, 100);
                                        log.Add(string.Format("Task {0} enters the semaphore.", CurrentId));
                                        Thread.Sleep(1000 + padding);
                                        // Calling release should give weakingly increasing results
                                        var count = semaphore.Release();
                                        logCount.Add(count);
                                        log.Add(string.Format("Task {0} release the semaphore; previous count: {1} ", CurrentId, count));
                                    }
                                ).Unwrap();
                            }
                        ).ToArray();
                    Thread.Sleep(TimeSpan.FromMilliseconds(500));
                    log.Add(string.Format("Main thread call Release({0}) -->", maxCount));
                    semaphore.Release(maxCount);
                    Task.WaitAll(tasks, source.Token);
                    log.Add("Main thread exits");
                }
            }
            foreach (var entry in log)
            {
                Console.WriteLine(entry);
            }
            foreach (var entry in logCount)
            {
                Console.WriteLine(entry.ToString());
            }
        }
    }
}