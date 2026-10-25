package de.nwos.ipc;

import java.io.EOFException;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;

/**
 * Rahmenformat für die IPC-Kommunikation: 4-Byte-Längenpräfix (big-endian,
 * da beide Enden Java sind) gefolgt vom UTF-8-JSON-Payload.
 *
 * <p>PT1-Fix: In der C#-Version schrieb der Client gerahmte Nachrichten,
 * während der Server ungerahmten JSON-Stream erwartete (Framing-Mismatch,
 * 4/5 fehlschlagende IPC-Tests). In der Java-Version verwenden beide Seiten
 * ausschließlich dieses Codec - es gibt keinen ungerahmten Pffad mehr.</p>
 */
public final class FrameCodec {

    /** 16 MB Sicherheitsgrenze gegen fehlerhafte/böswillige Längenangaben. */
    public static final int MAX_FRAME_BYTES = 16 * 1024 * 1024;

    private FrameCodec() {
    }

    public static void writeFrame(OutputStream out, byte[] payload) throws IOException {
        out.write(new byte[]{
                (byte) (payload.length >>> 24),
                (byte) (payload.length >>> 16),
                (byte) (payload.length >>> 8),
                (byte) payload.length});
        out.write(payload);
        out.flush();
    }

    public static byte[] readFrame(InputStream in) throws IOException {
        byte[] lengthBuffer = readExact(in, 4);
        int length = ((lengthBuffer[0] & 0xFF) << 24)
                | ((lengthBuffer[1] & 0xFF) << 16)
                | ((lengthBuffer[2] & 0xFF) << 8)
                | (lengthBuffer[3] & 0xFF);

        if (length < 0 || length > MAX_FRAME_BYTES) {
            throw new IOException("Ungültige IPC-Nachrichtenlänge: " + length + " Bytes.");
        }

        return readExact(in, length);
    }

    private static byte[] readExact(InputStream in, int count) throws IOException {
        byte[] buffer = new byte[count];
        int totalRead = 0;
        while (totalRead < count) {
            int read = in.read(buffer, totalRead, count - totalRead);
            if (read < 0) {
                throw new EOFException("Verbindung wurde geschlossen, bevor die vollständige Nachricht gelesen werden konnte.");
            }
            totalRead += read;
        }
        return buffer;
    }
}
