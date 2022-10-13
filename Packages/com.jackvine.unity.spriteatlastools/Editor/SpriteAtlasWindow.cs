using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpriteAtlasTools.Editor
{
    public class SpriteAtlasWindow : EditorWindow
    {
        [MenuItem("Tools/Sprite Atlas Tools/SpriteAtlas Window")]
        public static void ShowExample()
        {
            var wnd = GetWindow<SpriteAtlasWindow>();
            wnd.titleContent = new GUIContent("SpriteAtlas Window");
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            var root = rootVisualElement;

            // Import UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.jackvine.unity.spriteatlastools/Editor/SpriteAtlasWindow.uxml");
            VisualElement rootElement = visualTree.Instantiate();
            root.Add(rootElement);
        }
    }
}
