using MessageClient;
using Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UploadWorkflow;

var usage = "Use coordinator | simulators | start <saga-guid> <user-guid> <channel-guid> <positive-bytes> | status <saga-guid>";
if (args.Length == 0) throw new ArgumentException(usage);
var directory = Environment.GetEnvironmentVariable("UPLOAD_DATA") ?? "upload-data";
var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
using var services = new ServiceCollection().AddMessageClient(configuration).BuildServiceProvider();
var bus = services.GetRequiredService<IMessageClient>();
using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
var ct = stop.Token;
try
{
    switch (args[0])
    {
        case "start" when args.Length == 5:
            var upload = new StartUpload(Guid.Parse(args[1]), Guid.Parse(args[2]), Guid.Parse(args[3]),
                long.Parse(args[4]), DateTimeOffset.UtcNow);
            if (!upload.IsValid) throw new ArgumentException(usage);
            await bus.PublishAsync(upload, ct);
            Console.WriteLine(upload.SagaId);
            break;
        case "simulators":
            var simulated = new SimulatedParticipants(directory, bus);
            using (await bus.SubscribeAsync<FileCommand>("upload-file-simulator", simulated.Handle, ct))
            using (await bus.SubscribeAsync<QuotaCommand>("upload-quota-simulator", simulated.Handle, ct))
            using (await bus.SubscribeAsync<AttachmentMessageCommand>("upload-message-simulator", simulated.Handle, ct))
            {
                Console.WriteLine("File Sharing, Quota and Message simulators ready");
                await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            }
            break;
        case "coordinator":
        case "status" when args.Length == 2:
            var coordinator = new UploadCoordinator(Path.Combine(directory, "sagas.db"), bus);
            if (args[0] == "status")
            {
                Console.WriteLine(coordinator.Status(Guid.Parse(args[1]))?.ToString() ?? "Unknown saga");
                break;
            }

            using (await bus.SubscribeAsync<StartUpload>("upload-start", coordinator.Start, ct))
            using (await bus.SubscribeAsync<UploadStepResult>("upload-result", coordinator.Receive, ct))
            {
                Console.WriteLine("Upload coordinator ready");
                while (!ct.IsCancellationRequested)
                {
                    try { await coordinator.DispatchPending(ct); }
                    catch (Exception ex) when (!ct.IsCancellationRequested)
                    {
                        Console.Error.WriteLine($"Pending uploads will retry: {ex.Message}");
                    }
                    await Task.Delay(TimeSpan.FromSeconds(2), ct);
                }
            }
            break;
        default:
            throw new ArgumentException(usage);
    }
}
catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
