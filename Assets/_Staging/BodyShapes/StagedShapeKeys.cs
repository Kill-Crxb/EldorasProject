// Staged shape keys: one body region authored as a family of whole shapes, "Chest 10%" … "Chest 100%", with Basis
// as step 0. Each key is the full shape at that step, so a step pushes its own key to 100 and the rest to 0.

public static class StagedShapeKeys
{
    // "Chest 10%" → 10; -1 when the name isn't a staged key.
    public static int Percent(string shape)
    {
        int space = shape.LastIndexOf(' ');
        if (space < 1 || !shape.EndsWith("%")) return -1;

        return int.TryParse(shape.Substring(space + 1, shape.Length - space - 2), out int percent) ? percent : -1;
    }

    // "Chest 10%" → "Chest"; null when the name isn't a staged key.
    public static string Family(string shape)
    {
        return Percent(shape) < 0 ? null : shape.Substring(0, shape.LastIndexOf(' '));
    }

    public static string Name(string family, int percent)
    {
        return $"{family} {percent}%";
    }
}
