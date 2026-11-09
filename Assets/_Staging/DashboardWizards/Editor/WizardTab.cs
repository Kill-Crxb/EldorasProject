using System.Collections.Generic;
using System.IO;
using CrabSystem;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// The shape the staging wizards share (Item Setup, NPC Setup): a step rail on the left, the step in the middle, and
// the checks, what the write will do and the write button on the right. Errors block the button, warnings don't (the
// dashboard's tab contract). Graduates into CrabSystem/Editor/Dashboard with the first wizard that moves there.

public class WizardIssue
{
    public bool error;
    public string text;
}

public abstract class WizardTab : IDashboardTab
{
    public abstract string Title { get; }
    public DashboardGroup Group => DashboardGroup.Content;
    public abstract int Order { get; }

    protected abstract string[] StepNames { get; }
    protected abstract string WriteLabel { get; }
    protected abstract string RailNote { get; }

    protected abstract VisualElement StepContent(int step);
    protected abstract void CollectIssues(List<WizardIssue> issues);
    protected abstract List<string> PlanLines();
    protected abstract void Write();

    protected virtual void OnBuild() { }
    protected virtual void OnStepShown(int step) { }
    protected virtual void OnRefresh() { }

    // Shown above every step, e.g. a CharacterStage. Null: the steps have the column to themselves.
    protected virtual VisualElement Stage() => null;

    protected DashboardContext Context { get; private set; }
    protected int Step { get; private set; }

    private readonly List<Button> stepButtons = new List<Button>();
    private VisualElement editor;
    private VisualElement checks;

    public VisualElement Build(DashboardContext dashboard)
    {
        Context = dashboard;
        editor = new ScrollView { style = { flexGrow = 1 } };
        checks = new ScrollView { style = { flexGrow = 1 } };

        OnBuild();
        var layout = DashboardLayout.Columns(StepRail(), Middle(), checks);
        ShowStep(0);
        return layout;
    }

    protected void ShowStep(int index)
    {
        Step = index;
        for (int i = 0; i < stepButtons.Count; i++)
            stepButtons[i].EnableInClassList("selected", i == Step);

        OnStepShown(Step);
        editor.Clear();
        editor.Add(StepContent(Step));
        editor.Add(StepButtons());
        RefreshChecks();
    }

    protected void RefreshChecks()
    {
        checks.Clear();
        var issues = Issues();

        checks.Add(DashboardLayout.Heading("Checks"));
        if (issues.Count == 0) checks.Add(DashboardLayout.Note("Ready."));
        foreach (var issue in issues)
            checks.Add(IssueLabel(issue));

        checks.Add(DashboardLayout.Heading("Will do"));
        foreach (string line in PlanLines())
            checks.Add(DashboardLayout.Note(line));

        var write = new Button(TryWrite) { text = WriteLabel };
        write.SetEnabled(!issues.Exists(i => i.error));
        checks.Add(write);
        OnRefresh();
    }

    private VisualElement Middle()
    {
        var stage = Stage();
        if (stage == null) return editor;

        var middle = new VisualElement { style = { flexGrow = 1 } };
        middle.Add(stage);
        middle.Add(editor);
        return middle;
    }

    private void TryWrite()
    {
        if (Issues().Exists(i => i.error)) return;
        Write();
    }

    private List<WizardIssue> Issues()
    {
        var issues = new List<WizardIssue>();
        CollectIssues(issues);
        return issues;
    }

    private VisualElement StepRail()
    {
        var rail = new VisualElement();
        rail.Add(DashboardLayout.Heading("Steps"));
        for (int i = 0; i < StepNames.Length; i++)
        {
            int index = i;
            var button = new Button(() => ShowStep(index)) { text = $"{i + 1}  {StepNames[i]}" };
            button.AddToClassList("rail-tab");
            stepButtons.Add(button);
            rail.Add(button);
        }

        rail.Add(DashboardLayout.Note(RailNote));
        return rail;
    }

    private VisualElement StepButtons()
    {
        var row = Row();
        row.style.marginTop = 10;
        var back = new Button(() => ShowStep(Step - 1)) { text = "Back" };
        var next = new Button(() => ShowStep(Step + 1)) { text = "Next" };
        back.SetEnabled(Step > 0);
        next.SetEnabled(Step < StepNames.Length - 1);
        row.Add(back);
        row.Add(next);
        return row;
    }

    private static Label IssueLabel(WizardIssue issue)
    {
        var label = new Label((issue.error ? "✗  " : "!  ") + issue.text) { style = { whiteSpace = WhiteSpace.Normal, marginBottom = 3 } };
        label.style.color = issue.error ? new Color(0.9f, 0.45f, 0.35f) : new Color(0.9f, 0.75f, 0.35f);
        return label;
    }

    protected static void Error(List<WizardIssue> issues, string text) => issues.Add(new WizardIssue { error = true, text = text });

    protected static void Warn(List<WizardIssue> issues, string text) => issues.Add(new WizardIssue { error = false, text = text });

    protected static VisualElement Row()
    {
        return new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, flexWrap = Wrap.Wrap } };
    }

    protected static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
