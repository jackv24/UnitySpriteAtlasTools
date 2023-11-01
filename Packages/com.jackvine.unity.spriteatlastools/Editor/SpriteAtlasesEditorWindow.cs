using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Sprites;
using UnityEditor.U2D;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace SpriteAtlasTools.Editor
{
    public class SpriteAtlasesEditorWindow : EditorWindow
    {
        private List<string> atlasPaths;
        private string atlasSearchTerm;

        private SpriteAtlas dragFromAtlas;
        private List<Object> dragFromList;

        [MenuItem("Tools/SpriteAtlases/Viewer Window")]
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

            void OnAtlasListViewSelectionChange(IEnumerable<object> objects)
            {
                ClearAtlasView();

                var sorted = objects.OfType<string>().OrderBy(Path.GetFileNameWithoutExtension);

                foreach (string atlasPath in sorted)
                {
                    var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);

                    // Will crash editor if packing happens right now
                    EditorApplication.delayCall += () =>
                    {
                        PackAtlasPreview(atlas, out var previewTextures, out var sprites);
                        ShowSelectedAtlasView(
                            atlas,
                            previewTextures.Length > 0 ? previewTextures[0] : null,
                            sprites ?? Enumerable.Empty<Sprite>());
                    };
                }
            }

            var atlasListView = root.Q<ListView>("AtlasListView");
            atlasListView.makeItem = () => new AssetListItemElement(false);
            atlasListView.bindItem = (element, i) =>
                ((AssetListItemElement)element).AssetObject =
                AssetDatabase.LoadAssetAtPath<Object>(atlasPaths[i]);
            atlasListView.itemsSource = atlasPaths;

            void PackAtlasPreview(SpriteAtlas atlas, out Texture2D[] previewTextures, out Sprite[] sprites)
            {
                SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);

                var getPreviewMethod = typeof(SpriteAtlasExtensions).GetMethod(
                    "GetPreviewTextures",
                    BindingFlags.NonPublic | BindingFlags.Static);

                previewTextures = getPreviewMethod!.Invoke(null, new object[] { atlas }) as Texture2D[];
                if (previewTextures == null || previewTextures.Length == 0)
                {
                    sprites = null;
                    return;
                }

                var getSpritesMethod = typeof(SpriteAtlasExtensions).GetMethod(
                    "GetPackedSprites",
                    BindingFlags.NonPublic | BindingFlags.Static);

                sprites = getSpritesMethod!.Invoke(null, new object[] { atlas }) as Sprite[];
            }

            void ShowSelectedAtlasView(SpriteAtlas atlas, Texture2D atlasTexture, IEnumerable<Sprite> sprites)
            {
                // Create atlas view
                var atlasView = atlasViewAsset.Instantiate();
                var container = atlasView.Q<TemplateContainer>();
                container.style.flexGrow = new StyleFloat(1);

                var nameLabel = atlasView.Q<Label>("NameLabel");
                nameLabel.text = atlas.name;

                var infoLabel = atlasView.Q<Label>("TextureInfoLabel");
                infoLabel.text = atlasTexture
                    ? $"{atlasTexture.width} x {atlasTexture.height} px - {atlasTexture.graphicsFormat}"
                    : "no texture";

                var hoveredSpriteLabel = atlasView.Q<Label>("HoveredSpriteLabel");
                hoveredSpriteLabel.text = null;

                var textureView = atlasView.Q<VisualElement>("TextureView");
                if (atlasTexture)
                    textureView.style.backgroundImage = Background.FromTexture2D(atlasTexture);

                Sprite hoveredSprite = null;
                var selectedPackables = new List<Object>();
                var selectedSprites = new List<Sprite>();

                // Show selected sprites in texture
                textureView.generateVisualContent = context =>
                {
                    var size = context.visualElement.contentRect.size;

                    foreach (var sprite in sprites)
                    {
                        Color? selectedColor;
                        if (hoveredSprite == sprite)
                            selectedColor = new Color(1, 1, 1, 0.2f);
                        else if (selectedSprites.Contains(sprite))
                            selectedColor = new Color(0, 0.5f, 1.0f, 0.15f);
                        else
                            selectedColor = null;

                        if (selectedColor == null)
                            continue;

                        Vector2[] spriteUvs;
                        try
                        {
                            spriteUvs = SpriteUtility.GetSpriteUVs(sprite, true);
                        }
                        catch (Exception e)
                        {
                            //Debug.LogError($"Exception getting UVs for \"{sprite.name}\": {e}");
                            continue;
                        }

                        var vertices = spriteUvs
                            .Select(v => new Vertex
                            {
                                position = new Vector3(v.x * size.x, (1 - v.y) * size.y, Vertex.nearZ),
                                tint = selectedColor.Value,
                            })
                            .ToArray();

                        var mesh = context.Allocate(vertices.Length, sprite.triangles.Length);
                        mesh.SetAllVertices(vertices);
                        mesh.SetAllIndices(sprite.triangles);
                    }
                };

                // Handle mouse over on texture
                textureView.RegisterCallback<MouseMoveEvent>(
                    evt =>
                    {
                        var mousePos = evt.localMousePosition;
                        var viewSize = textureView.localBound.size;

                        // TODO: Account for background scaling within view
                        var mousePosUV = new Vector2(mousePos.x / viewSize.x, mousePos.y / viewSize.y);
                        mousePosUV.y = 1 - mousePosUV.y;

                        //Debug.Log($"mousePos: {mousePos}, viewSize: {viewSize}, mousePosUV: {mousePosUV}");

                        foreach (var sprite in sprites)
                        {
                            Vector2[] spriteUvs;
                            try
                            {
                                spriteUvs = SpriteUtility.GetSpriteUVs(sprite, true);
                            }
                            catch (Exception e)
                            {
                                //Debug.LogError($"Exception getting UVs for \"{sprite.name}\": {e}");
                                continue;
                            }

                            var polygon = ConvexHull.ComputeConvexHull(spriteUvs).ToArray();
                            if (IsPointInPolygon(mousePosUV, polygon))
                            {
                                if (hoveredSprite != sprite)
                                {
                                    hoveredSprite = sprite;
                                    hoveredSpriteLabel.text = hoveredSprite.name;
                                    textureView.MarkDirtyRepaint();
                                }
                            }
                            else
                            {
                                if (hoveredSprite == sprite)
                                {
                                    hoveredSprite = null;
                                    hoveredSpriteLabel.text = null;
                                    textureView.MarkDirtyRepaint();
                                }
                            }
                        }
                    });

                // Setup atlas sprite list
                List<Object> GetPackablesSorted(string searchTerm)
                {
                    IEnumerable<Object> newPackables = atlas.GetPackables();

                    if (!string.IsNullOrWhiteSpace(searchTerm))
                        newPackables = newPackables.Where(obj => obj.name.ToLower().Contains(searchTerm));

                    // Sort packables by name
                    return newPackables
                        .OrderBy(obj => obj.name)
                        .ToList();
                }

                var packables = GetPackablesSorted(null);

                var spriteListView = atlasView.Q<ListView>("SpriteListView");
                spriteListView.makeItem = () => new AssetListItemElement();
                spriteListView.bindItem = (element, i) => ((AssetListItemElement)element).AssetObject = packables[i];
                spriteListView.itemsSource = packables;
                spriteListView.onSelectionChange += objects =>
                {
                    selectedPackables.Clear();
                    selectedPackables.AddRange(objects.OfType<Object>());
                    selectedSprites.Clear();

                    var spriteDict = Helper.GetSpritesForPackables(selectedPackables);
                    var newSprites = spriteDict.SelectMany(kvp => kvp.Value);
                    selectedSprites.AddRange(newSprites);

                    textureView.MarkDirtyRepaint();
                };

                spriteListView.RegisterCallback<PointerDownEvent>(
                    evt =>
                    {
                        dragFromList = selectedPackables;
                        dragFromAtlas = atlas;
                    });

                spriteListView.RegisterCallback<PointerUpEvent>(
                    evt =>
                    {
                        if (atlas == null)
                            return;

                        // Clear drag if dropped on same list
                        if (dragFromAtlas == atlas)
                            return;

                        // Remove selected from old atlas and add to new atlas
                        var add = dragFromList.ToArray();
                        dragFromAtlas.Remove(add);
                        atlas.Add(add);

                        dragFromList = null;
                        dragFromAtlas = null;

                        // Update views
                        ClearAtlasView();
                        OnAtlasListViewSelectionChange(atlasListView.selectedItems);
                    });

                var spriteSearchField = atlasView.Q<ToolbarSearchField>("SpriteSearchField");
                spriteSearchField.RegisterValueChangedCallback(evt =>
                {
                    string searchTerm = evt.newValue.ToLower();
                    packables.Clear();
                    packables.AddRange(GetPackablesSorted(searchTerm));
                    spriteListView.RefreshItems();
                });

                // Resize texture view within container to keep aspect
                if (atlasTexture)
                {
                    var textureContainer = atlasView.Q<VisualElement>("TextureContainer");
                    textureContainer.RegisterCallback<GeometryChangedEvent>(
                        evt =>
                        {
                            var size = evt.newRect.size;

                            float scaleUpX = size.x / atlasTexture.width;
                            float scaleUpY = size.y / atlasTexture.height;

                            float scaleUp = Mathf.Min(scaleUpX, scaleUpY);

                            textureView.style.width = atlasTexture.width * scaleUp;
                            textureView.style.height = atlasTexture.height * scaleUp;

                            // To fix top listview from taking all available space, leaving none for others
                            spriteListView.style.maxHeight = size.y;
                        });
                }

                // Select hovered sprite on mouse down
                textureView.RegisterCallback<MouseDownEvent>(
                    evt =>
                    {
                        // Left button pressed
                        if (evt.button == 0)
                        {
                            if (!hoveredSprite)
                            {
                                selectedSprites.Clear();
                                textureView.MarkDirtyRepaint();
                                spriteListView.ClearSelection();
                                return;
                            }

                            // Only clear selection if not holding control
                            if ((evt.modifiers & EventModifiers.Control) == 0)
                                selectedSprites.Clear();

                            // Toggle inside
                            if (selectedSprites.Contains(hoveredSprite))
                                selectedSprites.Remove(hoveredSprite);
                            else
                                selectedSprites.Add(hoveredSprite);

                            textureView.MarkDirtyRepaint();

                            // Get selected packables from selected sprites
                            var selectedIndices = Helper.GetSpritesForPackables(packables)
                                // Get packables that contain sprites that are selected
                                .Where(kvp => kvp.Value.Any(s => selectedSprites.Contains(s)))
                                .Select(kvp => kvp.Key)
                                .Select(v => packables.IndexOf(v));
                            spriteListView.SetSelection(selectedIndices);
                        }
                        // Right button pressed
                        else if (evt.button == 1)
                        {
                            // Get packable from hovered sprite
                            var hoveredPackable = Helper.GetSpritesForPackables(packables)
                                .First(kvp => kvp.Value.Any(s => s == hoveredSprite))
                                .Key;

                            string path = AssetDatabase.GetAssetPath(hoveredPackable);
                            if (string.IsNullOrEmpty(path))
                                return;

                            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(path));
                        }
                    });

                selectedAtlasViewParent.Add(atlasView);
            }

            atlasListView.onSelectionChange += OnAtlasListViewSelectionChange;

            void RefreshAtlasList()
            {
                atlasPaths.Clear();
                atlasPaths.AddRange(GetAtlasPaths());
                atlasListView.RefreshItems();
            }

            var refreshButton = root.Q<Button>("RefreshButton");
            refreshButton.clicked += RefreshAtlasList;

            var atlasSearchField = root.Q<ToolbarSearchField>("AtlasListSearchField");
            atlasSearchField.SetValueWithoutNotify(atlasSearchTerm);
            atlasSearchField.RegisterValueChangedCallback(evt =>
            {
                atlasSearchTerm = evt.newValue;
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

                    if (string.IsNullOrWhiteSpace(atlasSearchTerm))
                        return true;

                    string fileName = Path.GetFileNameWithoutExtension(path);
                    return fileName.ToLower().Contains(atlasSearchTerm.ToLower());
                });
        }
        
        // From: https://codereview.stackexchange.com/questions/108857/point-inside-polygon-check
        private static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
        {
            int polygonLength = polygon.Length, i = 0;

            if (polygonLength <= 0)
            {
                Debug.LogError("Polygon point array has length 0!");
                return false;
            }

            bool inside = false;

            // x, y for tested point.
            float pointX = point.x, pointY = point.y;

            // start / end point for the current polygon segment.
            var endPoint = polygon[polygonLength - 1];
            float endX = endPoint.x;
            float endY = endPoint.y;
            while (i < polygonLength)
            {
                float startX = endX;
                float startY = endY;
                endPoint = polygon[i++];
                endX = endPoint.x;
                endY = endPoint.y;

                inside ^= (endY > pointY ^ startY > pointY) /* ? pointY inside [startY;endY] segment ? */
                          && /* if so, test if it is under the segment */
                          ((pointX - endX) < (pointY - endY) * (startX - endX) / (startY - endY));
            }

            return inside;
        }
    }
}
