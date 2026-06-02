using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AnyDeck.Models;
using Microsoft.Extensions.Logging;

namespace AnyDeck.Services
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

            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AnyDeck"
            );
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
                }
                return new List<DeckProfile>(_cache);
            }
        }

        public DeckProfile? GetProfile(string id)
        {
            return GetProfiles().FirstOrDefault(p => p.Id == id);
        }

        public void SaveProfile(DeckProfile profile)
        {
            lock (_lock)
            {
                try
                {
                    var filePath = Path.Combine(_profilesDirectory, $"{profile.Id}.json");
                    var json = JsonSerializer.Serialize(profile, _jsonOptions);
                    File.WriteAllText(filePath, json);

                    var existing = _cache.FirstOrDefault(p => p.Id == profile.Id);
                    if (existing != null)
                    {
                        _cache.Remove(existing);
                    }
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

                                // DEBUG LOGGING
                                try {
                                    var debugPath = Path.Combine(_profilesDirectory, "debug_status.txt");
                                    var lines = new List<string>();
                                    lines.Add($"Profile {profile.Id} ({profile.Name}) loaded:");
                                    foreach(var btn in profile.Buttons) {
                                        lines.Add($" - Button {btn.Row},{btn.Column}: DynamicType='{btn.DynamicType}'");
                                    }
                                    File.AppendAllLines(debugPath, lines);
                                } catch {}
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
    }
}
