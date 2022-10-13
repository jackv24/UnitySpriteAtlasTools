using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpriteAtlasTools.Editor
{
    public class SpriteAtlasEditorWindow : EditorWindow
    {
        [MenuItem("Tools/Sprite Atlas Tools/SpriteAtlas Editor")]
        public static void ShowExample()
        {
            var wnd = GetWindow<SpriteAtlasEditorWindow>();
            wnd.titleContent = new GUIContent("SpriteAtlas Editor");
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            var root = rootVisualElement;

            // Import UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.jackvine.unity.spriteatlastools/Editor/SpriteAtlasEditorWindow.uxml");
            visualTree.CloneTree(root);

            var tex = new Texture2D(50, 50);
            for (int x = 0; x < tex.width; x++)
            {
                for (int y = 0; y < tex.height; y++)
                {
                    tex.SetPixel(x, y, Color.cyan);
                }
            }
            tex.Apply();

            var textureElement = root.Q<VisualElement>("AtlasTexture");
            textureElement.style.backgroundImage = Background.FromTexture2D(tex);
        }
    }
}
