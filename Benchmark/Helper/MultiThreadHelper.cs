using System;
using System.Threading;
using System.Threading.Tasks;

namespace DataflowBench.Helper
{
    public class MultiThreadHelper
    {
        // Signal to start work
        private bool _canStart = false;
        // Number of threads ready for work
        private int _ready = 0;
        // Number of threads complated work
        private int _complate = 0;
        // Number of threads
        private int _jobs = 0;
        private readonly bool _useThreadPool;

        public MultiThreadHelper() : this(false)
        {
        }
        public MultiThreadHelper(bool useThreadPool)
        {
            _useThreadPool = useThreadPool;
        }

        public void AddJob(Action action)
        {
            _jobs++;

            if (_useThreadPool)
            {
                Task.Factory.StartNew(() =>
                {
                    Job(action);
                },
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
            else
            {
                new Thread(() => Job(action)).Start();
            }
        }

        /// <summary>
        /// Adds a task that will stop when the main test ends.
        /// This is necessary to make an additional load to simulate different scenarios.
        /// </summary>
        /// <param name="action"></param>
        public void AddBackgroundJob(Action action)
        {
            if (_useThreadPool)
            {
                Task.Factory.StartNew(() =>
                {
                    BackgroundJob(action);
                },
                CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
            else
            {
                new Thread(() => BackgroundJob(action)).Start();
            }
        }

        // Wait all threads are ready
        public void WaitReady()
        {
            while (Volatile.Read(ref _ready) < _jobs)
            {
                Thread.Yield();
            }
        }

        public void Start()
        {
            Volatile.Write(ref _canStart, true);

            while (Volatile.Read(ref _complate) < _jobs)
            {
                Thread.Yield();
            }
        }

        private void Job(Action action)
        {
            Interlocked.Add(ref _ready, 1);

            while (!Volatile.Read(ref _canStart))
            {
                Thread.Yield();
            }

            action();

            Interlocked.Add(ref _complate, 1);
        }

        private void BackgroundJob(Action action)
        {
            while (!Volatile.Read(ref _canStart))
            {
                Thread.Yield();
            }

            var jobs = Volatile.Read(ref _jobs);

            while (Volatile.Read(ref _complate) < jobs)
            {
                action();
            }

            Interlocked.Add(ref _complate, 1);
        }
    }
}
