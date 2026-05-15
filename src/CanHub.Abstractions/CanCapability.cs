namespace CanHub;

/// <summary>
/// 适配器能力描述。<br/>
/// Describes an adapter capability.
/// </summary>
public sealed class CanCapability
{
    /// <summary>能力名称（如 "can-fd"、"bus-off-recovery"、"hardware-periodic"）。<br/>Capability name (e.g., "can-fd", "bus-off-recovery", "hardware-periodic").</summary>
    public string Name { get; }

    /// <summary>打开时是否必须支持此能力。<br/>Whether this capability must be supported when opening.</summary>
    public bool IsRequired { get; }

    /// <summary>可选描述。<br/>Optional description.</summary>
    public string? Description { get; }

    /// <summary>创建一个适配器能力描述。<br/>Creates an adapter capability descriptor.</summary>
    public CanCapability(string name, bool isRequired, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        IsRequired = isRequired;
        Description = description;
    }
}
