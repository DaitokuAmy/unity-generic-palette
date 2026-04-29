using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGenericPalette {
    /// <summary>
    /// 特定の Palette に対応する値アセットの基底
    /// </summary>
    /// <typeparam name="TPaletteAsset">対応する Palette の型</typeparam>
    /// <typeparam name="TValue">保持する値の型</typeparam>
    public abstract class PaletteProfileAssetBase<TPaletteAsset, TValue> : ScriptableObject, IPaletteProfileAsset
        where TPaletteAsset : PaletteAssetBase {
        [SerializeField, Tooltip("対応する Profile の ID")]
        private string _profileId;
        [SerializeField, Tooltip("Profile 一覧表示用の並び順")]
        private int _sortOrder;
        [SerializeField, Tooltip("対応する Palette アセット")]
        private TPaletteAsset _paletteAsset;
        [SerializeField, Tooltip("EntryId と値を対応付けた一覧")]
        private List<PaletteProfileValue<TValue>> _values = new();
        private Dictionary<string, int> _entryIndexCache;
        
        /// <summary>対応する Profile の ID</summary>
        public string ProfileId => _profileId;
        /// <summary>Profile 一覧表示用の並び順</summary>
        public int SortOrder => _sortOrder;
        /// <summary>対応する Palette アセット</summary>
        public TPaletteAsset PaletteAsset => _paletteAsset;
        /// <summary>対応する Palette アセット</summary>
        public PaletteAssetBase PaletteAssetBase => _paletteAsset;
        /// <summary>保持している値の数</summary>
        public int ValueCount => _values.Count;

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
                    $"(PaletteType: {typeof(TPaletteAsset).Name}, ProfileId: '{_profileId}', ValueCount: {_values.Count}).");
            }

            return _values[index].Value;
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

            if (TryGetEntryIndex(entryId, out var entryIndex)) {
                return entryIndex;
            }

            throw new KeyNotFoundException(
                $"EntryId '{entryId}' was not found in {GetType().Name} " +
                $"(PaletteType: {typeof(TPaletteAsset).Name}, ProfileId: '{_profileId}', ValueCount: {_values.Count}).");
        }

        /// <summary>
        /// 指定した EntryId に対応する index を取得
        /// </summary>
        /// <param name="entryId">取得対象の EntryId</param>
        /// <param name="entryIndex">取得できた index</param>
        /// <returns>取得できた場合は true</returns>
        public bool TryGetEntryIndex(string entryId, out int entryIndex) {
            if (string.IsNullOrEmpty(entryId)) {
                entryIndex = 0;
                return false;
            }

            EnsureEntryIndexCache();
            return _entryIndexCache.TryGetValue(entryId, out entryIndex);
        }

        /// <summary>
        /// EntryId から index を引くキャッシュを初期化する
        /// </summary>
        /// <exception cref="InvalidOperationException">重複した EntryId が存在する場合</exception>
        private void EnsureEntryIndexCache() {
            if (_entryIndexCache != null) {
                return;
            }

            _entryIndexCache = new Dictionary<string, int>(_values.Count);
            for (var i = 0; i < _values.Count; i++) {
                var profileValue = _values[i];
                if (profileValue == null || string.IsNullOrEmpty(profileValue.EntryId)) {
                    continue;
                }

                if (_entryIndexCache.ContainsKey(profileValue.EntryId)) {
                    throw new InvalidOperationException(
                        $"Duplicate EntryId '{profileValue.EntryId}' was found in {GetType().Name} " +
                        $"(PaletteType: {typeof(TPaletteAsset).Name}, ProfileId: '{_profileId}').");
                }

                _entryIndexCache.Add(profileValue.EntryId, i);
            }
        }
    }
}
