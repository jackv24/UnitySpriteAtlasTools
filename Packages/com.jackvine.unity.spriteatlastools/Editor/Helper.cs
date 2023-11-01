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
                        var folderSprites = Directory.GetFiles(assetPath)
                            .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
                            .OfType<Sprite>();
                        outDict[obj] = folderSprites;
                        break;
                }
            }

            return outDict;
        }
    }
}
