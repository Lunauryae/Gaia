using Dalamud.Configuration;

namespace Gaia.Core.Data;

/// <summary>
/// Base configuration for Gaia.
///
/// Provides the Initialize(IDalamudPluginInterface) + Save() pattern.
/// Subclasses add their own plugin-specific fields.
/// </summary>
[Serializable]
public abstract class BaseConfiguration : IPluginConfiguration
{
    /// <summary>
    /// Configuration schema version. Increment when you add migrations.
    /// Subclasses should check this in their constructor and run any
    /// needed upgrade logic.
    /// </summary>
    public int Version { get; set; } = 0;

    /// <summary>
    /// When true, Save() is a no-op. Used during development to prevent
    /// experimental changes from being persisted.
    /// </summary>
    [NonSerialized] public bool DevMode = false;

    [NonSerialized] private IDalamudPluginInterface? _pluginInterface;

    /// <summary>
    /// Bind this config to the Dalamud plugin interface. Must be called
    /// once after loading the config in the plugin constructor.
    /// </summary>
    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        _pluginInterface = pluginInterface;
        OnInitialize();
    }

    /// <summary>
    /// Override for one-time setup after Initialize (e.g. first-run defaults).
    /// Base implementation does nothing.
    /// </summary>
    protected virtual void OnInitialize() { }

    /// <summary>
    /// Persist the configuration to disk. No-op if DevMode is active.
    /// </summary>
    public void Save()
    {
        if (DevMode) return;
        _pluginInterface!.SavePluginConfig(this);
    }
}
