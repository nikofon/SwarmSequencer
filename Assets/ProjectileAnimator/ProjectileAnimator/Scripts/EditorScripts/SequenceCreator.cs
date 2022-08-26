using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

public class SequenceCreator : EditorWindow
{

    [MenuItem("/Window/ProjectileAnimator/SequenceCreator")]
    private static void ShowWindow()
    {
        var window = GetWindow<SequenceCreator>();
        window.titleContent = new GUIContent("SequenceCreator");
        window.minSize = new Vector2(678, 450);
        window.Show();
    }

    private void OnEnable()
    {
        VisualTreeAsset origin = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/ProjectileAnimator/ProjectileAnimator/UIDocuments/UXML/SequenceCreatorUXML.uxml");
        TemplateContainer container = origin.CloneTree();
        rootVisualElement.Add(container);
    }
    private void OnGUI()
    {

    }
}