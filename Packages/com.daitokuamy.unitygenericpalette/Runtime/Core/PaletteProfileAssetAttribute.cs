using System;

namespace UnityGenericPalette {
    /// <summary>
    /// PaletteAsset に対応する ProfileAsset 型を表す属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class PaletteProfileAssetAttribute : Attribute {
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="profileAssetType">対応する ProfileAsset の型</param>
        /// <exception cref="ArgumentNullException">profileAssetType が null の場合</exception>
        /// <exception cref="ArgumentException">profileAssetType が ScriptableObject ではない場合</exception>
        public PaletteProfileAssetAttribute(Type profileAssetType) {
            if (profileAssetType == null) {
                throw new ArgumentNullException(nameof(profileAssetType));
            }

            if (!typeof(PaletteProfileAssetBase).IsAssignableFrom(profileAssetType)) {
                throw new ArgumentException("Profile asset type must inherit from PaletteProfileAssetBase.", nameof(profileAssetType));
            }

            ProfileAssetType = profileAssetType;
        }

        /// <summary>対応する ProfileAsset の型</summary>
        public Type ProfileAssetType { get; }
    }
}
