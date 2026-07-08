namespace MitmiCsv;

public sealed record ModbusReadResult(
    ReadRequest Request,
    DateTimeOffset Timestamp,
    IReadOnlyList<bool>? Bits,
    IReadOnlyList<ushort>? Registers)
{
    public bool IsBitResult => Request.FunctionCode is 1 or 2;

    public static ModbusReadResult ForBits(ReadRequest request, DateTimeOffset timestamp, IReadOnlyList<bool> bits)
    {
        return new ModbusReadResult(request, timestamp, bits, Registers: null);
    }

    public static ModbusReadResult ForRegisters(ReadRequest request, DateTimeOffset timestamp, IReadOnlyList<ushort> registers)
    {
        return new ModbusReadResult(request, timestamp, Bits: null, registers);
    }
}
