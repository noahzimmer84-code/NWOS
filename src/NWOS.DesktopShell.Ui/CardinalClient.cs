using System;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading.Tasks;
using NWOS.Contracts;

namespace NWOS.DesktopShell.Ui
{
    public sealed class CardinalClient
    {
        private const string PipeName = "NWOS.Cardinal.Pipe";
        public string? LastError { get; private set; }

        public async Task<JsonDocument?> CallAsync(string targetService, string action, object payload, PermissionScope scope)
        {
            try
            {
                using var clientStream = new NamedPipeClientStream(".", PipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                await clientStream.ConnectAsync(1500); // 1,5 Sekunden Timeout

                using var writer = new StreamWriter(clientStream);
                using var reader = new StreamReader(clientStream);

                var request = new
                {
                    Service = targetService,
                    Action = action,
                    Scope = scope.ToString(),
                    Payload = JsonSerializer.Serialize(payload)
                };

                string jsonRequest = JsonSerializer.Serialize(request);
                await writer.WriteLineAsync(jsonRequest);
                await writer.FlushAsync();

                string? jsonResponse = await reader.ReadLineAsync();
                LastError = null;

                if (string.IsNullOrEmpty(jsonResponse)) return null;

                return JsonDocument.Parse(jsonResponse);
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return null;
            }
        }
    }
}