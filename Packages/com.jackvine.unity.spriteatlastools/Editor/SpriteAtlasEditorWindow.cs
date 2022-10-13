using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UIElements;

namespace SpriteAtlasTools.Editor
{
    public class SpriteAtlasEditorWindow : EditorWindow
    {
        public static SpriteAtlasEditorWindow Show(SpriteAtlas atlas)
        {
            var wnd = CreateInstance<SpriteAtlasEditorWindow>();
            wnd.titleContent = new GUIContent("SpriteAtlas Editor");
            wnd.atlas = atlas;
            wnd.Show();
            return wnd;
        }

        private SpriteAtlas atlas;

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            var root = rootVisualElement;

            // Import UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.jackvine.unity.spriteatlastools/Editor/SpriteAtlasEditorWindow.uxml");
            visualTree.CloneTree(root);

            if (!atlas)
                return;

            var atlasName = root.Q<Label>("SpriteAtlasName");
            atlasName.text = atlas.name;

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
