package de.nwos.ipc;

import org.junit.jupiter.api.Test;

import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.nio.charset.StandardCharsets;

import static org.junit.jupiter.api.Assertions.assertArrayEquals;
import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;

class FrameCodecTest {

    @Test
    void roundTripPreservesPayload() throws IOException {
        var payload = "{\"nachricht\":\"hallo cardinal\"}".getBytes(StandardCharsets.UTF_8);
        var out = new ByteArrayOutputStream();
        FrameCodec.writeFrame(out, payload);

        var decoded = FrameCodec.readFrame(new ByteArrayInputStream(out.toByteArray()));
        assertArrayEquals(payload, decoded);
        assertEquals("{\"nachricht\":\"hallo cardinal\"}", new String(decoded, StandardCharsets.UTF_8));
    }

    @Test
    void multipleFramesInOneStreamStaySeparate() throws IOException {
        var out = new ByteArrayOutputStream();
        FrameCodec.writeFrame(out, "eins".getBytes(StandardCharsets.UTF_8));
        FrameCodec.writeFrame(out, "zwei".getBytes(StandardCharsets.UTF_8));

        var in = new ByteArrayInputStream(out.toByteArray());
        assertEquals("eins", new String(FrameCodec.readFrame(in), StandardCharsets.UTF_8));
        assertEquals("zwei", new String(FrameCodec.readFrame(in), StandardCharsets.UTF_8));
    }

    @Test
    void rejectsFramesAboveLimit() {
        var out = new ByteArrayOutputStream();
        out.write(0x7F); // Länge mit gesetztem Vorzeichenbit -> negativ/über Limit
        out.write(0x00);
        out.write(0x00);
        out.write(0x01);

        assertThrows(IOException.class, () -> FrameCodec.readFrame(new ByteArrayInputStream(out.toByteArray())));
    }
}
