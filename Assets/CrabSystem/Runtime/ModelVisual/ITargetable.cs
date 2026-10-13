using UnityEngine;

namespace CrabSystem
{
    public interface ITargetable
    {
        bool CanBeTargeted { get; }
        Vector3 GetTargetPoint();
        void OnTargeted();
        void OnTargetLost();
    }
}
