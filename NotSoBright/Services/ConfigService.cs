using System;
using System.IO;
using System.Text.Json;
using NotSoBright.Models;
using Serilog;

namespace NotSoBright.Services;

public sealed class ConfigService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string ConfigPath { get; }
    public event EventHandler<Exception>? ConfigSaveFailed;

    public ConfigService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        ConfigPath = Path.Combine(appData, "NotSoBright", "config.json");
    }

    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                Log.Information("Config file does not exist, using defaults");
                return new AppConfig();
            }

            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
            if (config == null)
            {
                Log.Warning("Config deserialization returned null, trying backup");
                return LoadFromBackup();
            }
            Log.Information("Config loaded successfully");
            return config;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load config, trying backup");
            return LoadFromBackup();
        }
    }

    private AppConfig LoadFromBackup()
    {
        try
        {
            var backupPath = ConfigPath + ".bak";
            if (File.Exists(backupPath))
            {
                var json = File.ReadAllText(backupPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                if (config != null)
                {
                    Log.Information("Config loaded from backup");
                    return config;
                }
            }
        }
        catch (Exception ex2)
        {
            Log.Error(ex2, "Failed to load config from backup");
        }
        return new AppConfig();
    }

    public void Save(AppConfig config)
    {
        if (config is null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        try
        {
            var directory = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Atomic write: serialize to a temp file then rename so a crash
            // mid-write never leaves a truncated / empty config.json.
            var tempPath = ConfigPath + ".tmp";
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(tempPath, json);

            // Keep one backup before replacing the live config.
            if (File.Exists(ConfigPath))
            {
                var backupPath = ConfigPath + ".bak";
                File.Copy(ConfigPath, backupPath, overwrite: true);
            }

            File.Move(tempPath, ConfigPath, overwrite: true);
            Log.Information("Config saved successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save config");
            ConfigSaveFailed?.Invoke(this, ex);
        }
    }
}
