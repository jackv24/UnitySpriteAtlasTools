using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SpriteAtlasTools.Editor
{
    public class AssetListItemElement : VisualElement
    {
        // public new class UxmlFactory : UxmlFactory<AssetListItemElement, UxmlTraits> { }

        private readonly bool displayType;
        private readonly Label label;

        private Object assetObject;

        public AssetListItemElement(bool displayType = true)
        {
            this.displayType = displayType;

            label = new Label();
            label.RegisterCallback<MouseDownEvent>(
                evt =>
                {
                    // Only respond to right mouse down
                    if (evt.button != 1)
                        return;

                    string path = AssetDatabase.GetAssetPath(assetObject);
                    if (string.IsNullOrEmpty(path))
                        return;

                    EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(path));
                });
            Add(label);
        }

        public Object AssetObject
        {
            get => assetObject;
            set
            {
                assetObject = value;

                label.text = displayType
                    ? $"{assetObject.name} ({assetObject.GetType().GetTypeInfo().Name})"
                    : $"{assetObject.name}";
            }
        }
    }
}
