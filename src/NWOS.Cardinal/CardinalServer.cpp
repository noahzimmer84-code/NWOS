#include <windows.h>
#include <iostream>
#include <thread>
#include <string>

// Der Name unserer sicheren Named Pipe
#define PIPE_NAME L"\\\\.\\pipe\\NWOS_Cardinal_Pipe"

// Verarbeitet eine einzelne Client-Verbindung (isoliert im eigenen Thread)
void HandleClient(HANDLE hPipe) {
    char buffer[2048];
    DWORD bytesRead = 0;

    // Daten von der Taskleiste (Desktop-Shell) lesen
    BOOL success = ReadFile(hPipe, buffer, sizeof(buffer) - 1, &bytesRead, NULL);
    if (success && bytesRead > 0) {
        buffer[bytesRead] = '\0';
        std::string request(buffer);

        // Hier greift später dein Dispatcher für Alya, Marc & Alice
        std::cout << "[Cardinal Kernel] Befehl empfangen: " << request << std::endl;

        // Beispiel-Antwort zurück an das Overlay senden
        std::string response = "{\"status\":\"ok\",\"source\":\"CardinalKernel\",\"data\":\"Befehl erfolgreich verarbeitet\"}";
        DWORD bytesWritten = 0;
        WriteFile(hPipe, response.c_str(), (DWORD)response.length(), &bytesWritten, NULL);
    }

    // Verbindung sauber abschließen
    FlushFileBuffers(hPipe);
    DisconnectNamedPipe(hPipe);
    CloseHandle(hPipe);
}

// Die Haupt-Lauscheschleife des Servers
void StartCardinalServer() {
    std::wcout << L"[Cardinal] Native IPC-Server läuft und lauscht..." << std::endl;

    while (true) {
        // Erstellen der Named Pipe für eingehende Verbindungen
        HANDLE hPipe = CreateNamedPipe(
                PIPE_NAME,
                PIPE_ACCESS_DUPLEX,
                PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT,
                PIPE_UNLIMITED_INSTANCES,
                2048, 2048, 0, NULL
        );

        if (hPipe == INVALID_HANDLE_VALUE) {
            std::cerr << "[Cardinal Error] Named Pipe konnte nicht erstellt werden. Fehler: " << GetLastError() << std::endl;
            std::this_thread::sleep_for(std::chrono::milliseconds(1000));
            continue;
        }

        // Auf eintreffende Verbindung von der Desktop-Shell warten
        if (ConnectNamedPipe(hPipe, NULL) ? TRUE : (GetLastError() == ERROR_PIPE_CONNECTED)) {
            // Jede Verbindung wird in einem separaten Thread abgespalten.
            // Stirbt ein Client, läuft der Server absolut stabil weiter.
            std::thread(HandleClient, hPipe).detach();
        } else {
            CloseHandle(hPipe);
        }
    }
}

int main() {
    // Startet den Cardinal Kernel Server als eigenständigen Hintergrundprozess
    StartCardinalServer();
    return 0;
}