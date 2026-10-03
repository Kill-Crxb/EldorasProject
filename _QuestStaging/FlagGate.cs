using System;
using System.Collections.Generic;

/// <summary>Anything that can answer "what is this flag's value". Unset flags read 0.</summary>
public interface IFlagSource
{
    int GetFlag(string flagId);
}

[Serializable]
public class FlagRequirement
{
    public string flagId;
    public FlagCompare op = FlagCompare.AtLeast;
    public int value = 1;

    public bool Holds(IFlagSource player, IFlagSource world)
    {
        int current = Flags.Read(flagId, player, world);

        switch (op)
        {
            case FlagCompare.Equal: return current == value;
            case FlagCompare.NotEqual: return current != value;
            case FlagCompare.AtLeast: return current >= value;
            case FlagCompare.AtMost: return current <= value;
        }

        return false;
    }
}

[Serializable]
public class FlagChange
{
    public string flagId;
    public int value = 1;
}

/// <summary>
/// One gate type for zones, quests, vendors and doors. Every "all" entry must hold, and at
/// least one "any" entry if that list is non-empty. An empty gate is always open.
/// </summary>
[Serializable]
public class Gate
{
    public List<FlagRequirement> all = new List<FlagRequirement>();
    public List<FlagRequirement> any = new List<FlagRequirement>();

    public bool IsOpen(IFlagSource player, IFlagSource world)
    {
        foreach (var requirement in all)
        {
            if (!requirement.Holds(player, world)) return false;
        }

        if (any.Count == 0) return true;

        foreach (var requirement in any)
        {
            if (requirement.Holds(player, world)) return true;
        }

        return false;
    }
}

/// <summary>
/// Flag scope is decided by prefix (World_Progression §3.1): quest. and player. live on the
/// character, zone. and world. live on the server.
/// </summary>
public static class Flags
{
    public static bool IsWorldScoped(string flagId)
    {
        if (string.IsNullOrEmpty(flagId)) return false;
        return flagId.StartsWith("zone.") || flagId.StartsWith("world.");
    }

    public static int Read(string flagId, IFlagSource player, IFlagSource world)
    {
        IFlagSource source = IsWorldScoped(flagId) ? world : player;
        return source != null ? source.GetFlag(flagId) : 0;
    }
}
