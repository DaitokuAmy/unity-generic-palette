using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityGenericPalette {
    /// <summary>
    /// Palette の EntryId 集合と型情報を管理するアセットの基底
    /// </summary>
    public abstract class PaletteAssetBase : ScriptableObject {
        [SerializeField, Tooltip("Palette に含まれる Entry 定義一覧")]
        private List<PaletteEntry> _entries = new();
        [SerializeField, Tooltip("初期化時に適用する既定 Profile の ID")]
        private string _defaultProfileId;
        private Dictionary<string, int> _entryIndexCache;

        /// <summary>Entry 一覧</summary>
        public IReadOnlyList<PaletteEntry> Entries => _entries;
        /// <summary>初期化時に適用する既定 Profile の ID</summary>
        public string DefaultProfileId => _defaultProfileId;

        /// <summary>
        /// 指定した EntryId に対応する index を取得する
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
                $"(EntryCount: {_entries.Count}).");
        }

        /// <summary>
        /// 指定した EntryId に対応する index を取得する
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
        /// EntryId キャッシュを無効化する
        /// </summary>
        public void InvalidateEntryIndexCache() {
            _entryIndexCache = null;
        }

        /// <summary>
        /// EntryId から index を引くキャッシュを初期化する
        /// </summary>
        /// <exception cref="InvalidOperationException">重複した EntryId が存在する場合</exception>
        private void EnsureEntryIndexCache() {
            if (_entryIndexCache != null) {
                return;
            }

            _entryIndexCache = new Dictionary<string, int>(_entries.Count);
            for (var i = 0; i < _entries.Count; i++) {
                var entry = _entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.EntryId)) {
                    continue;
                }

                if (!_entryIndexCache.TryAdd(entry.EntryId, i)) {
                    throw new InvalidOperationException(
                        $"Duplicate EntryId '{entry.EntryId}' was found in {GetType().Name}.");
                }
            }
        }

        /// <summary>
        /// デシリアライズ後のキャッシュを無効化する
        /// </summary>
        private void OnValidate() {
            InvalidateEntryIndexCache();
        }
    }
}
