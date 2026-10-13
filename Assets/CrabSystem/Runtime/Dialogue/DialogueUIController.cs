using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Must live on an always-active object (e.g. UIManager), NOT under DialogueCanvas.
// DialogueCanvas starts inactive and is only activated by DialogueUIListener reacting
// to GameEvents.OnDialogueStarted — a script parented under it would only subscribe
// in OnEnable once active, meaning it would miss the very event meant to wake it up.
public class DialogueUIController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI npcDialogueText;
    [SerializeField] private RectTransform optionsContainer;
    [SerializeField] private Button optionTemplate;

    private readonly List<GameObject> spawnedOptions = new List<GameObject>();

    private void OnEnable()
    {
        GameEvents.OnDialogueStarted += HandleDialogueStarted;
        GameEvents.OnDialogueEnded += HandleDialogueEnded;
    }

    private void OnDisable()
    {
        GameEvents.OnDialogueStarted -= HandleDialogueStarted;
        GameEvents.OnDialogueEnded -= HandleDialogueEnded;
    }

    private void HandleDialogueStarted(ControllerBrain npc, ControllerBrain actor)
    {
        var dialogue = npc.GetModule<DialogueSystem>();
        var database = dialogue?.Database;

        if (npcDialogueText != null)
            npcDialogueText.text = database != null ? database.greetingText : "";

        if (dialogue == null)
        {
            ClearOptions();
            return;
        }

        BuildOptions(dialogue);
    }

    // Rebuilt after every pick, so an option that has just used itself up disappears
    // without the dialogue needing to close and reopen.
    private void BuildOptions(DialogueSystem dialogue)
    {
        ClearOptions();
        if (!dialogue.IsInConversation) return; // the pick ended it — OpenShop, say

        var options = dialogue.Database != null ? dialogue.Database.options : null;

        if (options != null)
        {
            foreach (var option in options)
            {
                if (!dialogue.IsOptionAvailable(option)) continue;

                SpawnOptionButton(option.optionText, () =>
                {
                    dialogue.SelectOption(option);
                    BuildOptions(dialogue);
                });
            }
        }

        SpawnOptionButton("Goodbye", () => dialogue.EndConversation());
    }

    private void HandleDialogueEnded(ControllerBrain npc)
    {
        ClearOptions();
    }

    private void SpawnOptionButton(string text, System.Action onClick)
    {
        if (optionTemplate == null || optionsContainer == null) return;

        GameObject buttonObj = Instantiate(optionTemplate.gameObject, optionsContainer);
        buttonObj.SetActive(true);
        spawnedOptions.Add(buttonObj);

        var label = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.text = text;

        var button = buttonObj.GetComponent<Button>();
        button.onClick.AddListener(() => onClick());
    }

    private void ClearOptions()
    {
        foreach (var obj in spawnedOptions)
            if (obj != null) Destroy(obj);
        spawnedOptions.Clear();
    }
}
