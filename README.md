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
|   Read |       1 | 5.445 ns | 0.4569 ns | 0.0707 ns | 5.371 ns | 5.527 ns | 183,639,080.2 | 183,639,080.20 |
|   Read |       1 | 5.511 ns | 0.2708 ns | 0.0703 ns | 5.437 ns | 5.591 ns | 181,462,956.5 | 181,462,956.50 |
|   Read |       1 | 5.587 ns | 0.4706 ns | 0.0728 ns | 5.501 ns | 5.678 ns | 178,995,592.2 | 178,995,592.20 |
|   Read |       1 | 5.545 ns | 0.6594 ns | 0.1713 ns | 5.389 ns | 5.811 ns | 180,332,582.3 | 180,332,582.30 |

### Bench only writers:
| Method | Threads |     Mean |    Error |   StdDev |      Min |      Max |         Op/s |     Op/s total |
|------- |-------- |---------:|---------:|---------:|---------:|---------:|-------------:|--------------- |
|  Write |       1 | 11.25 ns | 2.003 ns | 0.520 ns | 10.45 ns | 11.78 ns | 88,884,451.8 |  88,884,451.80 |
|  Write |       2 | 11.88 ns | 1.340 ns | 0.348 ns | 11.49 ns | 12.21 ns | 84,161,901.6 | 168,323,803.20 |
|  Write |       4 | 12.37 ns | 1.732 ns | 0.268 ns | 11.97 ns | 12.54 ns | 80,847,692.9 | 323,390,771.60 |
|  Write |       8 | 15.55 ns | 3.478 ns | 0.538 ns | 14.77 ns | 15.97 ns | 64,320,056.6 | 514,560,452.80 |

### Bench writers with one reader thread. 
#### The consumer thread does operations all the time until the producers write everything down.
|          Method | Threads |     Mean |    Error |   StdDev |      Min |      Max |         Op/s |     Op/s total |
|---------------- |-------- |---------:|---------:|---------:|---------:|---------:|-------------:|--------------- |
| WriteWithReader |       1 | 12.83 ns | 7.334 ns | 1.905 ns | 10.46 ns | 15.31 ns | 77,919,316.1 |  77,919,316.10 |
| WriteWithReader |       2 | 11.76 ns | 2.106 ns | 0.326 ns | 11.29 ns | 12.01 ns | 85,017,400.5 | 170,034,801.00 |
| WriteWithReader |       4 | 13.70 ns | 4.107 ns | 1.067 ns | 12.57 ns | 15.23 ns | 73,015,285.4 | 292,061,141.60 |
| WriteWithReader |       8 | 16.52 ns | 2.497 ns | 0.648 ns | 15.93 ns | 17.36 ns | 60,516,862.4 | 484,134,899.20 |




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

![image](https://user-images.githubusercontent.com/41398/166560940-29b32816-da3c-429d-ab1a-c4f9963acb46.png)
