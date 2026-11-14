using System;
using System.Collections.Concurrent;
using System.Threading;

namespace PeakEngine.Core
{
    /// <summary>
    /// Runs work on the game's main thread.
    ///
    /// THE PROBLEM: web requests from the control panel arrive on background threads, but Unity
    /// (the engine PEAK is built on) only allows game objects to be touched from its one main thread.
    /// Touching them from anywhere else can crash the game.
    ///
    /// THE FIX: a background thread puts a "job" in a queue and waits. Once per frame the game calls
    /// <see cref="Pump"/> on the main thread, which runs the queued jobs and hands back the results.
    ///
    /// If you know JavaScript: this is the same idea as the event loop. Game code runs on a single
    /// thread, and everyone else has to queue work for it.
    /// </summary>
    public static class MainThread
    {
        private const int Pending = 0, Started = 1, Abandoned = 2;

        private sealed class Job
        {
            public Func<object> Work;
            public object Result;
            public Exception Error;
            public int State = Pending;
            public readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
        }

        private static readonly ConcurrentQueue<Job> Queue = new ConcurrentQueue<Job>();

        /// <summary>
        /// Called from a background thread. Queues <paramref name="work"/> for the main thread and
        /// blocks until it has run. Throws <see cref="TimeoutException"/> if the game does not get
        /// to it in time (for example during a loading screen).
        /// </summary>
        public static object Run(Func<object> work, int timeoutMs = 5000)
        {
            var job = new Job { Work = work };
            Queue.Enqueue(job);

            if (!job.Done.Wait(timeoutMs))
            {
                // Try to cancel. If the main thread already started the job, we must wait for it to finish.
                if (Interlocked.CompareExchange(ref job.State, Abandoned, Pending) == Pending)
                    throw new TimeoutException("The game did not respond in time. It may be loading or paused.");
                job.Done.Wait();
            }

            if (job.Error != null) throw job.Error;
            return job.Result;
        }

        /// <summary>Called once per frame on the main thread. Runs everything that was queued.</summary>
        public static void Pump()
        {
            while (Queue.TryDequeue(out var job))
            {
                // Skip jobs whose caller already gave up, so a cheat never fires long after it was clicked.
                if (Interlocked.CompareExchange(ref job.State, Started, Pending) != Pending) continue;
                try { job.Result = job.Work(); }
                catch (Exception e) { job.Error = e; }
                finally { job.Done.Set(); }
            }
        }
    }
}
