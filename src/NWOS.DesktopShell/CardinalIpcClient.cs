using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;

namespace NWOS.DesktopShell
{
    public class CardinalIpcClient
    {
        private const string PipeName = "NWOS_Cardinal_Pipe";
        private const string ServerName = ".";

        /// <summary>
        /// Sendet eine asynchrone Anfrage an den Cardinal-Kernel und wartet auf die Antwort.
        /// </summary>
        public async Task<string> SendRequestAsync(string targetModule, string action, string payload)
    {
        try
        {
            using var clientStream = new NamedPipeClientStream(ServerName, PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await clientStream.ConnectAsync(1500);

            string requestMessage = $"{{\"target\":\"{targetModule}\",\"action\":\"{action}\",\"payload\":\"{payload}\"}}";
            byte[] outBuffer = Encoding.UTF8.GetBytes(requestMessage);

            await clientStream.WriteAsync(outBuffer, 0, outBuffer.Length);
            await clientStream.FlushAsync();

            using var reader = new StreamReader(clientStream, Encoding.UTF8);
            string response = await reader.ReadToEndAsync();

            return response;
        }
        catch (Exception ex)
        {
            return $"{{\"status\":\"error\",\"message\":\"IPC-Verbindung fehlgeschlagen: {ex.Message}\"}}";
        }
    }

        // --- Controller-Wrapper-Methoden ---

        public async Task<string> GetAlyaTasksAsync()
    {
        return await SendRequestAsync("nwos.alya", "alya.listTasks", "{}");
    }

        public async Task<string> RunAlyaTaskAsync(string taskPayload)
    {
        return await SendRequestAsync("nwos.alya", "alya.runTask", taskPayload);
    }

        public async Task<string> ListWidgetsAsync()
    {
        return await SendRequestAsync("nwos.widgets", "widget.list", "{}");
    }
    }
}