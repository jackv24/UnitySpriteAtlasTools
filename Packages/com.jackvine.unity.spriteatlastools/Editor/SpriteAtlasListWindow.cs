using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UIElements;

namespace SpriteAtlasTools.Editor
{
    public class SpriteAtlasListWindow : EditorWindow
    {
        private Dictionary<SpriteAtlas, SpriteAtlasEditorWindow> openEditorWindows;

        [MenuItem("Tools/Sprite Atlas Tools/SpriteAtlas List")]
        public static void ShowExample()
        {
            var wnd = GetWindow<SpriteAtlasListWindow>();
            wnd.titleContent = new GUIContent("SpriteAtlas List");
            wnd.openEditorWindows = new Dictionary<SpriteAtlas, SpriteAtlasEditorWindow>();
        }

        private void OnDestroy()
        {
            if (openEditorWindows == null)
                return;

            foreach (var kvp in openEditorWindows)
            {
                var window = kvp.Value;
                if (!window)
                    continue;
                window.Close();
            }
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            var root = rootVisualElement;

            // Import UXML
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.jackvine.unity.spriteatlastools/Editor/SpriteAtlasListWindow.uxml");
            visualTree.CloneTree(root);

            string[] atlasGuids = AssetDatabase.FindAssets("t:SpriteAtlas");

            var totalLabel = root.Q<Label>("AtlasCountLabel");
            totalLabel.text = string.Format(totalLabel.text, atlasGuids.Length);

            var list = root.Q<ListView>("AtlasList");
            list.makeItem = () => new Label();
            list.bindItem = (element, i) => ((Label)element).text = AssetDatabase.GUIDToAssetPath(atlasGuids[i]);
            list.itemsSource = atlasGuids;
            list.onSelectionChange += (objects) =>
            {
                foreach (string guid in objects)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);

                    if (openEditorWindows.TryGetValue(atlas, out var existingWindow))
                    {
                        existingWindow.Focus();
                        continue;
                    }

                    var window = SpriteAtlasEditorWindow.Show(atlas);
                    openEditorWindows[atlas] = window;
                }
            };
        }
    }
}
