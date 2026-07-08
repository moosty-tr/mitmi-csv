namespace MitmiCsv.Tests;

internal sealed record TestModbusTcpResponse(byte[]? Pdu, bool KeepConnectionOpen)
{
    public static TestModbusTcpResponse Bits(TestModbusTcpRequest request, params bool[] values)
    {
        byte[] pdu = new byte[2 + ((values.Length + 7) / 8)];
        pdu[0] = request.FunctionCode;
        pdu[1] = (byte)(pdu.Length - 2);

        for (int index = 0; index < values.Length; index++)
        {
            if (values[index])
            {
                pdu[2 + (index / 8)] |= (byte)(1 << (index % 8));
            }
        }

        return new TestModbusTcpResponse(pdu, KeepConnectionOpen: false);
    }

    public static TestModbusTcpResponse Registers(TestModbusTcpRequest request, params ushort[] values)
    {
        byte[] pdu = new byte[2 + (values.Length * 2)];
        pdu[0] = request.FunctionCode;
        pdu[1] = (byte)(values.Length * 2);

        for (int index = 0; index < values.Length; index++)
        {
            pdu[2 + (index * 2)] = (byte)(values[index] >> 8);
            pdu[3 + (index * 2)] = (byte)values[index];
        }

        return new TestModbusTcpResponse(pdu, KeepConnectionOpen: false);
    }

    public static TestModbusTcpResponse ModbusException(TestModbusTcpRequest request, byte exceptionCode)
    {
        return new TestModbusTcpResponse([(byte)(request.FunctionCode | 0x80), exceptionCode], KeepConnectionOpen: false);
    }

    public static TestModbusTcpResponse NoResponse()
    {
        return new TestModbusTcpResponse(Pdu: null, KeepConnectionOpen: true);
    }
}
