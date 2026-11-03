using DroidDeck.Lib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DroidDeck.Models;
using Microsoft.Extensions.Logging;

namespace DroidDeck.Services
{
    public class StreamDeckConfigService
    {
        private readonly ILogger<StreamDeckConfigService> _logger;
        private readonly string _profilesDirectory;
        private readonly string _layoutPath;
        private readonly object _lock = new object();
        private List<DeckProfile> _cache = new List<DeckProfile>();
        private DeviceLayout _layout = new DeviceLayout();

        private readonly JsonSerializerOptions _jsonOptions;

        public StreamDeckConfigService(ILogger<StreamDeckConfigService> logger)
        {
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            var baseDir = AppPaths.RoamingDir;
            _profilesDirectory = Path.Combine(baseDir, "Profiles");
            _layoutPath = Path.Combine(baseDir, "layout.json");

            EnsureDirectoryExists();
            LoadProfiles();
            LoadLayout();
        }

        // ---- Grade física do deck (definida pelo celular, usada pelo configurador) ----
        public DeviceLayout GetLayout()
        {
            lock (_lock)
            {
                return new DeviceLayout { Rows = _layout.Rows, Columns = _layout.Columns };
            }
        }

        public void SaveLayout(DeviceLayout layout)
        {
            lock (_lock)
            {
                _layout = new DeviceLayout
                {
                    Rows = System.Math.Clamp(layout.Rows, 1, 20),
                    Columns = System.Math.Clamp(layout.Columns, 1, 20)
                };
                try { File.WriteAllText(_layoutPath, JsonSerializer.Serialize(_layout, _jsonOptions)); }
                catch (Exception ex) { _logger.LogError(ex, "Error saving layout"); }
            }
        }

        private void LoadLayout()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_layoutPath))
                        _layout = JsonSerializer.Deserialize<DeviceLayout>(File.ReadAllText(_layoutPath), _jsonOptions)
                                  ?? new DeviceLayout();
                }
                catch (Exception ex) { _logger.LogError(ex, "Error loading layout"); }
            }
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_profilesDirectory))
            {
                Directory.CreateDirectory(_profilesDirectory);
            }
        }

        public List<DeckProfile> GetProfiles()
        {
            lock (_lock)
            {
                if (_cache.Count == 0)
                {
                    LoadProfiles();
                }

                if (_cache.Count == 0)
                {
                    var defaultProfile = new DeckProfile { Name = "Default", IsDefault = true };
                    SaveProfile(defaultProfile);
                    foreach (var keyboard in CreateKeyboardProfiles()) SaveProfile(keyboard);
                }
                return new List<DeckProfile>(_cache);
            }
        }

        public DeckProfile? GetProfile(string id)
        {
            return GetProfiles().FirstOrDefault(p => p.Id == id);
        }

        // SEGURANÇA: o Id vem do corpo JSON do cliente e é usado para compor um caminho de
        // arquivo. Sem validação, "..\\..\\..\\algo" permitiria escrever/apagar fora da pasta
        // de perfis (path traversal). Ids legítimos são GUID (backend) ou timestamp numérico
        // (app) — ambos casam com este charset. Rejeita separadores, "." e vazio.
        public static bool IsValidProfileId(string? id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 128) return false;
            foreach (var c in id)
            {
                if (!(char.IsAsciiLetterOrDigit(c) || c == '-' || c == '_')) return false;
            }
            return true;
        }

        public void SaveProfile(DeckProfile profile)
        {
            if (profile == null || !IsValidProfileId(profile.Id))
            {
                _logger.LogWarning("SaveProfile: Id de perfil invalido rejeitado: {Id}", profile?.Id);
                return;
            }
            lock (_lock)
            {
                try
                {
                    var filePath = Path.Combine(_profilesDirectory, $"{profile.Id}.json");
                    var json = JsonSerializer.Serialize(profile, _jsonOptions);
                    // Grava ao lado e troca: uma queda no meio nao deixa o perfil truncado.
                    var tmp = filePath + ".tmp";
                    File.WriteAllText(tmp, json);
                    File.Move(tmp, filePath, overwrite: true);

                    // Substitui no mesmo lugar. Remover e adicionar no fim mudava a ordem dos
                    // perfis a cada edicao, e o celular abria em outra pagina do deck.
                    var index = _cache.FindIndex(p => p.Id == profile.Id);
                    if (index >= 0)
                        _cache[index] = profile;
                    else
                        _cache.Add(profile);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error saving profile {Id}", profile.Id);
                }
            }
        }

        public void DeleteProfile(string id)
        {
            if (!IsValidProfileId(id))
            {
                _logger.LogWarning("DeleteProfile: Id de perfil invalido rejeitado: {Id}", id);
                return;
            }
            lock (_lock)
            {
                try
                {
                    var filePath = Path.Combine(_profilesDirectory, $"{id}.json");
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }

                    var existing = _cache.FirstOrDefault(p => p.Id == id);
                    if (existing != null)
                    {
                        _cache.Remove(existing);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error deleting profile {Id}", id);
                }
            }
        }

        private void LoadProfiles()
        {
            lock (_lock)
            {
                _cache.Clear();
                try
                {
                    var files = Directory.GetFiles(_profilesDirectory, "*.json");
                    foreach (var file in files)
                    {
                        try
                        {
                            var json = File.ReadAllText(file);
                            var profile = JsonSerializer.Deserialize<DeckProfile>(json, _jsonOptions);
                            if (profile != null)
                            {
                                _cache.Add(profile);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error loading profile file {File}", file);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error loading profiles directory");
                }
            }
        }

        public const string KeyboardProfileId = "keyboard_profile";

        /// <summary>
        /// Perfil "Teclado" de exemplo, com duas pastas (F1-F12 e "Mais"). Cada pagina tem
        /// 3x5, a grade padrao: botao fora da grade do celular some, entao um perfil maior
        /// perdia as setas num celular de grade pequena.
        /// </summary>
        public static List<DeckProfile> CreateKeyboardProfiles()
        {
            const string fkeysId = "keyboard_fkeys", moreId = "keyboard_more";

            DeckButton Key(int row, int col, string label, string key, string mode = "hold", string? icon = null, string bg = "#252530") => new()
            {
                Row = row,
                Column = col,
                Label = label,
                IconName = icon,
                BackgroundColor = bg,
                ActiveColor = mode == "hold" ? null : "#3F51B5",
                Action = new DeckAction { Type = "key", Parameters = new() { ["key"] = key, ["mode"] = mode } },
            };
            DeckButton Shortcut(int row, int col, string label, string keys, string icon) => new()
            {
                Row = row,
                Column = col,
                Label = L.T(label),
                IconName = icon,
                BackgroundColor = "#202830",
                Action = new DeckAction { Type = "hotkey", Parameters = new() { ["keys"] = keys } },
            };
            DeckButton Folder(int row, int col, string label, string profileId) => new()
            {
                Row = row,
                Column = col,
                Label = label,
                IconName = "folder",
                BackgroundColor = "#303040",
                Action = new DeckAction { Type = "open_profile", Parameters = new() { ["profileId"] = profileId } },
            };
            DeckButton Back(int row, int col) => new()
            {
                Row = row,
                Column = col,
                Label = L.T("Voltar"),
                IconName = "arrow_back",
                BackgroundColor = "#303040",
                Action = new DeckAction { Type = "back" },
            };

            var main = new DeckProfile
            {
                Id = KeyboardProfileId,
                Name = L.T("Teclado"),
                Rows = 3,
                Columns = 5,
                Buttons =
                {
                    Key(0, 0, "ESC", "ESC", bg: "#302020"),
                    Key(0, 1, "TAB", "TAB"),
                    Key(0, 2, "", "UP", icon: "arrow_upward", bg: "#252538"),
                    Key(0, 3, "", "BACKSPACE", icon: "backspace"),
                    Key(0, 4, "", "ENTER", icon: "keyboard_return", bg: "#203025"),
                    // Modificadores: travam ate a proxima tecla (Shift + seta seleciona).
                    Key(1, 0, "SHIFT", "SHIFT", "oneshot", bg: "#2A2A38"),
                    Key(1, 1, "", "LEFT", icon: "arrow_back", bg: "#252538"),
                    Key(1, 2, "", "DOWN", icon: "arrow_downward", bg: "#252538"),
                    Key(1, 3, "", "RIGHT", icon: "arrow_forward", bg: "#252538"),
                    Key(1, 4, "", "SPACE"),
                    Key(2, 0, "CTRL", "CTRL", "oneshot", bg: "#2A2A38"),
                    Key(2, 1, "ALT", "ALT", "oneshot", bg: "#2A2A38"),
                    Key(2, 2, "WIN", "WIN", "oneshot", bg: "#2A2A38"),
                    Folder(2, 3, "F1–F12", fkeysId),
                    Folder(2, 4, L.T("Mais"), moreId),
                },
            };

            var fkeys = new DeckProfile { Id = fkeysId, Name = L.T("Teclado") + " · F1–F12", Rows = 3, Columns = 5 };
            for (int i = 0; i < 12; i++)
                fkeys.Buttons.Add(Key(i / 5, i % 5, $"F{i + 1}", $"F{i + 1}", bg: "#202028"));
            fkeys.Buttons.Add(Back(2, 4));

            var more = new DeckProfile
            {
                Id = moreId,
                Name = L.T("Teclado") + " · " + L.T("Mais"),
                Rows = 3,
                Columns = 5,
                Buttons =
                {
                    Shortcut(0, 0, "Copiar", "^c", "content_copy"),
                    Shortcut(0, 1, "Colar", "^v", "content_paste"),
                    Shortcut(0, 2, "Recortar", "^x", "content_cut"),
                    Shortcut(0, 3, "Desfazer", "^z", "undo"),
                    Shortcut(0, 4, "Selecionar tudo", "^a", "select_all"),
                    Key(1, 0, "HOME", "HOME"),
                    Key(1, 1, "END", "END"),
                    Key(1, 2, "PG UP", "PGUP"),
                    Key(1, 3, "PG DN", "PGDN"),
                    Shortcut(1, 4, "Fechar janela", "%{F4}", "close"),
                    Key(2, 0, "INS", "INSERT"),
                    Key(2, 1, "DEL", "DELETE", bg: "#352020"),
                    Back(2, 4),
                },
            };

            return new List<DeckProfile> { main, fkeys, more };
        }
    }
}
