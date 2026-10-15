using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NWOS.Cardinal.Ipc
{
    public sealed class CardinalIpcServer
    {
        private const string PipeName = "NWOS.Cardinal.Pipe";
        private CancellationTokenSource? _cts;
        private readonly List<string> _searchIndex = new();

        private sealed class BoxItemRecord
        {
            public string sourcePath { get; set; } = "";
        }

        private sealed class BoxRecord
        {
            public string boxId { get; set; } = "";
            public string name { get; set; } = "";
            public List<BoxItemRecord> items { get; set; } = new();
        }

        private sealed class WidgetRecord
        {
            public string instanceId { get; set; } = "";
            public string widgetTypeId { get; set; } = "";
        }

        private sealed class AlyaTaskRecord
        {
            public string taskId { get; set; } = "";
            public string name { get; set; } = "";
            public string status { get; set; } = "Idle";
        }

        private readonly List<BoxRecord> _boxes = new();
        private readonly List<WidgetRecord> _widgets = new();
        private readonly List<AlyaTaskRecord> _alyaTasks = new()
        {
            new AlyaTaskRecord { taskId = "task-cleanup", name = "Temporäre Dateien bereinigen", status = "Ready" },
            new AlyaTaskRecord { taskId = "task-index-sync", name = "Suchindex-Synchronisation", status = "Idle" }
        };

        public void Start()
        {
            _cts = new CancellationTokenSource();
            Task.Run(() => ListenAsync(_cts.Token));
            Console.WriteLine(" IPC-Server läuft und wartet auf Verbindungen...");
        }

        public void Stop()
        {
            _cts?.Cancel();
        }

        private async Task ListenAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using var serverStream = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous);

                    await serverStream.WaitForConnectionAsync(cancellationToken);
                    _ = Task.Run(() => HandleClientAsync(serverStream), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"IPC-Server Fehler: {ex.Message}");
                }
            }
        }

        private async Task HandleClientAsync(NamedPipeServerStream serverStream)
        {
            try
            {
                using var reader = new StreamReader(serverStream);
                using var writer = new StreamWriter(serverStream);

                string? requestJson = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(requestJson)) return;

                using var doc = JsonDocument.Parse(requestJson);
                var root = doc.RootElement;

                string service = root.GetProperty("Service").GetString() ?? "";
                string action = root.GetProperty("Action").GetString() ?? "";
                string payload = root.GetProperty("Payload").GetString() ?? "{}";

                Console.WriteLine($"[IPC] Empfangen -> Service: {service}, Action: {action}");

                string jsonResponse = ProcessKernelRequest(service, action, payload);

                await writer.WriteLineAsync(jsonResponse);
                await writer.FlushAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler bei der Client-Verarbeitung: {ex.Message}");
            }
        }

        private string ProcessKernelRequest(string service, string action, string payload)
        {
            try
            {
                return service switch
                {
                    "nwos.desktopshell" => action switch
                    {
                        "icon.listStates" => HandleIconListStates(),
                        "icon.toggleVisibility" => HandleIconToggle(payload),
                        "box.list" => HandleBoxList(),
                        "box.create" => HandleBoxCreate(payload),
                        "box.addItem" => HandleBoxAddItem(payload),
                        "search.rebuildIndex" => HandleRebuildIndex(payload),
                        "search.global" => HandleGlobalSearch(payload),
                        _ => "{\"status\":\"error\",\"message\":\"Unknown action\"}"
                    },
                    "nwos.widgets" => action switch
                    {
                        "widget.listTypes" => HandleWidgetListTypes(),
                        "widget.list" => HandleWidgetList(),
                        "widget.create" => HandleWidgetCreate(payload),
                        "widget.render" => HandleWidgetRender(payload),
                        _ => "{\"status\":\"error\",\"message\":\"Unknown widget action\"}"
                    },
                    "nwos.alya" => action switch
                    {
                        "alya.listTasks" => HandleAlyaListTasks(),
                        "alya.runTask" => HandleAlyaRunTask(payload),
                        "alya.getStatus" => HandleAlyaGetStatus(),
                        _ => "{\"status\":\"error\",\"message\":\"Unknown Alya action\"}"
                    },
                    _ => "{\"status\":\"error\",\"message\":\"Unknown service\"}"
                };
            }
            catch (Exception ex)
            {
                return $"{{\"status\":\"error\",\"message\":\"{ex.Message.Replace("\"", "\\\"")}\"}}";
            }
        }

        private string HandleRebuildIndex(string payload)
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                string rootDir = doc.RootElement.TryGetProperty("rootDirectory", out var prop)
                    ? prop.GetString() ?? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
                    : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

                _searchIndex.Clear();
                if (Directory.Exists(rootDir))
                {
                    foreach (var file in Directory.EnumerateFiles(rootDir, "*.*", SearchOption.AllDirectories))
                    {
                        _searchIndex.Add(file);
                    }
                }

                return $"{{\"entryCount\":{_searchIndex.Count}}}";
            }
            catch
            {
                return "{\"entryCount\":0}";
            }
        }

        private string HandleGlobalSearch(string payload)
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                string query = doc.RootElement.TryGetProperty("query", out var prop) ? prop.GetString()?.ToLowerInvariant() ?? "" : "";

                var items = _searchIndex
                    .Where(path => Path.GetFileName(path).ToLowerInvariant().Contains(query))
                    .Take(10)
                    .Select(path =>
                    {
                        var escapedPath = path.Replace("\\", "\\\\").Replace("\"", "\\\"");
                        var escapedName = Path.GetFileName(path).Replace("\"", "\\\"");
                        return $"{{\"path\":\"{escapedPath}\",\"displayName\":\"{escapedName}\"}}";
                    })
                    .ToList();

                return "[" + string.Join(",", items) + "]";
            }
            catch
            {
                return "[]";
            }
        }

        private string HandleBoxList()
        {
            var boxJsons = _boxes.Select(b =>
            {
                var itemStrs = b.items.Select(i =>
                {
                    var escapedPath = i.sourcePath.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    return $"{{\"sourcePath\":\"{escapedPath}\"}}";
                });
                var itemsJoined = string.Join(",", itemStrs);
                var escapedName = b.name.Replace("\"", "\\\"");
                return $"{{\"boxId\":\"{b.boxId}\",\"name\":\"{escapedName}\",\"items\":[{itemsJoined}]}}";
            });
            return "[" + string.Join(",", boxJsons) + "]";
        }

        private string HandleBoxCreate(string payload)
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                string name = doc.RootElement.TryGetProperty("name", out var prop) ? prop.GetString() ?? "Box" : "Box";

                var newBox = new BoxRecord
                {
                    boxId = "box-" + Guid.NewGuid().ToString("N")[..8],
                    name = name,
                    items = new List<BoxItemRecord>()
                };

                _boxes.Add(newBox);
                var escapedName = newBox.name.Replace("\"", "\\\"");
                return $"{{\"boxId\":\"{newBox.boxId}\",\"name\":\"{escapedName}\",\"items\":[]}}";
            }
            catch
            {
                return "{\"status\":\"error\"}";
            }
        }

        private string HandleBoxAddItem(string payload)
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                string boxId = doc.RootElement.GetProperty("boxId").GetString() ?? "";
                string sourcePath = doc.RootElement.GetProperty("sourcePath").GetString() ?? "";

                var box = _boxes.FirstOrDefault(b => b.boxId == boxId);
                if (box != null)
                {
                    box.items.Add(new BoxItemRecord { sourcePath = sourcePath });
                    return "{\"status\":\"ok\"}";
                }
                return "{\"status\":\"not_found\"}";
            }
            catch
            {
                return "{\"status\":\"error\"}";
            }
        }

        private string HandleWidgetListTypes()
        {
            return "[{\"widgetTypeId\":\"clock\",\"displayName\":\"Uhrzeit & Datum\"},{\"widgetTypeId\":\"system-monitor\",\"displayName\":\"System-Auslastung\"}]";
        }

        private string HandleWidgetList()
        {
            var widgetStrs = _widgets.Select(w => $"{{\"instanceId\":\"{w.instanceId}\",\"widgetTypeId\":\"{w.widgetTypeId}\"}}");
            return "[" + string.Join(",", widgetStrs) + "]";
        }

        private string HandleWidgetCreate(string payload)
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                string typeId = doc.RootElement.GetProperty("widgetTypeId").GetString() ?? "clock";

                var widget = new WidgetRecord
                {
                    instanceId = "w-" + Guid.NewGuid().ToString("N")[..8],
                    widgetTypeId = typeId
                };

                _widgets.Add(widget);
                return $"{{\"instanceId\":\"{widget.instanceId}\",\"widgetTypeId\":\"{widget.widgetTypeId}\"}}";
            }
            catch
            {
                return "{\"status\":\"error\"}";
            }
        }

        private string HandleWidgetRender(string payload)
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                string instanceId = doc.RootElement.GetProperty("instanceId").GetString() ?? "";
                var widget = _widgets.FirstOrDefault(w => w.instanceId == instanceId);

                if (widget == null) return "null";

                return widget.widgetTypeId switch
                {
                    "clock" => $"{{\"values\":{{\"Time\":\"{DateTime.Now:HH:mm:ss}\",\"Date\":\"{DateTime.Now:dd.MM.yyyy}\"}} }}",
                    "system-monitor" => "{\"values\":{\"CPU\":\"12%\",\"RAM\":\"4.2 GB\"}}",
                    _ => "{\"values\":{\"Status\":\"Aktiv\"}}"
                };
            }
            catch
            {
                return "null";
            }
        }

        private string HandleAlyaListTasks()
        {
            var taskStrs = _alyaTasks.Select(t =>
            {
                var escapedName = t.name.Replace("\"", "\\\"");
                return $"{{\"taskId\":\"{t.taskId}\",\"name\":\"{escapedName}\",\"status\":\"{t.status}\"}}";
            });
            return "[" + string.Join(",", taskStrs) + "]";
        }

        private string HandleAlyaRunTask(string payload)
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                string taskId = doc.RootElement.TryGetProperty("taskId", out var prop) ? prop.GetString() ?? "" : "";

                var task = _alyaTasks.FirstOrDefault(t => t.taskId == taskId);
                if (task != null)
                {
                    task.status = "Running";
                    return $"{{\"status\":\"success\",\"taskId\":\"{task.taskId}\",\"message\":\"Task gestartet\"}}";
                }
                return "{\"status\":\"not_found\"}";
            }
            catch
            {
                return "{\"status\":\"error\"}";
            }
        }

        private string HandleAlyaGetStatus()
        {
            return "{\"agent\":\"Alya\",\"state\":\"Online\",\"activeBackgroundTasks\":0}";
        }

        private string HandleIconListStates() => "{}";
        private string HandleIconToggle(string payload) => "{\"isVisible\": true}";
    }
}