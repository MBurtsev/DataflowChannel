# DataflowChannel
A lock-free library for high-performance implementations of the producers\consumers data structures

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

## MPOC no order - Multiple Producers One Consumer
Producers use a spinlock for an initialization thread only once at a time.
All read-write operations fully lock-free\wait-free.
No order means that read order is not equal to write order.
    
### Bench only write:
| Method | Threads |     Mean |    Error |   StdDev |      Min |      Max |         Op/s |     Op/s total |
|------- |-------- |---------:|---------:|---------:|---------:|---------:|-------------:|--------------- |
|  Write |       1 | 13.68 ns | 1.753 ns | 0.455 ns | 12.92 ns | 14.04 ns | 73,095,611.9 |  73,095,611.90 |
|  Write |       2 | 14.18 ns | 2.664 ns | 0.412 ns | 13.61 ns | 14.60 ns | 70,519,462.2 | 141,038,924.40 |
|  Write |       4 | 16.00 ns | 5.530 ns | 1.436 ns | 14.58 ns | 18.01 ns | 62,509,617.1 | 250,038,468.40 |
|  Write |       8 | 20.34 ns | 2.097 ns | 0.325 ns | 19.86 ns | 20.52 ns | 49,160,348.0 | 393,282,784.00 |

### Bench witers with one reader thread. Reader makes operations = count * writer_threads.
|          Method | Threads |     Mean |     Error |   StdDev |      Min |      Max |         Op/s |     Op/s total |
|---------------- |-------- |---------:|----------:|---------:|---------:|---------:|-------------:|--------------- |
| WriteWithReader |       1 | 45.46 ns | 27.188 ns | 4.207 ns | 39.19 ns | 48.19 ns | 21,997,555.4 |  21,997,555.40 |
| WriteWithReader |       2 | 13.79 ns |  2.769 ns | 0.429 ns | 13.22 ns | 14.20 ns | 72,502,675.3 | 145,005,350.60 |
| WriteWithReader |       4 | 28.24 ns |  8.616 ns | 2.238 ns | 26.20 ns | 31.79 ns | 35,414,159.8 | 141,656,639.20 |
| WriteWithReader |       8 | 57.82 ns |  8.543 ns | 1.322 ns | 56.99 ns | 59.80 ns | 17,294,063.4 | 138,352,507.20 |

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
