using System;
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
        ~MultiThreadHelper()
        {

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
                var thread = new Thread(() => Job(action));

                thread.Start();
            }
        }

        public void WaitReady()
        {
            //Debugger.Log(0, "Err", _jobs.ToString()); ; //.WriteLine(_jobs);

            //Debugger.Log(3, "Err", $"ready:{Volatile.Read(ref _ready)}, jobs:{_jobs}");
            //Console.WriteLine($"ready:{Volatile.Read(ref _ready)}, jobs:{_jobs}");

            // Wait all threads are ready
            while (Volatile.Read(ref _ready) < _jobs)
            {
                //Debugger.Log(0, "Err", Volatile.Read(ref _ready).ToString());
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

        public void Stop()
        {
            Volatile.Write(ref _complate, int.MaxValue - _jobs);
            Volatile.Write(ref _ready, int.MaxValue - _jobs);
        }

        private void Job(Action action)
        {
            Interlocked.Add(ref _ready, 1);

            while (!Volatile.Read(ref _canStart))
            {
            }

            action();

            Interlocked.Add(ref _complate, 1);
        }
    }
}
