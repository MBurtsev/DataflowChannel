using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Benchmark
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

        public MultiThreadHelper():this(false)
        {
        }
        public MultiThreadHelper(bool useThreadPool)
        {
            _useThreadPool = useThreadPool;
        }

        public void AddJob(Action action)
        {
            _jobs++;
            Console.WriteLine($"Jod added, jobs:{_jobs}");

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
                var thread = new Thread(() => Job(action));

                thread.Start();
            }
        }

        // Wait all threads are ready
        public void WaitReady()
        {
            while (Volatile.Read(ref _ready) < _jobs)
            {
            }
        }

        public void Start()
        {
            Volatile.Write(ref _canStart, true);

            Console.WriteLine($"Bench begin ready threads:{_ready}");

            while (Volatile.Read(ref _complate) < _jobs)
            {
            }

            Console.WriteLine($"Bench done");
        }

        public void Stop()
        {
            Volatile.Write(ref _complate, int.MaxValue - _jobs);
            Volatile.Write(ref _ready, int.MaxValue - _jobs);
        }

        private void Job(Action action)
        {
            Interlocked.Add(ref _ready, 1);

            Console.WriteLine($"Thread ready:{Thread.CurrentThread.ManagedThreadId}");

            while (!Volatile.Read(ref _canStart))
            {
            }

            Console.WriteLine($"Thread begin:{Thread.CurrentThread.ManagedThreadId}");

            action();

            Console.WriteLine($"Thread done:{Thread.CurrentThread.ManagedThreadId}");

            Interlocked.Add(ref _complate, 1);
        }
    }
}
