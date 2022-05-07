# DataflowChannel
A lock-free library for high-performance implementations of the producer(s)\consumer(s) data structures

## OPOC - One Producer One Consumer
This channel to use only two threads at the same time.
At the core a cycle buffer that implements a producer-consumer pattern. 
Wait-Free implementation without any CAS operations.

Performance about 180-190 millions operations per second

### Usage example
```c#
        static void ChannelOPOC_Sample()
        {
            var threads = 2;
            var ready = 0;
            var count = 100_000_000;
            var sw = Stopwatch.StartNew();
            
            var channel = new ChannelOPOC<int>();

            // Run Producer
            Task.Factory.StartNew(() =>
            {
                Interlocked.Add(ref ready, 1);

                while (Volatile.Read(ref ready) < threads)
                {
                }

                var start = sw.ElapsedMilliseconds;

                for (int i = 0; i < count; i++)
                {
                    channel.Write(i);
                }

                channel.Write(-1);

                Console.WriteLine($"Producer time:{sw.ElapsedMilliseconds - start}, thread:{Thread.CurrentThread.ManagedThreadId}");

            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            // Run Consumer
            Task.Factory.StartNew(() =>
            {
                Interlocked.Add(ref ready, 1);

                while (Volatile.Read(ref ready) < threads)
                {
                }

                var sum = 0;
                var empty = 0;
                var start = sw.ElapsedMilliseconds;

                while (true)
                {
                    if (channel.TryRead(out var num))
                    {
                        if (num == -1)
                        {
                            break;
                        }
                        
                        sum++;
                    }
                    else
                    {
                        empty++;
                    }
                }

                Console.WriteLine($"Consumer time:{sw.ElapsedMilliseconds - start}, thread:{Thread.CurrentThread.ManagedThreadId}, sum:{sum}, empty reads:{empty}");

            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
```

![image](https://user-images.githubusercontent.com/41398/166560940-29b32816-da3c-429d-ab1a-c4f9963acb46.png)

## OPMC - One Producer Many Consumers
## MPOC - Many Producers One Consumer
## MPMC - Many Producers Many Consumers
### Coming soon
