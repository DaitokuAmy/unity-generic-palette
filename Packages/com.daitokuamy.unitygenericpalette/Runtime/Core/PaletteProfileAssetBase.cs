using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGenericPalette {
    /// <summary>
    /// すべての ProfileAsset が持つ共通情報を表す基底
    /// </summary>
    public abstract class PaletteProfileAssetBase : ScriptableObject {
        [SerializeField, Tooltip("対応する Profile の ID")]
        private string _profileId;
        [SerializeField, Tooltip("Profile 一覧表示用の並び順")]
        private int _sortOrder;
        [SerializeField, Tooltip("対応する PaletteAsset の GUID")]
        private string _paletteGuid;
        [SerializeField, Tooltip("対応する PaletteAsset の Local File ID")]
        private long _paletteLocalFileId;

        /// <summary>対応する Profile の ID</summary>
        public string ProfileId => _profileId;
        /// <summary>Profile 一覧表示用の並び順</summary>
        public int SortOrder => _sortOrder;
        /// <summary>対応する PaletteAsset の GUID</summary>
        public string PaletteGuid => _paletteGuid;
        /// <summary>対応する PaletteAsset の Local File ID</summary>
        public long PaletteLocalFileId => _paletteLocalFileId;
        /// <summary>内部キャッシュを無効化する</summary>
        public abstract void InvalidateCache();
    }

    /// <summary>
    /// 特定の Palette に対応する値アセットの基底
    /// </summary>
    /// <typeparam name="TPaletteAsset">対応する Palette の型</typeparam>
    /// <typeparam name="TValue">保持する値の型</typeparam>
    public abstract class PaletteProfileAssetBase<TPaletteAsset, TValue> : PaletteProfileAssetBase, ISerializationCallbackReceiver
        where TPaletteAsset : PaletteAssetBase {
        [SerializeField, Tooltip("Palette の Entry 順に対応した値一覧")]
        private List<TValue> _values = new();
        
        /// <summary>保持している値の数</summary>
        public int ValueCount => _values.Count;

        /// <inheritdoc/>
        void ISerializationCallbackReceiver.OnBeforeSerialize() {
        }

        /// <inheritdoc/>
        void ISerializationCallbackReceiver.OnAfterDeserialize() {
            InvalidateCache();
        }
        
        /// <inheritdoc/>
        public override void InvalidateCache() {
        }

        /// <summary>
        /// 指定した index に対応する値を取得
        /// </summary>
        /// <param name="index">取得対象の index</param>
        /// <returns>対応する値</returns>
        /// <exception cref="ArgumentOutOfRangeException">index が範囲外の場合</exception>
        public TValue GetValueByIndex(int index) {
            if (index < 0 || index >= _values.Count) {
                throw new ArgumentOutOfRangeException(
                    nameof(index),
                    index,
                    $"Index is out of range in {GetType().Name} " +
                    $"(PaletteType: {typeof(TPaletteAsset).Name}, ProfileId: '{ProfileId}', ValueCount: {_values.Count}).");
            }

            return _values[index];
        }

        /// <summary>
        /// Inspector 更新時に内部キャッシュを無効化する
        /// </summary>
        private void OnValidate() {
            InvalidateCache();
        }
    }
}
