namespace MitmiCsv.Tests;

public sealed class CsvFormatterTests
{
    [Fact]
    public void Format_WritesRegisterRowsWithHeaderAndTimestamp()
    {
        ReadRequest request = new(
            "192.168.1.50",
            502,
            1,
            3,
            0,
            2,
            3000,
            IncludeHeader: true,
            IncludeTimestamp: true);
        DateTimeOffset timestamp = new(2026, 7, 8, 8, 30, 0, TimeSpan.FromHours(3));
        ModbusReadResult result = ModbusReadResult.ForRegisters(request, timestamp, [250, 65436]);

        string csv = CsvFormatter.Format(result);

        Assert.Equal(
            "timestamp,host,port,unit,function,address,offset,raw_hex,uint16,int16\r\n" +
            "2026-07-08T08:30:00+03:00,192.168.1.50,502,1,3,0,0,00FA,250,250\r\n" +
            "2026-07-08T08:30:00+03:00,192.168.1.50,502,1,3,0,1,FF9C,65436,-100\r\n",
            csv);
    }

    [Fact]
    public void Format_WritesBitRowsWithoutHeaderOrTimestamp()
    {
        ReadRequest request = new(
            "device.local",
            1502,
            255,
            1,
            12,
            2,
            3000,
            IncludeHeader: false,
            IncludeTimestamp: false);
        ModbusReadResult result = ModbusReadResult.ForBits(request, DateTimeOffset.UnixEpoch, [true, false]);

        string csv = CsvFormatter.Format(result);

        Assert.Equal(
            "device.local,1502,255,1,12,0,true\r\n" +
            "device.local,1502,255,1,12,1,false\r\n",
            csv);
    }

    [Fact]
    public void Format_EscapesCsvFields()
    {
        ReadRequest request = new(
            "device,\"a\"",
            502,
            1,
            4,
            0,
            1,
            3000,
            IncludeHeader: true,
            IncludeTimestamp: false);
        ModbusReadResult result = ModbusReadResult.ForRegisters(request, DateTimeOffset.UnixEpoch, [1]);

        string csv = CsvFormatter.Format(result);

        Assert.Contains("\"device,\"\"a\"\"\"", csv, StringComparison.Ordinal);
    }
}
