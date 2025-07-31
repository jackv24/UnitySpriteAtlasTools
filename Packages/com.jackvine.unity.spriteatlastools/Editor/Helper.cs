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
    public static class Helper
    {
        public static IReadOnlyDictionary<Object, IEnumerable<Sprite>> GetSpritesForPackables(
            IEnumerable<Object> packables)
        {
            var outDict = new Dictionary<Object, IEnumerable<Sprite>>();

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
                        var sprites = new List<Sprite>();
                        GetPackablesForFolder(assetPath, sprites);
                        outDict[obj] = sprites;
                        break;
                }
            }

            return outDict;
        }

        private static void GetPackablesForFolder(string folderPath, List<Sprite> sprites)
        {
            var folderItems = Directory.GetFiles(folderPath)
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath);

            foreach (var obj in folderItems)
            {
                string assetPath = AssetDatabase.GetAssetPath(obj);

                switch (obj)
                {
                    case Texture2D:
                        var texSprites = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>();
                        sprites.AddRange(texSprites);
                        break;

                    case DefaultAsset:
                        GetPackablesForFolder(assetPath, sprites);
                        break;
                }
            }

            string[] folders = Directory.GetDirectories(folderPath);
            foreach (string folder in folders)
                GetPackablesForFolder(folder, sprites);
        }
    }
}
