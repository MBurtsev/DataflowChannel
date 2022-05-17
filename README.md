# DataflowChannel
A lock-free library for high-performance implementations of the producer(s)\consumer(s) data structures

## OPOC - One Producer One Consumer
This channel to use only two threads at the same time.
At the core a cycle buffer that implements a producer-consumer pattern. 
Wait-Free implementation without any CAS operations.

Performance:

|    Method |     Mean |      Error |    StdDev |      Min |       Max |          Op/s |
|---------- |---------:|-----------:|----------:|---------:|----------:|--------------:|
|     Write | 6.011 ns |  0.7820 ns | 0.2031 ns | 5.725 ns |  6.208 ns | 166,366,137.3 |
|      Read | 4.918 ns |  0.2605 ns | 0.0677 ns | 4.860 ns |  5.014 ns | 203,322,311.0 |
| ReadWrite | 8.398 ns | 12.4634 ns | 3.2367 ns | 5.150 ns | 13.060 ns | 119,071,782.1 |

## MPOC No Order - Multiple Producers One Consumer
Producers use spin lock for initialization thread only once. 
All write operations fully lock-free\wait-free.
Customer fully lock-free\wait-free.
No order means that read order is not equal to write order.
    
Performance:

| Threads | Write op\s | Read op\s |
| ------------- | ------------- | ------------- |
| Producer: 0, Consumer: 1 | nope |  124M|
| Producer: 1, Consumer: 0 | 69M | nope |
| Producer: 1, Consumer: 1 | 49M |  49M |
| Producer: 2, Consumer: 1 | 115M |  104M |
| Producer: 4, Consumer: 1 | 195M |  116M |
| Producer: 8, Consumer: 1 | 363M |  110M |

## MPOC - Multiple Producers One Consumer
### Coming soon
## OPMC - One Producer Multiple Consumers
### Coming soon
## MPMC - Multiple Producer Multiple Consumer
### Coming soon

## Usage example
```c#
        static void Sample()
        {
            var producers = 8;
            var consumers = 1;
            var threads = producers + consumers;
            var ready = 0;
            var count = 100_000_000;
            var sw = Stopwatch.StartNew();

            // Attention: No more than one consumer and one producer
            //var channel = new ChannelOPOC<int>();
            
            // Attention: No more than one consumer
            var channel = new ChannelMPOCnoOrder<int>();
            
            // Run producers
            for (var n = 0; n < producers; n++)
            {
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
            }

            // Run consumers
            for (var n = 0; n < consumers; n++)
            {
                Task.Factory.StartNew(() =>
                {
                    Interlocked.Add(ref ready, 1);

                    while (Volatile.Read(ref ready) < threads)
                    {
                    }

                    var sum   = 0;
                    var empty = 0;
                    var exit  = 0;
                    var start = sw.ElapsedMilliseconds;

                    while (true)
                    {
                        if (channel.TryRead(out var num))
                        {
                            if (num == -1)
                            {
                                exit++;

                                if (exit == producers)
                                {
                                    break;
                                }

                                continue;
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
        }
```

![image](https://user-images.githubusercontent.com/41398/166560940-29b32816-da3c-429d-ab1a-c4f9963acb46.png)
