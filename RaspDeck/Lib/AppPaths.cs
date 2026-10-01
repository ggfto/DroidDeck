using System;
using System.IO;

namespace DroidDeck.Lib
{
    /// <summary>
    /// Pastas de dados do DroidDeck.
    ///   Local   (%LocalAppData%\DroidDeck): chave, segredos das integracoes, cache.
    ///   Roaming (%AppData%\DroidDeck):      perfis e grade.
    ///
    /// DROIDDECK_DATA_DIR troca as duas por uma pasta so (Roaming vira um subdiretorio).
    /// Existe para os testes: sem ela, subir o servidor num teste lia e regravava a chave e
    /// os segredos reais de quem estava desenvolvendo.
    /// </summary>
    public static class AppPaths
    {
        public const string DataDirEnvVar = "DROIDDECK_DATA_DIR";

        private static string? Override =>
            Environment.GetEnvironmentVariable(DataDirEnvVar) is { Length: > 0 } dir ? dir : null;

        public static string LocalDir => Ensure(Override ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DroidDeck"));

        public static string RoamingDir => Ensure(Override is { } dir
            ? Path.Combine(dir, "Roaming")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DroidDeck"));

        private static string Ensure(string dir)
        {
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
