using UnityEngine;

/// <summary>
/// Hashed integer key for blackboard fact lookups.
/// Uses deterministic hashing for fast O(1) dictionary access without string allocations.
/// 
/// Architecture:
/// - Hash computed once at registration
/// - Integer comparison in hot paths (faster than string)
/// - Debug name preserved for inspector/logging
/// 
/// Performance:
/// - sizeof(int) = 4 bytes (vs string overhead)
/// - Hash comparison ~1-2 CPU cycles
/// - Zero allocations at runtime
/// 
/// Usage:
/// Code-generated constants:
///   BlackboardKey.IsWounded = 12345;
///   blackboard.GetBool(BlackboardKey.IsWounded);
/// 
/// Phase 1.3: Semantic Bridge System
/// Created: January 18, 2026
/// </summary>
public struct BlackboardKey
{
    public readonly int hash;
    public readonly string debugName;

    public BlackboardKey(string name)
    {
        debugName = name;
        hash = GenerateHash(name);
    }

    /// <summary>
    /// Generate deterministic hash from string key
    /// Uses .NET's GetHashCode (stable within session)
    /// </summary>
    private static int GenerateHash(string name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        return name.GetHashCode();
    }

    public override string ToString() => debugName ?? $"Hash_{hash}";
    
    public override int GetHashCode() => hash;
    
    public override bool Equals(object obj)
    {
        if (obj is BlackboardKey other)
            return hash == other.hash;
        return false;
    }

    public static bool operator ==(BlackboardKey a, BlackboardKey b) => a.hash == b.hash;
    public static bool operator !=(BlackboardKey a, BlackboardKey b) => a.hash != b.hash;
}
