namespace MitmiCsv;

public sealed class ModbusReadException : Exception
{
    public ModbusReadException(int statusCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
