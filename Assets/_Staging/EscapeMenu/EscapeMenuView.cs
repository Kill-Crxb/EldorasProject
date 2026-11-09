using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor.Events;
#endif

// The escape menu's list. Rows are built once from the entries, top to bottom, and the block
// grows to fit, so adding a button is one entry. Headers and spacers split the list into groups.
// A button with nothing wired to it shows greyed out, so planned entries can sit in the list early.
// Sits on the same object as the UIPanel (side Menu); the router opens and closes it.

namespace CrabSystem
{
    public enum EscapeMenuEntryKind { Button, Header, Spacer }

    public class EscapeMenuView : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public EscapeMenuEntryKind kind = EscapeMenuEntryKind.Button;
            public string label;

            [Tooltip("Spacer height in pixels. Buttons and headers take their template's height.")]
            public float height = 16f;

            public UnityEvent onClick = new();
        }

        [Header("Rows")]
        [SerializeField] private RectTransform list;
        [SerializeField] private Button buttonTemplate;
        [SerializeField] private TMP_Text headerTemplate;

        [Header("Entries")]
        [SerializeField] private List<Entry> entries = new();

        private void Awake()
        {
            if (list == null || buttonTemplate == null || headerTemplate == null)
            {
                Debug.LogError("[EscapeMenuView] list or row templates not assigned", this);
                return;
            }

            buttonTemplate.gameObject.SetActive(false);
            headerTemplate.gameObject.SetActive(false);

            foreach (var entry in entries) Build(entry);
        }

        private void Build(Entry entry)
        {
            switch (entry.kind)
            {
                case EscapeMenuEntryKind.Header: BuildHeader(entry); break;
                case EscapeMenuEntryKind.Spacer: BuildSpacer(entry); break;
                default: BuildButton(entry); break;
            }
        }

        private void BuildButton(Entry entry)
        {
            var button = Instantiate(buttonTemplate, list);
            button.name = entry.label;
            button.gameObject.SetActive(true);
            button.GetComponentInChildren<TMP_Text>().text = entry.label;
            button.interactable = entry.onClick.GetPersistentEventCount() > 0;
            button.onClick.AddListener(entry.onClick.Invoke);
        }

        private void BuildHeader(Entry entry)
        {
            var header = Instantiate(headerTemplate, list);
            header.name = entry.label;
            header.gameObject.SetActive(true);
            header.text = entry.label;
        }

        private void BuildSpacer(Entry entry)
        {
            var spacer = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(list, false);

            var layout = spacer.GetComponent<LayoutElement>();
            layout.minHeight = entry.height;
            layout.preferredHeight = entry.height;
        }

        public void Resume() => UIPanelRegistry.Menu?.Close();

        // Closing first hands the cursor and gameplay input back before the scene goes.
        public void QuitToMainMenu()
        {
            if (SceneLoader.Instance == null)
            {
                Debug.LogError("[EscapeMenuView] No SceneLoader in the scene", this);
                return;
            }

            Resume();
            SceneLoader.Instance.LoadMainMenu();
        }

        // SaveManager saves on quit through ManagerBrain.OnApplicationQuit.
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

#if UNITY_EDITOR
        // Starting list for a new menu; the component's Reset puts it back.
        private void Reset()
        {
            var resume = new Entry { label = "Resume" };
            var mainMenu = new Entry { label = "Main Menu" };
            var quit = new Entry { label = "Quit Game" };

            UnityEventTools.AddVoidPersistentListener(resume.onClick, Resume);
            UnityEventTools.AddVoidPersistentListener(mainMenu.onClick, QuitToMainMenu);
            UnityEventTools.AddVoidPersistentListener(quit.onClick, QuitGame);

            entries = new List<Entry>
            {
                new() { kind = EscapeMenuEntryKind.Header, label = "Game" },
                resume,
                new() { label = "Settings" },
                new() { kind = EscapeMenuEntryKind.Spacer },
                new() { kind = EscapeMenuEntryKind.Header, label = "Leave" },
                mainMenu,
                quit,
            };
        }
#endif
    }
}
