using UnityEngine;

namespace CrabSystem
{
    public interface IInputHandler
    {
        void SubscribeToInputs(PlayerInputControls inputControls);
        void UnsubscribeFromInputs(PlayerInputControls inputControls);
    }
}
