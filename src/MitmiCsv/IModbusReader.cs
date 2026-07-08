namespace MitmiCsv;

public interface IModbusReader
{
    Task<ModbusReadResult> ReadAsync(ReadRequest request, DateTimeOffset timestamp, CancellationToken cancellationToken);
}
