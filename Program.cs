﻿using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

var taskCount = 8000;

// This is just a quick 200 OK with minimal content.
var target = "https://fb.sasaares.main.ml.mdn.skype.net:13879/";

// For some loosely defined "last".
var lastCompleted = Stopwatch.GetTimestamp();
var lastDuration = TimeSpan.Zero;

var succeeded = 0;
var failed = 0;

// Tasks that have seen at least 1 success.
var startedTasks = 0;

_ = Task.Run(async delegate
{
    while (true)
    {
        var elapsedSincelastCompleted = Stopwatch.GetElapsedTime(lastCompleted);

        Console.WriteLine($"Successfully completed {succeeded} requests, last one took {lastDuration.TotalMilliseconds:F1} ms and finished {elapsedSincelastCompleted.TotalMilliseconds:F1} ms ago. {failed} failures seen. {startedTasks}/{taskCount} tasks successfully completed at least one request.");

        await Task.Delay(TimeSpan.FromSeconds(1));
    }
});

async Task ExecuteOneRequestAsync()
{
    using var handler = new SocketsHttpHandler
    {
        ConnectTimeout = TimeSpan.FromSeconds(2),
        ConnectCallback = async (context, cancel) =>
        {
            var ipAddresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, cancel);

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);

            await socket.ConnectAsync(ipAddresses.First(), context.DnsEndPoint.Port, cancel);

            return new NetworkStream(socket, ownsSocket: true);
        }
    };
    using var client = new HttpClient(handler)
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    bool started = false;

    while (true)
    {
        try
        {
            var start = Stopwatch.GetTimestamp();

            await client.GetAsync(target);

            lastDuration = Stopwatch.GetElapsedTime(start);
            lastCompleted = Stopwatch.GetTimestamp();
            Interlocked.Increment(ref succeeded);

            if (!started)
            {
                started = true;
                Interlocked.Increment(ref startedTasks);
            }

            // If we completed at least one request, we know this HttpClient works so no point doing anything more.
            return;
        }
        catch
        {
            Interlocked.Increment(ref failed);
        }

        // Try again soon.
        await Task.Delay(millisecondsDelay: 100);
    }
}

var tasks = new List<Task>(taskCount);

for (var i = 0; i < taskCount; i++)
{
    tasks.Add(Task.Run(ExecuteOneRequestAsync));

    // Wait between adding new tasks to avoid intentionally generating a huge burst.
    await Task.Delay(millisecondsDelay: 5);
}

await Task.WhenAll(tasks);