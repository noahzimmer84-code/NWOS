package de.nwos.contracts;

import com.fasterxml.jackson.databind.DeserializationFeature;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.fasterxml.jackson.databind.SerializationFeature;
import com.fasterxml.jackson.databind.json.JsonMapper;
import com.fasterxml.jackson.datatype.jsr310.JavaTimeModule;

/**
 * Gemeinsame JSON-Einstellungen für die Modul-Kommunikation (Port von
 * NWOS.Contracts.NwosJson). Ein einziger, unveränderlicher Mapper für alle
 * IPC-Teilnehmer, damit Absender und Empfänger dieselbe Feld- und
 * Datums-Darstellung verwenden.
 */
public final class NwosJson {

    private static final ObjectMapper MAPPER = JsonMapper.builder()
            .addModule(new JavaTimeModule())
            .disable(DeserializationFeature.FAIL_ON_UNKNOWN_PROPERTIES)
            .disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS)
            .disable(SerializationFeature.FAIL_ON_EMPTY_BEANS)
            .build();

    private NwosJson() {
    }

    public static ObjectMapper mapper() {
        return MAPPER;
    }
}
