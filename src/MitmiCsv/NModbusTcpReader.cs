using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net.Sockets;
using NModbus;

namespace MitmiCsv;

public sealed class NModbusTcpReader : IModbusReader
{
    public async Task<ModbusReadResult> ReadAsync(
        ReadRequest request,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        TimeSpan timeout = TimeSpan.FromMilliseconds(request.TimeoutMilliseconds);

        using TcpClient tcpClient = new()
        {
            ReceiveTimeout = request.TimeoutMilliseconds,
            SendTimeout = request.TimeoutMilliseconds,
            NoDelay = true
        };

        try
        {
            await tcpClient.ConnectAsync(request.Host, request.Port, cancellationToken)
                .AsTask()
                .WaitAsync(timeout, cancellationToken)
                .ConfigureAwait(false);

            ModbusFactory factory = new();
            using IModbusMaster master = factory.CreateMaster(tcpClient);
            master.Transport.ReadTimeout = request.TimeoutMilliseconds;
            master.Transport.WriteTimeout = request.TimeoutMilliseconds;
            master.Transport.Retries = 0;
            master.Transport.SlaveBusyUsesRetryCount = true;

            return await ReadValuesAsync(master, request, timestamp, timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (SlaveException exception)
        {
            byte exceptionCode = checked((byte)exception.SlaveExceptionCode);
            throw new ModbusReadException(
                StatusCodes.Status502BadGateway,
                $"Modbus exception response: code 0x{exceptionCode:X2}. {exception.Message}",
                exception);
        }
        catch (Exception exception) when (IsTimeoutException(exception))
        {
            throw new ModbusReadException(
                StatusCodes.Status504GatewayTimeout,
                $"Timed out reading Modbus TCP device {request.Host}:{request.Port}.",
                exception);
        }
        catch (Exception exception) when (IsInvalidResponseException(exception))
        {
            throw new ModbusReadException(
                StatusCodes.Status502BadGateway,
                $"Invalid Modbus response from {request.Host}:{request.Port}: {exception.Message}",
                exception);
        }
        catch (Exception exception) when (IsTransportException(exception))
        {
            throw new ModbusReadException(
                StatusCodes.Status502BadGateway,
                $"Could not read Modbus TCP device {request.Host}:{request.Port}: {exception.Message}",
                exception);
        }
    }

    private static async Task<ModbusReadResult> ReadValuesAsync(
        IModbusMaster master,
        ReadRequest request,
        DateTimeOffset timestamp,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ushort address = checked((ushort)request.Address);
        ushort count = checked((ushort)request.Count);

        return request.FunctionCode switch
        {
            1 => ModbusReadResult.ForBits(
                request,
                timestamp,
                await master.ReadCoilsAsync(request.UnitId, address, numberOfPoints: count)
                    .WaitAsync(timeout, cancellationToken)
                    .ConfigureAwait(false)),
            2 => ModbusReadResult.ForBits(
                request,
                timestamp,
                await master.ReadInputsAsync(request.UnitId, address, numberOfPoints: count)
                    .WaitAsync(timeout, cancellationToken)
                    .ConfigureAwait(false)),
            3 => ModbusReadResult.ForRegisters(
                request,
                timestamp,
                await master.ReadHoldingRegistersAsync(request.UnitId, address, numberOfPoints: count)
                    .WaitAsync(timeout, cancellationToken)
                    .ConfigureAwait(false)),
            4 => ModbusReadResult.ForRegisters(
                request,
                timestamp,
                await master.ReadInputRegistersAsync(request.UnitId, address, numberOfPoints: count)
                    .WaitAsync(timeout, cancellationToken)
                    .ConfigureAwait(false)),
            _ => throw new InvalidOperationException($"Unsupported Modbus function code {request.FunctionCode}.")
        };
    }

    private static bool IsTimeoutException(Exception exception)
    {
        return exception is TimeoutException
            || exception.InnerException is TimeoutException
            || exception is IOException { InnerException: SocketException socketException } && socketException.SocketErrorCode == SocketError.TimedOut
            || exception is SocketException { SocketErrorCode: SocketError.TimedOut };
    }

    private static bool IsInvalidResponseException(Exception exception)
    {
        return exception is FormatException
            || exception is InvalidDataException
            || exception is ArgumentException;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The adapter maps socket and library failures into plain HTTP error responses.")]
    private static bool IsTransportException(Exception exception)
    {
        return exception is IOException
            || exception is SocketException
            || exception is ObjectDisposedException
            || exception is InvalidOperationException;
    }
}
