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

        /// <summary>対応する Profile の ID</summary>
        public string ProfileId => _profileId;
        /// <summary>Profile 一覧表示用の並び順</summary>
        public int SortOrder => _sortOrder;
        /// <summary>対応する Palette アセット</summary>
        public abstract PaletteAssetBase PaletteAssetBase { get; }
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
        [SerializeField, Tooltip("対応する Palette アセット")]
        private TPaletteAsset _paletteAsset;
        [SerializeField, Tooltip("Palette の Entry 順に対応した値一覧")]
        private List<TValue> _values = new();
        
        /// <summary>対応する Palette アセット</summary>
        public TPaletteAsset PaletteAsset => _paletteAsset;
        /// <summary>対応する Palette アセット</summary>
        public override PaletteAssetBase PaletteAssetBase => _paletteAsset;
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
        /// 指定した EntryId に対応する値を取得
        /// </summary>
        /// <param name="entryId">取得対象の EntryId</param>
        /// <returns>対応する値</returns>
        /// <exception cref="KeyNotFoundException">対応する値が存在しない場合</exception>
        public TValue GetValueById(string entryId) {
            var entryIndex = GetEntryIndex(entryId);
            return GetValueByIndex(entryIndex);
        }

        /// <summary>
        /// 指定した EntryId に対応する値を取得する
        /// </summary>
        /// <param name="entryId">取得対象の EntryId</param>
        /// <param name="value">取得できた値</param>
        /// <returns>取得できた場合は true</returns>
        public bool TryGetValueById(string entryId, out TValue value) {
            if (!TryGetEntryIndex(entryId, out var entryIndex)) {
                value = default;
                return false;
            }

            value = GetValueByIndex(entryIndex);
            return true;
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
        /// 指定した EntryId に対応する index を取得
        /// </summary>
        /// <param name="entryId">取得対象の EntryId</param>
        /// <returns>対応する index</returns>
        /// <exception cref="ArgumentException">entryId が null または空文字の場合</exception>
        /// <exception cref="KeyNotFoundException">対応する EntryId が存在しない場合</exception>
        public int GetEntryIndex(string entryId) {
            if (string.IsNullOrEmpty(entryId)) {
                throw new ArgumentException("Entry ID must not be null or empty.", nameof(entryId));
            }

            if (_paletteAsset == null) {
                throw new InvalidOperationException(
                    $"{GetType().Name} has no PaletteAsset reference " +
                    $"(PaletteType: {typeof(TPaletteAsset).Name}, ProfileId: '{ProfileId}').");
            }

            try {
                return _paletteAsset.GetEntryIndex(entryId);
            }
            catch (KeyNotFoundException exception) {
                throw new KeyNotFoundException(
                    $"EntryId '{entryId}' was not found in {GetType().Name} " +
                    $"(PaletteType: {typeof(TPaletteAsset).Name}, ProfileId: '{ProfileId}', ValueCount: {_values.Count}).",
                    exception);
            }
        }

        /// <summary>
        /// 指定した EntryId に対応する index を取得
        /// </summary>
        /// <param name="entryId">取得対象の EntryId</param>
        /// <param name="entryIndex">取得できた index</param>
        /// <returns>取得できた場合は true</returns>
        public bool TryGetEntryIndex(string entryId, out int entryIndex) {
            if (_paletteAsset == null) {
                entryIndex = 0;
                return false;
            }

            return _paletteAsset.TryGetEntryIndex(entryId, out entryIndex);
        }

        /// <summary>
        /// Inspector 更新時に内部キャッシュを無効化する
        /// </summary>
        private void OnValidate() {
            InvalidateCache();
        }
    }
}
