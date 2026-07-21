# mitmi-csv

`mitmi-csv` is a small command-line hosted HTTP service that reads live values
from Modbus TCP devices and returns plain CSV text from HTTP GET requests.

It is intended for tools that already know how to fetch CSV from a URL, such as:

- Microsoft Excel Power Query
- Microsoft Power BI
- browser or command-line HTTP clients
- small scripts

The MVP is intentionally narrow: no database, no historical storage, no JSON
output, no UI, no background polling, and no write operations.

## Status

The current v0.1 service is implemented, simulator-tested, and packaged as a
local Windows x64 release archive. Real device validation is still required
before claiming field readiness for a specific device or site.

## Run

```powershell
dotnet run --project src/MitmiCsv
```

By default the service listens on:

```text
http://localhost:8080
```

You can override ASP.NET Core URLs in the normal way:

```powershell
dotnet run --project src/MitmiCsv --urls http://localhost:9090
```

## Read Endpoint

```text
GET /read
```

Example:

```text
http://localhost:8080/read?host=192.168.1.50&port=502&unit=1&fc=3&address=0&count=10
```

Required parameters:

| Parameter | Description |
| --- | --- |
| `host` | Modbus TCP device IP address or host name. Do not include `http://`. |
| `unit` | Modbus TCP unit identifier, `0..255`. |
| `fc` | Function code: `1`, `2`, `3`, or `4`. |
| `address` | Zero-based Modbus PDU start address, `0..65535`. |
| `count` | Number of coils, inputs, or registers to read. |

Optional parameters:

| Parameter | Default | Description |
| --- | --- | --- |
| `port` | `502` | Modbus TCP port. |
| `timeoutMs` | `3000` | Per-request connect/read timeout in milliseconds, `1..600000`. |
| `header` | `true` | Include a CSV header row. |
| `timestamp` | `true` | Include a timestamp column. |

Read count limits:

| Function | Name | Maximum count |
| --- | --- | --- |
| `1` | Read Coils | `2000` bits |
| `2` | Read Discrete Inputs | `2000` bits |
| `3` | Read Holding Registers | `125` registers |
| `4` | Read Input Registers | `125` registers |

## Addressing

Addresses are zero-based Modbus PDU addresses.

```text
address=0
```

means the first coil, discrete input, holding register, or input register.

The MVP does not convert `40001` or `30001` style reference notation. If you
send `address=40001`, the service treats it as zero-based address `40001`.

## CSV Output

Successful responses use:

```text
Content-Type: text/csv; charset=utf-8
```

Register reads, function codes `3` and `4`:

```csv
timestamp,host,port,unit,function,address,offset,raw_hex,uint16,int16
2026-07-08T08:30:00+03:00,192.168.1.50,502,1,3,0,0,00FA,250,250
2026-07-08T08:30:00+03:00,192.168.1.50,502,1,3,0,1,FF9C,65436,-100
```

Bit reads, function codes `1` and `2`:

```csv
timestamp,host,port,unit,function,address,offset,value
2026-07-08T08:30:00+03:00,192.168.1.50,502,1,1,0,0,true
2026-07-08T08:30:00+03:00,192.168.1.50,502,1,1,0,1,false
```

CSV rows use CRLF line endings.

## Errors

Validation, timeout, connection, invalid-response, and Modbus exception errors
return plain text, not JSON.

Example:

```text
Invalid parameter 'fc'. Supported values are 1, 2, 3, 4.
```

Common status codes:

| Status | Meaning |
| --- | --- |
| `400` | Invalid URL parameters. |
| `502` | Device connection, invalid response, or Modbus exception response. |
| `504` | Device connect/read timeout. |

## Security Note

The default listener is `localhost` only. Keep that default unless you have a
deliberate network boundary, because the `host` query parameter tells the
service what TCP endpoint to connect to.

## Build And Test

```powershell
dotnet build MitmiCsv.slnx
dotnet test MitmiCsv.slnx
.\scripts\Invoke-ReleaseSmokeTest.ps1
```

The test suite includes a small local Modbus TCP simulator for the current read
path. Real device validation is still required before claiming field readiness.

Build a local Windows x64 release archive:

```powershell
.\scripts\Publish-Release.ps1 -Version 0.1.0
```

The release script creates `artifacts/release/mitmi-csv-v0.1.0-win-x64.zip`.
By default the archive is self-contained and includes `mitmi-csv.exe`, the
README, Apache-2.0 license, and third-party notices.

## Future Scope

These are intentionally not part of the MVP:

- named read profiles
- typed multi-register decoding
- scaling and offset transforms
- write actions
- historical logging
- JSON output

## License

Licensed under the [Apache License, Version 2.0](LICENSE).
