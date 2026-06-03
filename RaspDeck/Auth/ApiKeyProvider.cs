using System;
using System.IO;
using System.Security.Cryptography;

namespace DroidDeck.Auth
{
    /// <summary>
    /// Carrega (ou gera na primeira vez) a chave de API forte usada para autenticar o app.
    /// Persistida em %LocalAppData%\DroidDeck\apikey — fora do código e do repositório.
    /// Substitui a antiga chave fixa "changeme".
    /// </summary>
    public static class ApiKeyProvider
    {
        private static readonly object _lock = new();
        private static string? _cached;

        private static string KeyFilePath
        {
            get
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DroidDeck");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "apikey");
            }
        }

        /// <summary>Chave atual (gera e persiste na primeira chamada).</summary>
        public static string GetKey()
        {
            if (_cached != null) return _cached;
            lock (_lock)
            {
                if (_cached != null) return _cached;
                try
                {
                    var path = KeyFilePath;
                    if (File.Exists(path))
                    {
                        var existing = File.ReadAllText(path).Trim();
                        if (!string.IsNullOrWhiteSpace(existing))
                            return _cached = existing;
                    }
                    var generated = Generate();
                    File.WriteAllText(path, generated);
                    return _cached = generated;
                }
                catch
                {
                    // Se não der pra persistir, mantém uma chave só em memória (sessão atual).
                    return _cached ??= Generate();
                }
            }
        }

        private static string Generate()
        {
            // 192 bits, base64url (sem +/=) para caber tranquilo em URL/QR.
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
                .Replace("+", "-").Replace("/", "_").Replace("=", "");
        }
    }
}
