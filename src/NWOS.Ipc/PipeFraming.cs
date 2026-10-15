using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace NWOS.Ipc;

/// <summary>
/// Einfaches Rahmenformat für die Pipe-Kommunikation: 4-Byte-Länge
/// (little-endian) gefolgt von UTF-8-kodiertem JSON. Named Pipes liefern
/// einen Byte-Strom ohne eingebaute Nachrichtengrenzen (im Byte-Modus) -
/// ohne Längenpräfix könnte ein Leser nicht wissen, wo eine JSON-Nachricht
/// endet und die nächste beginnt.
/// </summary>
internal static class PipeFraming
{
    private const int MaxMessageBytes = 16 * 1024 * 1024; // 16 MB Sicherheitsgrenze gegen fehlerhafte/böswillige Längenangaben.

    public static async Task WriteJsonAsync<T>(PipeStream stream, T value, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(value);
        var bytes = Encoding.UTF8.GetBytes(json);

        var lengthPrefix = BitConverter.GetBytes(bytes.Length);
        await stream.WriteAsync(lengthPrefix, cancellationToken);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    public static async Task<T> ReadJsonAsync<T>(PipeStream stream, CancellationToken cancellationToken)
    {
        var lengthBuffer = new byte[4];
        await ReadExactAsync(stream, lengthBuffer, cancellationToken);
        var length = BitConverter.ToInt32(lengthBuffer);

        if (length < 0 || length > MaxMessageBytes)
        {
            throw new InvalidOperationException($"Ungültige IPC-Nachrichtenlänge: {length} Bytes.");
        }

        var payloadBuffer = new byte[length];
        await ReadExactAsync(stream, payloadBuffer, cancellationToken);

        var json = Encoding.UTF8.GetString(payloadBuffer);
        return JsonSerializer.Deserialize<T>(json)
            ?? throw new InvalidOperationException("IPC-Nachricht wurde zu 'null' deserialisiert.");
    }

    private static async Task ReadExactAsync(PipeStream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead), cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("Pipe wurde geschlossen, bevor die vollständige Nachricht gelesen werden konnte.");
            }

            totalRead += read;
        }
    }
}
