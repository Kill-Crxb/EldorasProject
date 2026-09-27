using System;
using UnityEditor;
using UnityEngine;

namespace FXMeshGeneratorPro.UnityEdition
{
    public sealed partial class FXMeshGeneratorProUnityEditionWindow
    {
        // Centralized IMGUI layout helpers.
        // Keep all button sizing and section spacing here so future UI passes do not drift per panel.
        private static bool UiButton(string label)
        {
            return GUILayout.Button(label, GUILayout.Height(UiButtonHeight));
        }

        private static bool UiSmallButton(string label)
        {
            return GUILayout.Button(label, GUILayout.Height(UiSmallButtonHeight));
        }

        private static bool UiEqualButton(Rect rect, string label)
        {
            return GUI.Button(rect, label);
        }

        private static Rect GetEqualButtonRect(Rect rowRect, int index, int count, float gap)
        {
            count = Mathf.Max(1, count);
            float width = (rowRect.width - gap * (count - 1)) / count;
            return new Rect(rowRect.x + index * (width + gap), rowRect.y, width, rowRect.height);
        }

        private static void UiRowSpace()
        {
            GUILayout.Space(UiRowGap);
        }

        private static void DrawUiSeparator()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.12f));
            GUILayout.Space(4f);
        }

        private static void DrawUiSectionHeader(string title, string description)
        {
            GUILayout.Space(UiSectionGap);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (!string.IsNullOrEmpty(description))
            {
                EditorGUILayout.HelpBox(description, MessageType.None);
            }
            GUILayout.Space(2f);
        }

        private static void DrawUiSubHeader(string title)
        {
            GUILayout.Space(UiRowGap);
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            DrawUiSeparator();
        }

        // Editor scene changes, demo-scene builds, and asset refresh operations must not run directly inside OnGUI.
        // This helper centralizes delayCall scheduling and exception handling to keep IMGUI layout state stable.
        private void ScheduleDelayedEditorOperation(string dialogTitle, Action operation, string failureMessageEnglish, string failureMessageKorean)
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null)
                {
                    return;
                }

                try
                {
                    operation?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    EditorUtility.DisplayDialog(dialogTitle, Tr(failureMessageEnglish, failureMessageKorean), "OK");
                }
                finally
                {
                    Repaint();
                }
            };

            Repaint();
        }
    }
}
