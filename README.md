# DataflowChannel
A lock-free library for high-performance implementations of the producer(s)\consumer(s) data structures

## OPOC - One Producer One Consumer
This channel to use only two threads at the same time.
At the core a cycle buffer that implements a producer-consumer pattern. 
Wait-Free implementation without any CAS operations.

Performance:

|    Method |     Mean |     Error |    StdDev |      Min |      Max |          Op/s |
|---------- |---------:|----------:|----------:|---------:|---------:|--------------:|
|     Write | 5.821 ns | 0.1559 ns | 0.1031 ns | 5.683 ns | 5.953 ns | 171,780,592.1 |
|      Read | 4.893 ns | 0.0888 ns | 0.0588 ns | 4.812 ns | 5.003 ns | 204,367,948.0 |
| ReadWrite | 3.847 ns | 1.6445 ns | 0.9786 ns | 2.366 ns | 5.435 ns | 259,972,854.8 |

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
