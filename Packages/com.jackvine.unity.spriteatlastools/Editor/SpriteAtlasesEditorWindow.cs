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
using UnityEngine.UIElements;
using UnityEngine.U2D;

namespace SpriteAtlasTools.Editor
{
    public class SpriteAtlasesEditorWindow : EditorWindow
    {
        private List<string> atlasPaths;
        private string atlasSearchTerm;

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

            void ShowSelectedAtlasView(SpriteAtlas atlas, Texture2D atlasTexture, IEnumerable<Sprite> sprites)
            {
                // Create atlas view
                var atlasView = atlasViewAsset.Instantiate();
                var container = atlasView.Q<TemplateContainer>();
                container.style.flexGrow = new StyleFloat(1);

                var nameLabel = atlasView.Q<Label>("NameLabel");
                nameLabel.text = atlas.name;

                var textureView = atlasView.Q<VisualElement>("TextureView");
                textureView.style.backgroundImage = Background.FromTexture2D(atlasTexture);

                Sprite hoveredSprite = null;
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
                            Debug.LogError($"Exception getting UVs for \"{sprite.name}\": {e}");
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
                                Debug.LogError($"Exception getting UVs for \"{sprite.name}\": {e}");
                                continue;
                            }

                            var polygons = PolygonsFromSpriteMesh(spriteUvs, sprite.triangles);

                            foreach (var polygon in polygons)
                            {
                                if (IsPointInPolygon(mousePosUV, polygon))
                                {
                                    if (hoveredSprite != sprite)
                                    {
                                        hoveredSprite = sprite;
                                        textureView.MarkDirtyRepaint();
                                    }
                                }
                                else
                                {
                                    if (hoveredSprite == sprite)
                                    {
                                        hoveredSprite = null;
                                        textureView.MarkDirtyRepaint();
                                    }
                                }
                            }
                        }
                    });

                // Resize texture view within container to keep aspect
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
                    });

                // Setup atlas sprite list
                List<UnityEngine.Object> GetPackablesSorted(string searchTerm)
                {
                    IEnumerable<UnityEngine.Object> newPackables = atlas.GetPackables();

                    if (!string.IsNullOrWhiteSpace(searchTerm))
                        newPackables = newPackables.Where(obj => obj.name.ToLower().Contains(searchTerm));

                    // Sort packables by name
                    return newPackables
                        .OrderBy(obj => obj.name)
                        .ToList();
                }

                var packables = GetPackablesSorted(null);

                var spriteListView = atlasView.Q<ListView>("SpriteListView");
                spriteListView.makeItem = () => new Label();
                spriteListView.bindItem = (element, i) =>
                {
                    var packable = packables[i];
                    ((Label)element).text = $"{packable.name} ({packable.GetType().GetTypeInfo().Name})";
                };
                spriteListView.itemsSource = packables;
                spriteListView.onSelectionChange += objects =>
                {
                    selectedSprites.Clear();

                    var spriteDict = GetSpritesForPackables(objects.OfType<UnityEngine.Object>());
                    var newSprites = spriteDict.SelectMany(kvp => kvp.Value);
                    selectedSprites.AddRange(newSprites);

                    textureView.MarkDirtyRepaint();
                };

                var spriteSearchField = atlasView.Q<ToolbarSearchField>("SpriteSearchField");
                spriteSearchField.RegisterValueChangedCallback(evt =>
                {
                    string searchTerm = evt.newValue.ToLower();
                    packables.Clear();
                    packables.AddRange(GetPackablesSorted(searchTerm));
                    spriteListView.RefreshItems();
                });

                // Select hovered sprite on mouse down
                textureView.RegisterCallback<MouseDownEvent>(
                    evt =>
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
                        var selectedIndices = GetSpritesForPackables(packables)
                            // Get packables that contain sprites that are selected
                            .Where(kvp => kvp.Value.Any(s => selectedSprites.Contains(s)))
                            .Select(kvp => kvp.Key)
                            .Select(v => packables.IndexOf(v));
                        spriteListView.SetSelection(selectedIndices);
                    });

                selectedAtlasViewParent.Add(atlasView);
            }

            var atlasListView = root.Q<ListView>("AtlasListView");
            atlasListView.makeItem = () => new Label();
            atlasListView.bindItem = (element, i) =>
                ((Label)element).text = Path.GetFileNameWithoutExtension(atlasPaths[i]);
            atlasListView.itemsSource = atlasPaths;
            atlasListView.onSelectionChange += objects =>
            {
                ClearAtlasView();

                var sorted = objects.OfType<string>().OrderBy(Path.GetFileNameWithoutExtension);

                foreach (string atlasPath in sorted)
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
                        ShowSelectedAtlasView(atlas, previewTextures[0], sprites);
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

            var atlasSearchField = root.Q<ToolbarSearchField>("AtlasListSearchField");
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

        private static IReadOnlyDictionary<UnityEngine.Object, IEnumerable<Sprite>> GetSpritesForPackables(
            IEnumerable<UnityEngine.Object> packables)
        {
            var outDict = new Dictionary<UnityEngine.Object, IEnumerable<Sprite>>();

            // Selecting textures can show in atlas texture view
            foreach (var obj in packables)
            {
                string assetPath = AssetDatabase.GetAssetPath(obj);

                switch (obj)
                {
                    case Texture2D:
                        var texSprites = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>();
                        outDict[obj] = texSprites;
                        break;

                    case DefaultAsset:
                        var folderSprites = Directory.GetFiles(assetPath)
                            .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                            .OfType<Sprite>();
                        outDict[obj] = folderSprites;
                        break;
                }
            }

            return outDict;
        }

        // From: https://codereview.stackexchange.com/questions/108857/point-inside-polygon-check
        private static bool IsPointInPolygon(Vector2 point, Vector2[] polygon)
        {
            int polygonLength = polygon.Length, i = 0;
            bool inside = false;
            // x, y for tested point.
            float pointX = point.x, pointY = point.y;
            // start / end point for the current polygon segment.
            float startX, startY, endX, endY;
            var endPoint = polygon[polygonLength - 1];
            endX = endPoint.x;
            endY = endPoint.y;
            while (i < polygonLength)
            {
                startX = endX;
                startY = endY;
                endPoint = polygon[i++];
                endX = endPoint.x;
                endY = endPoint.y;
                //
                inside ^= (endY > pointY ^ startY > pointY) /* ? pointY inside [startY;endY] segment ? */
                          && /* if so, test if it is under the segment */
                          ((pointX - endX) < (pointY - endY) * (startX - endX) / (startY - endY));
            }

            return inside;
        }

        // Edited from: https://www.h3xed.com/programming/automatically-create-polygon-collider-2d-from-2d-mesh-in-unity
        private static Vector2[][] PolygonsFromSpriteMesh(Vector2[] vertices, ushort[] triangles)
        {
            // Get just the outer edges from the mesh's triangles (ignore or remove any shared edges)
            var edges = new Dictionary<string, KeyValuePair<int, int>>();
            for (int i = 0; i < triangles.Length; i += 3)
            {
                for (int e = 0; e < 3; e++)
                {
                    ushort vert1 = triangles[i + e];
                    ushort vert2 = triangles[i + e + 1 > i + 2 ? i : i + e + 1];
                    string edge = Mathf.Min(vert1, vert2) + ":" + Mathf.Max(vert1, vert2);
                    if (edges.ContainsKey(edge))
                    {
                        edges.Remove(edge);
                    }
                    else
                    {
                        edges.Add(edge, new KeyValuePair<int, int>(vert1, vert2));
                    }
                }
            }

            // Create edge lookup (Key is first vertex, Value is second vertex, of each edge)
            var lookup = new Dictionary<int, int>();
            foreach ((int key, int value) in edges.Values)
            {
                if (lookup.ContainsKey(key) == false)
                    lookup.Add(key, value);
            }

            var currentPaths = new List<Vector2[]>();

            // Loop through edge vertices in order
            int startVert = 0;
            int nextVert = startVert;
            int highestVert = startVert;
            var colliderPath = new List<Vector2>();
            while (true)
            {
                // Add vertex to collider path
                colliderPath.Add(vertices[nextVert]);

                // Get next vertex
                nextVert = lookup[nextVert];

                // Store highest vertex (to know what shape to move to next)
                if (nextVert > highestVert)
                {
                    highestVert = nextVert;
                }

                // Shape complete
                if (nextVert == startVert)
                {
                    // Add path to polygon collider
                    currentPaths.Add(colliderPath.ToArray());
                    colliderPath.Clear();

                    // Go to next shape if one exists
                    if (lookup.ContainsKey(highestVert + 1))
                    {
                        // Set starting and next vertices
                        startVert = highestVert + 1;
                        nextVert = startVert;

                        // Continue to next loop
                        continue;
                    }

                    // No more verts
                    break;
                }
            }

            return currentPaths.ToArray();
        }
    }
}
