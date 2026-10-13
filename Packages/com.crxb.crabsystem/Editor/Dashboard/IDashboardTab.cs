using UnityEngine.UIElements;

// One tab of the Crab Dashboard. A tab signs up by existing: the window finds every class that implements this
// (TypeCache), makes one with its parameterless constructor and orders the rail by Group, then Order. A tab that
// holds editor objects can also implement IDisposable; the window disposes it when it closes.
//
// The contract every content tab follows: list → edit → validate → write. Errors block writing, warnings don't.

namespace CrabSystem
{
    public enum DashboardGroup
    {
        Build,
        Content,
        Play
    }

    public interface IDashboardTab
    {
        string Title { get; }
        DashboardGroup Group { get; }
        int Order { get; }

        // Called once per window, the first time the tab is shown. The tab keeps its own state between visits.
        VisualElement Build(DashboardContext context);
    }

    // What the window lends a tab: the log strip at the bottom.
    public class DashboardContext
    {
        readonly System.Action<string> log;

        public DashboardContext(System.Action<string> log)
        {
            this.log = log;
        }

        public void Log(string message) => log(message);
    }
}
