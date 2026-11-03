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
                    SaveProfile(CreateKeyboardProfile());
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
        public static DeckProfile CreateKeyboardProfile()
        {
            var profile = new DeckProfile
            {
                Id = "keyboard_profile",
                Name = "Teclado (Setas & F-Keys)",
                Rows = 5,
                Columns = 6,
                IsDefault = false,
                Buttons = new List<DeckButton>()
            };

            for (int i = 1; i <= 6; i++)
            {
                profile.Buttons.Add(new DeckButton
                {
                    Row = 0,
                    Column = i - 1,
                    Label = $"F{i}",
                    BackgroundColor = "#202028",
                    Action = new DeckAction { Type = "key", Parameters = new() { ["key"] = $"F{i}", ["mode"] = "hold" } }
                });
            }
            for (int i = 7; i <= 12; i++)
            {
                profile.Buttons.Add(new DeckButton
                {
                    Row = 1,
                    Column = i - 7,
                    Label = $"F{i}",
                    BackgroundColor = "#202028",
                    Action = new DeckAction { Type = "key", Parameters = new() { ["key"] = $"F{i}", ["mode"] = "hold" } }
                });
            }

            profile.Buttons.Add(new DeckButton
            {
                Row = 2,
                Column = 0,
                Label = "ESC",
                BackgroundColor = "#302020",
                Action = new DeckAction { Type = "key", Parameters = new() { ["key"] = "ESC", ["mode"] = "hold" } }
            });
            profile.Buttons.Add(new DeckButton
            {
                Row = 2,
                Column = 1,
                Label = "TAB",
                BackgroundColor = "#252530",
                Action = new DeckAction { Type = "key", Parameters = new() { ["key"] = "TAB", ["mode"] = "hold" } }
            });
            profile.Buttons.Add(new DeckButton
            {
                Row = 2,
                Column = 2,
                Label = "BKSP",
                IconName = "backspace",
                BackgroundColor = "#252530",
                Action = new DeckAction { Type = "key", Parameters = new() { ["key"] = "BACKSPACE", ["mode"] = "hold" } }
            });
            profile.Buttons.Add(new DeckButton
            {
                Row = 2,
                Column = 3,
                Label = "ENTER",
                IconName = "keyboard_return",
                BackgroundColor = "#203025",
                Action = new DeckAction { Type = "key", Parameters = new() { ["key"] = "ENTER", ["mode"] = "hold" } }
            });
            profile.Buttons.Add(new DeckButton
            {
                Row = 2,
                Column = 4,
                Label = "INS",
                BackgroundColor = "#252530",
                Action = new DeckAction { Type = "key", Parameters = new() { ["key"] = "INSERT", ["mode"] = "hold" } }
            });
            profile.Buttons.Add(new DeckButton
            {
                Row = 2,
                Column = 5,
                Label = "DEL",
                BackgroundColor = "#352020",
                Action = new DeckAction { Type = "key", Parameters = new() { ["key"] = "DELETE", ["mode"] = "hold" } }
            });

            var modifiers = new (string key, string label)[]
            {
                ("SHIFT", "SHIFT"),
                ("CTRL", "CTRL"),
                ("ALT", "ALT"),
                ("WIN", "WIN")
            };
            for (int c = 0; c < modifiers.Length; c++)
            {
                var mod = modifiers[c];
                profile.Buttons.Add(new DeckButton
                {
                    Row = 3,
                    Column = c,
                    Label = mod.label,
                    Toggle = true,
                    BackgroundColor = "#2A2A38",
                    ActiveColor = "#3F51B5",
                    Action = new DeckAction { Type = "key", Parameters = new() { ["key"] = mod.key, ["mode"] = "toggle" } }
                });
            }
            profile.Buttons.Add(new DeckButton
            {
                Row = 3,
                Column = 4,
                Label = "HOME",
                BackgroundColor = "#252530",
                Action = new DeckAction { Type = "key", Parameters = new() { ["key"] = "HOME", ["mode"] = "hold" } }
            });
            profile.Buttons.Add(new DeckButton
            {
                Row = 3,
                Column = 5,
                Label = "END",
                BackgroundColor = "#252530",
                Action = new DeckAction { Type = "key", Parameters = new() { ["key"] = "END", ["mode"] = "hold" } }
            });

            profile.Buttons.Add(new DeckButton
            {
                Row = 4,
                Column = 0,
                Label = "PG UP",
                BackgroundColor = "#252530",
                Action = new DeckAction { Type = "key", Parameters = new() { ["key"] = "PGUP", ["mode"] = "hold" } }
            });
            profile.Buttons.Add(new DeckButton
            {
                Row = 4,
                Column = 1,
                Label = "PG DN",
                BackgroundColor = "#252530",
                Action = new DeckAction { Type = "key", Parameters = new() { ["key"] = "PGDN", ["mode"] = "hold" } }
            });
            profile.Buttons.Add(new DeckButton
            {
                Row = 4,
                Column = 2,
                Label = "◄",
                IconName = "arrow_back",
                BackgroundColor = "#252538",
                Action = new DeckAction { Type = "key", Parameters = new() { ["key"] = "LEFT", ["mode"] = "hold" } }
            });
            profile.Buttons.Add(new DeckButton
            {
                Row = 4,
                Column = 3,
                Label = "▲",
                IconName = "arrow_upward",
                BackgroundColor = "#252538",
                Action = new DeckAction { Type = "key", Parameters = new() { ["key"] = "UP", ["mode"] = "hold" } }
            });
            profile.Buttons.Add(new DeckButton
            {
                Row = 4,
                Column = 4,
                Label = "▼",
                IconName = "arrow_downward",
                BackgroundColor = "#252538",
                Action = new DeckAction { Type = "key", Parameters = new() { ["key"] = "DOWN", ["mode"] = "hold" } }
            });
            profile.Buttons.Add(new DeckButton
            {
                Row = 4,
                Column = 5,
                Label = "►",
                IconName = "arrow_forward",
                BackgroundColor = "#252538",
                Action = new DeckAction { Type = "key", Parameters = new() { ["key"] = "RIGHT", ["mode"] = "hold" } }
            });

            return profile;
        }
    }
}
