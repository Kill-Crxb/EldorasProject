using System;

/// <summary>
/// Root object that gets JSON-serialised by HotbarSystem.
/// Each field maps to one of the three action bars.
/// </summary>
[Serializable]
public class HotbarSaveData
{
    public int             version        = 1;
    public ActionBarConfig centreBar;
    public ActionBarConfig bottomLeftBar;
    public ActionBarConfig bottomRightBar;
}
