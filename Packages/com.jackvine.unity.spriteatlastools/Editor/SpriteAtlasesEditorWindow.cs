using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.U2D;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.U2D;

namespace SpriteAtlasTools.Editor
{
    public class SpriteAtlasesEditorWindow : EditorWindow
    {
        private List<string> atlasGuids;

        [MenuItem("Tools/SpriteAtlases Editor")]
        public static void ShowExample()
        {
            var wnd = GetWindow<SpriteAtlasesEditorWindow>();
            wnd.titleContent = new GUIContent("SpriteAtlases Editor");
        }

        private void OnEnable()
        {
            atlasGuids = new List<string>(GetAtlasGuids());
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            var root = rootVisualElement;

            // Import UXML
            var editorWindowAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.jackvine.unity.spriteatlastools/Editor/SpriteAtlasesEditorWindow.uxml");
            editorWindowAsset.CloneTree(root);

            var selectedAtlasViewParent = root.Q<VisualElement>("SelectedAtlasesColumn");

            var atlasViewAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.jackvine.unity.spriteatlastools/Editor/SpriteAtlasView.uxml");

            void ClearAtlasView()
            {
                selectedAtlasViewParent.Clear();
            }

            void ShowSelectedAtlasView(string atlasName, Texture2D texture)
            {
                var atlasView = atlasViewAsset.Instantiate();
                var container = atlasView.Q<TemplateContainer>();
                container.style.flexGrow = new StyleFloat(1);

                var nameLabel = atlasView.Q<Label>("NameLabel");
                nameLabel.text = atlasName;

                var textureView = atlasView.Q<VisualElement>("TextureView");
                textureView.style.backgroundImage = Background.FromTexture2D(texture);

                selectedAtlasViewParent.Add(atlasView);
            }

            var atlasListView = root.Q<ListView>("AtlasListView");
            atlasListView.makeItem = () => new Label();
            atlasListView.bindItem = (element, i) => ((Label)element).text = AssetDatabase.GUIDToAssetPath(atlasGuids[i]);
            atlasListView.itemsSource = atlasGuids;
            atlasListView.onSelectionChange += objects =>
            {
                ClearAtlasView();

                foreach (string guid in objects)
                {
                    string atlasPath = AssetDatabase.GUIDToAssetPath(guid);
                    var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);

                    // Will crash editor if packing happens right now
                    EditorApplication.delayCall += () =>
                    {
                        SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);

                        var getPreviewMethod = typeof(SpriteAtlasExtensions).GetMethod(
                            "GetPreviewTextures",
                            BindingFlags.NonPublic | BindingFlags.Static);

                        var previewTextures = getPreviewMethod!.Invoke(null, new object[] { atlas }) as Texture2D[];
                        if (previewTextures == null || previewTextures.Length == 0)
                            return;

                        // TODO: Multiple preview textures display
                        ShowSelectedAtlasView(atlasPath, previewTextures[0]);
                    };
                }
            };

            void RefreshAtlasList()
            {
                atlasGuids.Clear();
                atlasGuids.AddRange(GetAtlasGuids());
                atlasListView.RefreshItems();
            }

            var refreshButton = root.Q<Button>("RefreshButton");
            refreshButton.clicked += RefreshAtlasList;

            var searchField = root.Q<ToolbarSearchField>("AtlasListSearchField");
            searchField.RegisterValueChangedCallback(
                evt =>
                {
                    Debug.Log("TODO");
                });
        }

        private static IEnumerable<string> GetAtlasGuids()
        {
            return AssetDatabase.FindAssets("t:SpriteAtlas");
        }
    }
}
