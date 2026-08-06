using UnityEngine;

[CreateAssetMenu(fileName = "New Dialogue", menuName = "NinjaGame/Dialogue/Dialogue Database")]
public class DialogueDatabase : ScriptableObject
{
    [TextArea(2, 5)]
    public string greetingText;

    public DialogueOptionData[] options;
}
