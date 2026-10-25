using System.IO;
using System.Text;
using System.Text.Json;

namespace ResetExplode;

public sealed record OptimizationLogEntry(DateTimeOffset Time, string Area, string Status, string Item, string Message, string? Before = null, string? After = null)
{
    public string? StorageError { get; init; }
    public string Line => $"{Time.ToLocalTime():dd/MM/yyyy HH:mm:ss} [{Area}] [{Status}] {Item}: {Message}" +
        (Before == null ? "" : " | Antes: " + Before) + (After == null ? "" : " | Depois: " + After) + (StorageError == null ? "" : " [NÃO SALVO EM DISCO: " + StorageError + "]");
}

/// <summary>Diagnóstico independente dos backups. Falha de log não esconde o resultado nem interrompe a recuperação.</summary>
public sealed class OptimizationLogStore
{
    private readonly object _gate = new();
    public string Folder { get; }
    public string? LastError { get; private set; }
    public event Action<OptimizationLogEntry>? Written;
    public OptimizationLogStore(string folder) => Folder = folder;
    public void Write(string area, string status, string item, string message, string? before = null, string? after = null)
    {
        lock (_gate)
        {
            var entry = new OptimizationLogEntry(DateTimeOffset.Now, area, status, item, message, before, after);
            try
            {
                Directory.CreateDirectory(Folder);
                string path = Path.Combine(Folder, $"optimization-{entry.Time.ToLocalTime():yyyy-MM-dd}.jsonl");
                using var stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                if (stream.Length > 0)
                {
                    stream.Seek(-1, SeekOrigin.End);
                    bool unfinished = stream.ReadByte() != '\n';
                    stream.Seek(0, SeekOrigin.End);
                    if (unfinished) stream.WriteByte((byte)'\n');
                }
                byte[] data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(entry) + "\n");
                stream.Write(data); stream.Flush(true);
                LastError = null;
            }
            catch (Exception ex) { LastError = ex.Message; }
            entry = entry with { StorageError = LastError };
            // Um observador não deve impedir alterações ou esconder erros do Windows.
            foreach (var observer in Written?.GetInvocationList() ?? Array.Empty<Delegate>())
                try { ((Action<OptimizationLogEntry>)observer)(entry); } catch { }
        }
    }
    public (List<OptimizationLogEntry> Entries, int Unreadable) ReadRecent()
    {
        lock (_gate)
        {
            var entries = new Queue<OptimizationLogEntry>(); int unreadable = 0;
            if (!Directory.Exists(Folder)) return (new(), 0);
            foreach (string file in Directory.GetFiles(Folder, "optimization-*.jsonl").OrderByDescending(p => p).Take(7).OrderBy(p => p))
            {
                try
                {
                    foreach (string line in File.ReadLines(file))
                    {
                        try
                        {
                            var entry = JsonSerializer.Deserialize<OptimizationLogEntry>(line) ?? throw new InvalidDataException();
                            if (entry.Area == null || entry.Status == null || entry.Item == null || entry.Message == null) throw new InvalidDataException();
                            entries.Enqueue(entry);
                            while (entries.Count > 500) entries.Dequeue();
                        }
                        catch { unreadable++; }
                    }
                }
                catch { unreadable++; }
            }
            return (entries.ToList(), unreadable);
        }
    }
}

public static class OptimizationLog
{
    // Somente o app inicializa o destino. Testes e ferramentas de leitura não gravam na pasta real.
    public static OptimizationLogStore? Store { get; set; }
    public static void Write(string area, string status, string item, string message, string? before = null, string? after = null)
        => Store?.Write(area, status, item, message, before, after);
}
