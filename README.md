# DataflowChannel
A lock-free library for high-performance implementations of the producers\consumers data structures

## OPOC - One Producer One Consumer
This channel to use only two threads at the same time.
At the core a cycle buffer that implements a producer-consumer pattern. 
Wait-Free implementation without any CAS operations.

#### Performance:
|    Method |     Mean |     Error |    StdDev |      Min |      Max |          Op/s |
|---------- |---------:|----------:|----------:|---------:|---------:|--------------:|
|     Write | 4.579 ns | 0.3328 ns | 0.0515 ns | 4.529 ns | 4.651 ns | 218,410,951.1 |
|      Read | 4.943 ns | 0.1997 ns | 0.0519 ns | 4.873 ns | 4.991 ns | 202,290,061.0 |
| ReadWrite | 2.514 ns | 0.1986 ns | 0.0307 ns | 2.470 ns | 2.537 ns | 397,820,105.0 |

## MPOC no order - Multiple Producers One Consumer
Producers use a spinlock for an initialization thread only once at a time.
All read-write operations fully lock-free\wait-free.
No order means that read order is not equal to write order.

### Bench read
| Method | Threads |     Mean |     Error |    StdDev |      Min |      Max |          Op/s |     Op/s total |
|------- |-------- |---------:|----------:|----------:|---------:|---------:|--------------:|--------------- |
|   Read |       1 | 5.486 ns | 0.5605 ns | 0.0867 ns | 5.383 ns | 5.576 ns | 182,277,205.5 | 182,277,205.50 |
|   Read |       1 | 5.579 ns | 1.0285 ns | 0.1592 ns | 5.368 ns | 5.706 ns | 179,246,162.3 | 179,246,162.30 |
|   Read |       1 | 5.534 ns | 0.5522 ns | 0.0855 ns | 5.436 ns | 5.639 ns | 180,693,096.3 | 180,693,096.30 |
|   Read |       1 | 5.516 ns | 0.5674 ns | 0.1474 ns | 5.339 ns | 5.652 ns | 181,305,252.8 | 181,305,252.80 |

### Bench only writers:
| Method | Threads |     Mean |    Error |   StdDev |       Min |      Max |         Op/s |     Op/s total |
|------- |-------- |---------:|---------:|---------:|----------:|---------:|-------------:|--------------- |
|  Write |       1 | 10.81 ns | 3.655 ns | 0.949 ns |  9.628 ns | 11.88 ns | 92,503,720.5 |  92,503,720.50 |
|  Write |       2 | 11.94 ns | 2.431 ns | 0.631 ns | 11.234 ns | 12.56 ns | 83,753,388.7 | 167,506,777.40 |
|  Write |       4 | 12.06 ns | 0.702 ns | 0.182 ns | 11.827 ns | 12.30 ns | 82,945,042.7 | 331,780,170.80 |
|  Write |       8 | 15.11 ns | 0.432 ns | 0.112 ns | 14.985 ns | 15.23 ns | 66,184,357.4 | 529,474,859.20 |

### Bench writers with one reader thread. 
#### The consumer thread does operations all the time until the producers write everything down.
|          Method | Threads |     Mean |     Error |   StdDev |      Min |      Max |         Op/s |     Op/s total |
|---------------- |-------- |---------:|----------:|---------:|---------:|---------:|-------------:|--------------- |
| WriteWithReader |       1 | 13.51 ns | 10.501 ns | 2.727 ns | 11.31 ns | 17.91 ns | 74,023,925.3 |  74,023,925.30 |
| WriteWithReader |       2 | 12.07 ns |  6.205 ns | 0.960 ns | 10.86 ns | 13.04 ns | 82,876,339.4 | 165,752,678.80 |
| WriteWithReader |       4 | 14.51 ns |  3.133 ns | 0.814 ns | 13.85 ns | 15.80 ns | 68,905,927.4 | 275,623,709.60 |
| WriteWithReader |       8 | 17.50 ns |  5.333 ns | 1.385 ns | 16.12 ns | 19.48 ns | 57,134,765.9 | 457,078,127.20 |




## MPOC - Multiple Producers One Consumer
### Coming soon
## OPMC - One Producer Multiple Consumer
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

![image](https://user-images.githubusercontent.com/41398/172729195-bd3a10c8-0a6b-4a1d-853e-1c47bfe124f6.png)

