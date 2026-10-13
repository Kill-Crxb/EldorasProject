using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

// Content → Weapons. The Weapon wizard and the Weapon Asset Report, hosted as they are (IMGUI inside an
// IMGUIContainer). Each keeps its own hidden instance, so its fields survive switching tabs. Ported to UI Toolkit
// only when they're next changed.

namespace CrabSystem
{
    public class WeaponsTab : IDashboardTab, IDisposable
    {
        public string Title => "Weapons";
        public DashboardGroup Group => DashboardGroup.Content;
        public int Order => 10;

        WeaponWizard wizard;
        WeaponAssetReport report;
        IMGUIContainer host;
        Button wizardButton;
        Button reportButton;

        public VisualElement Build(DashboardContext context)
        {
            wizard = Hidden<WeaponWizard>();
            report = Hidden<WeaponAssetReport>();

            var list = new VisualElement();
            list.Add(DashboardLayout.Heading("Weapons"));
            wizardButton = Entry("Weapon wizard", ShowWizard);
            reportButton = Entry("Weapon asset report", ShowReport);
            list.Add(wizardButton);
            list.Add(reportButton);

            host = new IMGUIContainer();
            host.AddToClassList("imgui-host");
            ShowWizard();
            return DashboardLayout.Columns(list, host, null);
        }

        static T Hidden<T>() where T : ScriptableObject
        {
            var instance = ScriptableObject.CreateInstance<T>();
            instance.hideFlags = HideFlags.HideAndDontSave;
            return instance;
        }

        static Button Entry(string label, Action onClick)
        {
            var button = new Button(onClick) { text = label };
            button.AddToClassList("list-entry");
            return button;
        }

        void ShowWizard()
        {
            host.onGUIHandler = DrawWizard;
            wizardButton.EnableInClassList("selected", true);
            reportButton.EnableInClassList("selected", false);
        }

        void ShowReport()
        {
            host.onGUIHandler = DrawReport;
            wizardButton.EnableInClassList("selected", false);
            reportButton.EnableInClassList("selected", true);
        }

        void DrawWizard() => wizard.OnGUI();
        void DrawReport() => report.OnGUI();

        public void Dispose()
        {
            if (wizard != null) Object.DestroyImmediate(wizard);
            if (report != null) Object.DestroyImmediate(report);
        }
    }
}
