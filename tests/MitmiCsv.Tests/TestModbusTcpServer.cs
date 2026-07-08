using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace MitmiCsv.Tests;

internal sealed class TestModbusTcpServer : IAsyncDisposable
{
    private readonly CancellationTokenSource _cancellation = new();
    private readonly ConcurrentBag<Task> _clientTasks = [];
    private readonly Func<TestModbusTcpRequest, TestModbusTcpResponse> _handler;
    private readonly TcpListener _listener;
    private readonly ConcurrentQueue<TestModbusTcpRequest> _requests = [];
    private readonly Task _acceptLoop;

    private TestModbusTcpServer(Func<TestModbusTcpRequest, TestModbusTcpResponse> handler)
    {
        _handler = handler;
        _listener = new TcpListener(IPAddress.Loopback, port: 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    public int Port { get; }

    public IReadOnlyCollection<TestModbusTcpRequest> Requests => _requests.ToArray();

    public static TestModbusTcpServer Start(Func<TestModbusTcpRequest, TestModbusTcpResponse> handler)
    {
        return new TestModbusTcpServer(handler);
    }

    public async ValueTask DisposeAsync()
    {
        await _cancellation.CancelAsync().ConfigureAwait(false);
        _listener.Stop();

        try
        {
            await _acceptLoop.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (SocketException)
        {
        }

        foreach (Task clientTask in _clientTasks)
        {
            try
            {
                await clientTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
            }
            catch (SocketException)
            {
            }
        }

        _cancellation.Dispose();
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cancellation.IsCancellationRequested)
        {
            TcpClient client = await _listener.AcceptTcpClientAsync(_cancellation.Token).ConfigureAwait(false);
            _clientTasks.Add(Task.Run(() => HandleClientAsync(client, _cancellation.Token)));
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        await using NetworkStream stream = client.GetStream();
        using (client)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                byte[]? mbap = await ReadExactOrNullAsync(stream, 7, cancellationToken).ConfigureAwait(false);
                if (mbap is null)
                {
                    return;
                }

                ushort transactionId = ReadUInt16(mbap, 0);
                ushort length = ReadUInt16(mbap, 4);
                byte unitId = mbap[6];
                int pduLength = length - 1;
                if (pduLength < 5)
                {
                    return;
                }

                byte[]? pdu = await ReadExactOrNullAsync(stream, pduLength, cancellationToken).ConfigureAwait(false);
                if (pdu is null)
                {
                    return;
                }

                TestModbusTcpRequest request = new(
                    transactionId,
                    unitId,
                    pdu[0],
                    ReadUInt16(pdu, 1),
                    ReadUInt16(pdu, 3));
                _requests.Enqueue(request);

                TestModbusTcpResponse response = _handler(request);
                if (response.Pdu is null)
                {
                    if (response.KeepConnectionOpen)
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                    }

                    return;
                }

                byte[] responseBytes = BuildResponse(transactionId, unitId, response.Pdu);
                await stream.WriteAsync(responseBytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);

                if (!response.KeepConnectionOpen)
                {
                    return;
                }
            }
        }
    }

    private static byte[] BuildResponse(ushort transactionId, byte unitId, byte[] pdu)
    {
        ushort length = checked((ushort)(pdu.Length + 1));
        byte[] response = new byte[7 + pdu.Length];
        WriteUInt16(response, 0, transactionId);
        WriteUInt16(response, 2, 0);
        WriteUInt16(response, 4, length);
        response[6] = unitId;
        pdu.CopyTo(response, 7);
        return response;
    }

    private static async Task<byte[]?> ReadExactOrNullAsync(
        NetworkStream stream,
        int length,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[length];
        int offset = 0;

        while (offset < length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return offset == 0 ? null : buffer[..offset];
            }

            offset += read;
        }

        return buffer;
    }

    private static ushort ReadUInt16(byte[] buffer, int offset)
    {
        return (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
    }

    private static void WriteUInt16(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value >> 8);
        buffer[offset + 1] = (byte)value;
    }
}
