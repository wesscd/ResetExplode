using System.IO;
using System.Text;

namespace ResetExplode;

/// <summary>Grava no mesmo volume e só substitui o destino depois de descarregar o conteúdo.</summary>
public static class AtomicFile
{
    public static void Write(string path, string content, bool keepBackup = true)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                stream.Write(bytes);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temp, path, keepBackup ? path + ".bak" : null);
            else File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
