namespace UnityGenericPalette {
    /// <summary>
    /// ProfileAsset の共通情報を表すインターフェース
    /// </summary>
    public interface IPaletteProfileAsset {
        /// <summary>対応する Profile の ID</summary>
        string ProfileId { get; }
        /// <summary>対応する Palette アセット</summary>
        PaletteAssetBase PaletteAssetBase { get; }
    }
}
