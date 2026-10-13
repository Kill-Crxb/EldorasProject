using UnityEditor;
using UnityEngine;

// Text field plus a dropdown of the ids that exist. The text stays editable so an id can be
// typed while its asset is being made; a value that matches nothing shows red instead of
// being cleared. Dotted ids ("core.body") are grouped into submenus by their prefix.
[CustomPropertyDrawer(typeof(IdRefAttribute))]
public class IdRefDrawer : PropertyDrawer
{
    private const float ButtonWidth = 20f;
    private static readonly Color MissingColor = new(1f, 0.45f, 0.45f);

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.LabelField(position, label.text, "[IdRef] only works on strings");
            return;
        }

        var kind = ((IdRefAttribute)attribute).Kind;
        var fieldRect = new Rect(position.x, position.y, position.width - ButtonWidth - 2f, position.height);
        var buttonRect = new Rect(position.xMax - ButtonWidth, position.y, ButtonWidth, position.height);

        bool missing = !string.IsNullOrEmpty(property.stringValue) && !IdSources.Exists(kind, property.stringValue);
        var previous = GUI.color;
        if (missing) GUI.color = MissingColor;

        EditorGUI.BeginProperty(position, label, property);
        property.stringValue = EditorGUI.TextField(fieldRect, label, property.stringValue);
        EditorGUI.EndProperty();

        GUI.color = previous;

        if (GUI.Button(buttonRect, "▾", EditorStyles.miniButton))
            ShowMenu(property, kind);
    }

    private static void ShowMenu(SerializedProperty property, IdKind kind)
    {
        var menu = new GenericMenu();
        var target = property.serializedObject;
        string path = property.propertyPath;
        string current = property.stringValue;

        menu.AddItem(new GUIContent("(none)"), string.IsNullOrEmpty(current), () => Assign(target, path, ""));
        menu.AddSeparator("");

        foreach (string id in IdSources.Get(kind))
        {
            string chosen = id;
            menu.AddItem(new GUIContent(id.Replace('.', '/')), id == current, () => Assign(target, path, chosen));
        }

        if (IdSources.Get(kind).Length == 0)
            menu.AddDisabledItem(new GUIContent($"No {kind} ids found in any asset"));

        menu.ShowAsContext();
    }

    private static void Assign(SerializedObject target, string path, string id)
    {
        target.Update();
        target.FindProperty(path).stringValue = id;
        target.ApplyModifiedProperties();
    }
}
