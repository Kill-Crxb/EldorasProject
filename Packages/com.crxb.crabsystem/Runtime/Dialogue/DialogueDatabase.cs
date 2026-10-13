using UnityEngine;

namespace CrabSystem
{
    [CreateAssetMenu(fileName = "New Dialogue", menuName = "CrabSystem/Dialogue/Dialogue Database")]
    public class DialogueDatabase : ScriptableObject
    {
        [TextArea(2, 5)]
        public string greetingText;

        public DialogueOptionData[] options;
    }
}
