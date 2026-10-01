using System;
using System.IO;
using System.Runtime.CompilerServices;
using DroidDeck.Lib;

namespace DroidDeck.Tests
{
    /// <summary>
    /// Roda antes de qualquer teste: aponta as pastas de dados do servidor para um
    /// diretorio temporario. Sem isso, os testes de integracao sobem o servidor real e leem
    /// (e migram) a chave, os segredos e os perfis de quem esta desenvolvendo.
    /// </summary>
    internal static class TestEnvironment
    {
        [ModuleInitializer]
        internal static void IsolateDataDir()
        {
            var dir = Path.Combine(Path.GetTempPath(), "droiddeck-tests-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable(AppPaths.DataDirEnvVar, dir);
        }
    }
}
