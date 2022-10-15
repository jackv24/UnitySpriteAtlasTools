using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Sprites;
using UnityEditor.U2D;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.U2D;

namespace SpriteAtlasTools.Editor
{
    public class SpriteAtlasesEditorWindow : EditorWindow
    {
        private List<string> atlasPaths;
        private string searchTerm;

        [MenuItem("Tools/SpriteAtlases Editor")]
        public static void ShowExample()
        {
            var wnd = GetWindow<SpriteAtlasesEditorWindow>();
            wnd.titleContent = new GUIContent("SpriteAtlases Editor");
        }

        private void OnEnable()
        {
            atlasPaths = new List<string>(GetAtlasPaths());
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

            void ShowSelectedAtlasView(string atlasName, Texture2D texture, IEnumerable<Sprite> sprites)
            {
                // Create atlas view
                var atlasView = atlasViewAsset.Instantiate();
                var container = atlasView.Q<TemplateContainer>();
                container.style.flexGrow = new StyleFloat(1);

                var nameLabel = atlasView.Q<Label>("NameLabel");
                nameLabel.text = atlasName;

                var textureView = atlasView.Q<VisualElement>("TextureView");
                textureView.style.backgroundImage = Background.FromTexture2D(texture);

                Sprite selectedSprite = null;

                textureView.generateVisualContent = context =>
                {
                    var size = context.visualElement.contentRect.size;

                    foreach (var sprite in sprites)
                    {
                        var color = selectedSprite == sprite ? Color.magenta : Color.blue;
                        color.a = 0.2f;

                        Vector2[] spriteUvs;
                        try
                        {
                            spriteUvs = SpriteUtility.GetSpriteUVs(sprite, true);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"Exception getting UVs for \"{sprite.name}\": {e}");
                            continue;
                        }

                        var vertices = spriteUvs
                            .Select(v => new Vertex
                            {
                                position = new Vector3(v.x * size.x, (1 - v.y) * size.y, Vertex.nearZ),
                                tint = color,
                            })
                            .ToArray();

                        var mesh = context.Allocate(vertices.Length, sprite.triangles.Length);
                        mesh.SetAllVertices(vertices);
                        mesh.SetAllIndices(sprite.triangles);
                    }
                };

                // Handle mouse over on texture
                textureView.RegisterCallback<MouseMoveEvent>(evt =>
                {
                    var mousePos = evt.localMousePosition;
                    var viewSize = textureView.localBound.size;

                    // TODO: Account for background scaling within view
                    var mousePosUV = new Vector2(
                        mousePos.x / viewSize.x,
                        mousePos.y / viewSize.y);

                    //Debug.Log($"mousePos: {mousePos}, viewSize: {viewSize}, mousePosUV: {mousePosUV}");

                    foreach (var sprite in sprites)
                    {
                        if (!IsPointInPolygon(mousePosUV, sprite.uv))
                        {
                            if (selectedSprite == sprite)
                            {
                                selectedSprite = null;
                                textureView.MarkDirtyRepaint();
                            }
                        }
                        else if (selectedSprite != sprite)
                        {
                            selectedSprite = sprite;
                            textureView.MarkDirtyRepaint();
                        }
                        //Debug.Log($"Mouse pos: {mousePos}, UV: {mousePosUV}");
                        //Debug.Log($"Mouse inside sprite: {sprite.name}");
                    }
                });

                selectedAtlasViewParent.Add(atlasView);
            }

            var atlasListView = root.Q<ListView>("AtlasListView");
            atlasListView.makeItem = () => new Label();
            atlasListView.bindItem = (element, i) => ((Label)element).text = atlasPaths[i];
            atlasListView.itemsSource = atlasPaths;
            atlasListView.onSelectionChange += objects =>
            {
                ClearAtlasView();

                foreach (string atlasPath in objects)
                {
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

                        var getSpritesMethod = typeof(SpriteAtlasExtensions).GetMethod(
                            "GetPackedSprites",
                            BindingFlags.NonPublic | BindingFlags.Static);

                        var sprites = getSpritesMethod!.Invoke(null, new object[] { atlas }) as Sprite[];

                        // TODO: Multiple preview textures display
                        ShowSelectedAtlasView(atlasPath, previewTextures[0], sprites);
                    };
                }
            };

            void RefreshAtlasList()
            {
                atlasPaths.Clear();
                atlasPaths.AddRange(GetAtlasPaths());
                atlasListView.RefreshItems();
            }

            var refreshButton = root.Q<Button>("RefreshButton");
            refreshButton.clicked += RefreshAtlasList;

            var searchField = root.Q<ToolbarSearchField>("AtlasListSearchField");
            searchField.RegisterValueChangedCallback(evt =>
            {
                searchTerm = evt.newValue;
                RefreshAtlasList();
            });
        }

        private IEnumerable<string> GetAtlasPaths()
        {
            // Get atlas paths that match search term
            return AssetDatabase.FindAssets("t:SpriteAtlas")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path =>
                {
                    if (string.IsNullOrEmpty(path))
                        return false;

                    if (string.IsNullOrWhiteSpace(searchTerm))
                        return true;

                    return path.ToLower().Contains(searchTerm.ToLower());
                });
        }

        // From: https://codereview.stackexchange.com/questions/108857/point-inside-polygon-check
        private static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
        {
            // TODO: We need to get outer polygon from sprite mesh
            return false;

            // int polygonLength = polygon.Length, i = 0;
            // bool inside = false;
            // // x, y for tested point.
            // float pointX = point.x, pointY = point.y;
            // // start / end point for the current polygon segment.
            // float startX, startY, endX, endY;
            // Vector2 endPoint = polygon[polygonLength - 1];
            // endX = endPoint.x;
            // endY = endPoint.y;
            // while (i < polygonLength)
            // {
            //     startX = endX;
            //     startY = endY;
            //     endPoint = polygon[i++];
            //     endX = endPoint.x;
            //     endY = endPoint.y;
            //     //
            //     inside ^= (endY > pointY ^ startY > pointY) /* ? pointY inside [startY;endY] segment ? */
            //               && /* if so, test if it is under the segment */
            //               ((pointX - endX) < (pointY - endY) * (startX - endX) / (startY - endY));
            // }
            //
            // return inside;
        }
    }
}
