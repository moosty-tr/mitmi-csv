using Microsoft.AspNetCore.Http;

namespace MitmiCsv.Tests;

public sealed class NModbusTcpReaderTests
{
    [Fact]
    public async Task ReadAsync_ReadsHoldingRegisters()
    {
        await using TestModbusTcpServer server = TestModbusTcpServer.Start(
            request => TestModbusTcpResponse.Registers(request, 250, 65436));
        NModbusTcpReader reader = new();
        ReadRequest request = Request(server.Port, functionCode: 3, count: 2);
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        ModbusReadResult result = await reader.ReadAsync(request, timestamp, CancellationToken.None);

        Assert.Null(result.Bits);
        Assert.Equal([250, 65436], result.Registers);
        TestModbusTcpRequest received = Assert.Single(server.Requests);
        Assert.Equal(1, received.UnitId);
        Assert.Equal(3, received.FunctionCode);
        Assert.Equal(0, received.Address);
        Assert.Equal(2, received.Quantity);
    }

    [Fact]
    public async Task ReadAsync_ReadsCoils()
    {
        await using TestModbusTcpServer server = TestModbusTcpServer.Start(
            request => TestModbusTcpResponse.Bits(request, true, false, true));
        NModbusTcpReader reader = new();
        ReadRequest request = Request(server.Port, functionCode: 1, count: 3);

        ModbusReadResult result = await reader.ReadAsync(request, DateTimeOffset.UtcNow, CancellationToken.None);

        Assert.Null(result.Registers);
        Assert.Equal([true, false, true], result.Bits);
    }

    [Fact]
    public async Task ReadAsync_MapsModbusException()
    {
        await using TestModbusTcpServer server = TestModbusTcpServer.Start(
            request => TestModbusTcpResponse.ModbusException(request, 0x02));
        NModbusTcpReader reader = new();
        ReadRequest request = Request(server.Port, functionCode: 3, count: 1);

        ModbusReadException exception = await Assert.ThrowsAsync<ModbusReadException>(
            () => reader.ReadAsync(request, DateTimeOffset.UtcNow, CancellationToken.None));

        Assert.Equal(StatusCodes.Status502BadGateway, exception.StatusCode);
        Assert.Contains("0x02", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReadAsync_MapsTimeout()
    {
        await using TestModbusTcpServer server = TestModbusTcpServer.Start(
            _ => TestModbusTcpResponse.NoResponse());
        NModbusTcpReader reader = new();
        ReadRequest request = new(
            "127.0.0.1",
            server.Port,
            1,
            3,
            0,
            1,
            TimeoutMilliseconds: 50,
            IncludeHeader: true,
            IncludeTimestamp: true);

        ModbusReadException exception = await Assert.ThrowsAsync<ModbusReadException>(
            () => reader.ReadAsync(request, DateTimeOffset.UtcNow, CancellationToken.None));

        Assert.Equal(StatusCodes.Status504GatewayTimeout, exception.StatusCode);
    }

    private static ReadRequest Request(int port, int functionCode, int count)
    {
        return new ReadRequest(
            "127.0.0.1",
            port,
            1,
            functionCode,
            0,
            count,
            TimeoutMilliseconds: 1000,
            IncludeHeader: true,
            IncludeTimestamp: true);
    }
}
