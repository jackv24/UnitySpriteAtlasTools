using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UIElements;

namespace SpriteAtlasTools.Editor
{
    public class SpriteAtlasesDuplicatesWindow : EditorWindow
    {
        private readonly List<(string, IReadOnlyList<string>)> matches = new();
        private ListView listView;

        [MenuItem("Tools/SpriteAtlases/Find Duplicates")]
        public static void FindAllDuplicates()
        {
            if (Selection.assetGUIDs.Length == 0)
            {
                if (!EditorUtility.DisplayDialog(
                        "Find duplicates?",
                        "Will scan whole project for duplicate sprites in atlases, this may take a while.",
                        "Yes, scan project",
                        "Cancel"))
                {
                    return;
                }
            }

            var matches = MatchSpritesToAtlases()
                .Where(kvp => kvp.Value.Count > 1)
                .Select(kvp => (kvp.Key, (IReadOnlyList<string>)kvp.Value.ToList()))
                .OrderByDescending(match => match.Item2.Count)
                .ToList();

            if (matches.Count == 0)
            {
                EditorUtility.DisplayDialog("Finished Scanning", "Did not find any duplicates.", "OK");
                return;
            }
            
            var sb = new StringBuilder();
            foreach (var match in matches)
            {
                string spritePath = AssetDatabase.GUIDToAssetPath(match.Item1);
                sb.Append(spritePath);
                sb.Append(':');
                sb.AppendLine();

                foreach (string atlasGuid in match.Item2)
                {
                    string atlasName = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(atlasGuid));

                    sb.Append("\t- ");
                    sb.Append(atlasName);
                    sb.AppendLine();
                }

                sb.AppendLine();
            }

            GUIUtility.systemCopyBuffer = sb.ToString();

            var wnd = ShowNewWindow();
            wnd.matches.Clear();
            wnd.matches.AddRange(matches);
            wnd.listView.RefreshItems();
        }

        private static SpriteAtlasesDuplicatesWindow ShowNewWindow()
        {
            var wnd = GetWindow<SpriteAtlasesDuplicatesWindow>();
            wnd.titleContent = new GUIContent("Duplicates Viewer");
            return wnd;
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            var root = rootVisualElement;

            var sb = new StringBuilder();
            
            listView = new ListView
            {
                makeItem = () => new Label(),
                bindItem = (element, elementIndex) =>
                {
                    var match = matches[elementIndex];

                    string spritePath = AssetDatabase.GUIDToAssetPath(match.Item1);
                    
                    sb.Append(spritePath);
                    sb.Append(" (");

                    for (int i = 0; i < match.Item2.Count; i++)
                    {
                        if (i > 0)
                            sb.Append(", ");
                        
                        string atlasGuid = match.Item2[i];
                        string atlasName = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(atlasGuid));
                        
                        sb.Append(atlasName);
                    }

                    sb.Append(")");

                    ((Label)element).text = sb.ToString();
                    sb.Clear();
                },
                itemsSource = matches,
                focusable = true,
                style = { flexGrow = 1 },
            };

            root.Add(listView);
        }

        private static Dictionary<string, HashSet<string>> MatchSpritesToAtlases()
        {
            const string title = "Finding Duplicates";
            
            EditorUtility.DisplayProgressBar(title, "Gathering sprite atlases", 0);

            // Make sure we have as much memory as possible
            Resources.UnloadUnusedAssets();
            
            string[] atlasPaths = AssetDatabase.FindAssets("t:SpriteAtlas")
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToArray();
            
            EditorUtility.DisplayProgressBar(title, "Gathering sprites", 0);

            IEnumerable<string> spriteGuids;
            string[] selectionGuids = Selection.assetGUIDs;
            if (selectionGuids.Length > 0)
            {
                spriteGuids = selectionGuids;
            }
            else
            {
                spriteGuids = AssetDatabase.FindAssets("t:Sprite")
                    .Where(guid => AssetDatabase.GUIDToAssetPath(guid).StartsWith("Assets"));
            }
            
            // Create dictionary to relate sprites back to their atlases
            var spriteDict = spriteGuids
                .ToDictionary(guid => guid, _ => new HashSet<string>(1));

            for (int i = 0; i < atlasPaths.Length; i++)
            {
                string atlasPath = atlasPaths[i];
                if (EditorUtility.DisplayCancelableProgressBar(
                        title,
                        "Checking atlas: " + atlasPath,
                        i / (float)atlasPaths.Length))
                {
                    break;
                }

                // Get guids of all sprites that will be packed into atlas
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);

                var packedSpriteGuids = Helper.GetSpritesForPackables(atlas.GetPackables())
                    .SelectMany(kvp => kvp.Value)
                    .Select(AssetDatabase.GetAssetPath)
                    .Select(AssetDatabase.AssetPathToGUID);

                string atlasGuid = AssetDatabase.AssetPathToGUID(atlasPath);

                foreach (string packableGuid in packedSpriteGuids)
                {
                    if (!spriteDict.TryGetValue(packableGuid, out var atlasRefList))
                        continue;
                    
                    atlasRefList.Add(atlasGuid);
                }

                Resources.UnloadAsset(atlas);
            }

            EditorUtility.ClearProgressBar();
            Resources.UnloadUnusedAssets();

            return spriteDict;
        }
    }
}
