# Unity Enhanced Gesture

`Unity Enhanced Gesture` は、Unity Input System の `EnhancedTouch` をベースにしたモバイル向けジェスチャーライブラリです。  
uGUI の `RectTransform` と 3D の `Collider` を対象に、ドラッグ、タップ、ピンチをイベント駆動で扱えます。

このリポジトリは、Unity プロジェクト本体と UPM パッケージを同梱しています。実際のパッケージ本体は `Packages/com.daitokuamy.unityenhancedgesture` にあります。

## Features

- `Drag` / `Tap` / `Pinch` を同じ運用で扱える
- `RectTransform` 向け `UI` ハンドラーと `Collider` 向け `3D` ハンドラーを用意
- `System.Action<TEvent>` ベースで購読でき、`UnityEvent` に依存しない
- `GestureCoordinator` が入力収集とハンドラー振り分けを一元管理
- ハンドラーの `Priority` による競合解決に対応
- `Tap` は `SingleTap` / `DoubleTap` / `LongTap` をサポート
- `Drag` は通常ドラッグに加えて `LongTapDrag` 開始にも対応
- `Pinch` は距離だけでなく中心座標と角度差分も取得可能
- Unity Editor 上ではマウス入力で基本動作を確認できる
- Unity Editor 上では `Alt + Drag` でピンチ操作をシミュレートできる

## Requirements

- Unity `6000.0` 以降
- Unity Input System
- uGUI

パッケージの `package.json` では以下に依存しています。

- `com.unity.inputsystem` `1.14.2`
- `com.unity.ugui` `2.0.0`

## Installation

### Install via Package Manager

Unity の `Window > Package Manager` を開き、`Add package from git URL...` から次を指定します。

```text
https://github.com/DaitokuAmy/unity-enhanced-gesture.git?path=/Packages/com.daitokuamy.unityenhancedgesture
```

タグを固定したい場合は末尾にバージョンを付けてください。

```text
https://github.com/DaitokuAmy/unity-enhanced-gesture.git?path=/Packages/com.daitokuamy.unityenhancedgesture#0.9.0
```

### Install via manifest.json

`Packages/manifest.json` の `dependencies` に追加する場合は次のように設定します。

```json
{
  "dependencies": {
    "com.daitokuamy.unityenhancedgesture": "https://github.com/DaitokuAmy/unity-enhanced-gesture.git?path=/Packages/com.daitokuamy.unityenhancedgesture"
  }
}
```

## Quick Start

### 1. `GestureCoordinator` をシーンに配置

最初にシーンへ `GestureCoordinator` を 1 つ配置します。

- `Input Management Mode`
  - `Automatic`: `EnhancedTouchSupport` の有効化を `GestureCoordinator` が管理
  - `External`: 既存システム側で `EnhancedTouchSupport.Enable()` を管理
- `Update Mode`
  - `Update`: `MonoBehaviour.Update()` で自動更新
  - `ManualUpdate`: 任意のタイミングで `ManualUpdate()` を呼ぶ
- `Event Camera`
  - `3D` ハンドラーでスクリーン座標からレイを飛ばすときに使用

`GestureCoordinator` は複数配置に対応していません。

### 2. 対象オブジェクトにハンドラーを追加

用途に応じて対象へハンドラーを追加します。

#### UI 向け

- `DragGestureHandlerUI`
- `TapGestureHandlerUI`
- `PinchGestureHandlerUI`

それぞれ対象の `RectTransform` を設定します。

#### 3D 向け

- `DragGestureHandler3D`
- `TapGestureHandler3D`
- `PinchGestureHandler3D`

それぞれ対象の `Collider` を設定します。`3D` ハンドラーを使う場合は、`GestureCoordinator` の `Event Camera` も設定してください。

### 3. スクリプトからイベントを購読

以下は `DragGestureHandlerUI` を使った最小例です。

```csharp
using UnityEngine;
using UnityEnhancedGesture;

public sealed class DragExample : MonoBehaviour {
    [SerializeField] private DragGestureHandlerUI _dragGestureHandler;

    private void OnEnable() {
        if (_dragGestureHandler == null) {
            return;
        }

        _dragGestureHandler.BeginDragEvent += OnBeginDrag;
        _dragGestureHandler.DragEvent += OnDrag;
        _dragGestureHandler.EndDragEvent += OnEndDrag;
        _dragGestureHandler.CancelDragEvent += OnCancelDrag;
    }

    private void OnDisable() {
        if (_dragGestureHandler == null) {
            return;
        }

        _dragGestureHandler.BeginDragEvent -= OnBeginDrag;
        _dragGestureHandler.DragEvent -= OnDrag;
        _dragGestureHandler.EndDragEvent -= OnEndDrag;
        _dragGestureHandler.CancelDragEvent -= OnCancelDrag;
    }

    private void OnBeginDrag(DragGestureEvent gestureEvent) {
        Debug.Log($"Begin: {gestureEvent.StartPosition}");
    }

    private void OnDrag(DragGestureEvent gestureEvent) {
        Debug.Log($"Delta: {gestureEvent.Delta}, Total: {gestureEvent.TotalDelta}");
    }

    private void OnEndDrag(DragGestureEvent gestureEvent) {
        Debug.Log($"End: {gestureEvent.Position}");
    }

    private void OnCancelDrag(DragGestureEvent gestureEvent) {
        Debug.Log("Canceled");
    }
}
```

`ManualUpdate` を使う場合は、任意の更新ループから次のように呼びます。

```csharp
using UnityEngine;
using UnityEnhancedGesture;

public sealed class GestureCoordinatorDriver : MonoBehaviour {
    [SerializeField] private GestureCoordinator _gestureCoordinator;

    private void Update() {
        if (_gestureCoordinator == null) {
            return;
        }

        _gestureCoordinator.ManualUpdate();
    }
}
```

## Supported Gestures

### Drag

ドラッグ系は `BeginDragEvent` / `DragEvent` / `EndDragEvent` / `CancelDragEvent` を購読します。

主な設定項目:

- `DragStartThreshold`
- `EnableLongTapDrag`
- `LongTapDragDuration`
- `LongTapDragMaxMovement`
- `Priority`

`DragGestureEvent` から主に次を取得できます。

- `StartMode`
  - `Immediate`
  - `LongTap`
- `StartPosition`
- `Position`
- `Delta`
- `TotalDelta`
- `Samples`
- `Duration`
- `ActivePointerCount`
- `EventCamera`

### Tap

タップ系は `TapEvent` / `DoubleTapEvent` / `LongTapEvent` を購読します。

主な設定項目:

- `MaxTapDuration`
- `MaxTapMovement`
- `EnableDoubleTap`
- `DoubleTapMaxDelay`
- `DoubleTapMaxMovement`
- `EnableLongTap`
- `LongTapDuration`
- `LongTapMaxMovement`
- `Priority`

`TapGestureEvent` から主に次を取得できます。

- `Type`
  - `SingleTap`
  - `DoubleTap`
  - `LongTap`
- `TapCount`
- `FirstTapPosition`
- `StartPosition`
- `Position`
- `Samples`
- `Duration`
- `Interval`
- `EventCamera`

注意点:

- `EnableDoubleTap` が有効な場合、単一タップ通知は即時ではなくダブルタップ待機後に確定します

### Pinch

ピンチ系は `BeginPinchEvent` / `PinchEvent` / `EndPinchEvent` / `CancelPinchEvent` を購読します。

主な設定項目:

- `PinchStartThreshold`
- `Priority`

`PinchGestureEvent` から主に次を取得できます。

- `StartCenter`
- `Center`
- `CenterDelta`
- `StartDistance`
- `Distance`
- `DeltaDistance`
- `Scale`
- `StartAngle`
- `Angle`
- `DeltaAngle`
- `TotalAngleDelta`
- `FirstPosition`
- `SecondPosition`
- `Duration`
- `EventCamera`

## Priority と競合

開始位置に対して複数のハンドラーが反応可能な場合は、`Priority` が高いハンドラーが優先されます。  
同じオブジェクトに複数のジェスチャー種別を共存させることはできますが、同じ種別で重なる構成では `Priority` を明示しておくと意図が分かりやすくなります。

## Editor Testing

Unity Editor では実機がなくても基本挙動を確認できます。

- 通常操作はマウス左ドラッグで確認
- ピンチは `Alt + Drag` で 2 点入力をシミュレート
- 実機では `EnhancedTouch`、Editor では状況に応じてマウス入力へフォールバック

`GestureCoordinator` を `External` モードで使う場合は、事前に `EnhancedTouchSupport.Enable()` を有効化してください。

## Sample Scene

このリポジトリには動作確認用のサンプルシーンが含まれています。

- Scene: `Assets/Sample/Scenes/SampleScene.unity`
- Script:
  - `Assets/Sample/Scripts/SampleCamera.cs`
  - `Assets/Sample/Scripts/SampleCube.cs`

サンプルでは次を確認できます。

- UI 領域上でのドラッグによるカメラ移動
- UI 領域上でのピンチによるズーム
- 3D オブジェクトに対するドラッグ操作

## Repository Layout

```text
Packages/com.daitokuamy.unityenhancedgesture/
  Editor/
  Runtime/
Assets/Sample/
docs/specs/
```

- `Packages/com.daitokuamy.unityenhancedgesture`
  - 配布対象の UPM パッケージ本体
- `Assets/Sample`
  - 動作確認用のサンプルアセット
- `docs/specs`
  - 仕様メモと設計整理用ドキュメント

## License

MIT License
