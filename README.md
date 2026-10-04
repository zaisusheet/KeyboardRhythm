# KeyboardRhythm：無音のLONG・DOUBLE確認版（保持判定PERFECT対応）

2026年10月4日。Unity 6000.6・新Input System・uGUIを使用する既存プロジェクト向け。音楽なしの試作にLONG、DOUBLE、長押しFLOORを追加した。TOUCHは通常幅2以上、FLOORは共通10レーンのレーン3中心～8中心の表示を維持する。前回の24ノート譜面はそのまま残している。

**提供環境ではUnity/C#コンパイラーを使えないため、今回のコンパイル・判定テスト・Playは未確認。** JSONとソース・文書の静的整合性を確認し、実際の判定コードを検査するUnityメニューと.NET用チェックを同梱した。

## 今回の変更

LONG・FLOOR_LONGの始点判定は時間差に従うGREAT・GOODのまま残し、保持中の定期判定はPERFECTに分離した。空白・MISSの処理と記録は維持し、再保持後の定期判定もPERFECTにする。これはユーザー指定による確定仕様。

前回のLONG/DOUBLE版を導入済みなら、更新するコードは `Assets/KeyboardRhythm/SilentPrototype/RhythmSession.cs` と `Assets/KeyboardRhythm/SilentPrototype/Editor/GameplayCoreChecks.cs` の2ファイル。README・関連docsも反映する。既存.meta・Sceneは保持し、譜面JSONは変更しない。詳しい確認例は [LONG_DOUBLE_CHECKS.md](docs/LONG_DOUBLE_CHECKS.md)。

## VS Code / Codexで行うこと

1. ZIPを展開し、Assetsのファイルを既存の `C:\dev\KeyboardRhythm\Assets` へ構成を保って反映する。導入済みファイルは差分を確認して更新し、既存.meta・Scene・関係しないコードは保持する。
2. docsのSPEC v0.5・STATUS・CHART_FORMAT・LONG_DOUBLE_CHECKS・Schemaを、リポジトリ最新版と整合させて反映する。
3. 必要に応じて同梱verificationをプロジェクト直下へ追加し、.NET SDK 10で `dotnet run --project verification/CoreChecks.csproj` を実行する。既存のUnity自動生成csprojは編集しない。

新規ファイルは `Assets/Resources/Charts/long_double_demo.json`、`Assets/KeyboardRhythm/SilentPrototype/Editor/GameplayCoreChecks.cs`、説明文書とverification。既存のChartData・RhythmSession・KeyboardLaneInput・SilentChartPlayer・SilentChartView・Editorメニュー2ファイルを更新した。NoteLayoutと基本silent_demo.jsonは前回版から変更していない。

Codexへの依頼例：

```text
展開したSilentChartPrototypeの保持判定PERFECT更新版を反映してください。
AGENTS.md、docs/SPEC.md、docs/STATUS.mdを読んでから作業してください。
LONG/DOUBLE版が導入済みならRhythmSession.csとEditor/GameplayCoreChecks.cs、README・関連docsを既存変更と差分を確認して反映してください。始点評価を保持し、保持中の定期判定はPERFECTにしてください。
既存の.metaとSceneを保持し、更新対象以外のコードは保持してください。
.NET SDK 10が利用できる場合はverification/CoreChecks.csprojで実際の判定チェックを実行してください。
Unity向けのコンパイル確認を行い、未実施の確認と結果をSTATUSへ区別して記録してください。
Unityでの操作は Tools > KeyboardRhythm > Open LONG DOUBLE Test、Play、Gameビュー、Enterです。
```

## Unityで行うこと

1. インポート・コンパイル後、`Tools > KeyboardRhythm > Open LONG DOUBLE Test` を選ぶ。
2. Consoleに `Silent Chart checks passed` が表示されたら、Playを押す。専用の `Assets/Scenes/LongDoubleTest.unity` を自動作成／開く。現在のシーンに未保存変更がある場合はUnityの保存確認に対応する。
3. GameビューをクリックしてEnterを押す。3秒後に先頭ノートへ到達する。今回の譜面は約41秒で終了する。

Hierarchyへの手動追加、Inspectorの参照設定、ノート配置は不要。Sceneはプレイヤー1つと背景Cameraを持ち、Canvas・レーン・長押しの帯・ノートラベルをコードで生成する。初回に生成された新規.metaとSceneをGitへ含める。Sceneのビルド登録は自動では行わない。

## 操作と確認順

- Enter：開始／フォーカス外れによる停止からの再開。
- F1：前回のTOUCH/FLOOR基本譜面へ切替。F2：LONG/DOUBLE譜面へ切替。どちらもREADYへ戻るのでEnterで開始。
- F5：現在の譜面を再読込し、全ノート状態・キー候補・結果・コンボを初期化。
- 水色：TOUCH。黄色の二重線：DOUBLE。緑の帯：LONG。紫：Space単発／SPACE HOLD。

まずDOUBLEの異なる2キー（同じレーンのQ+Aも可）を確認する。続いてLONGの開始・持ち替え・短い空白・長い空白・復帰、LONGと単発ノートの共有、2本のLONG、Space長押しを確認する。譜面内の時刻と操作例は [LONG_DOUBLE_CHECKS.md](docs/LONG_DOUBLE_CHECKS.md) を参照する。

## 仮設定と未実装

DOUBLEの相互差50ms、悪い方の判定採用、LONGの空白100msでMISS、新しい押下による開始、開始MISS後の途中参加不可、開始成功+1・終端加算なし、BPM<120の16分間隔、共有LONGの各ノート加算は全て試作用の仮方式。最終仕様は未決定事項U03～U10として保持する。値はPrototypeSettings、方式はRhythmSessionにまとまっている。従来の±50/100/150msの時間窓も仮値。

音楽再生、精密な入力イベント時刻、音声・入力・表示の遅延補正、イベント実行、GUI譜面エディター、スコア・クリア条件・設定保存は未実装。イベントタグは読み込んで保持するだけで、時計や判定へ作用しない。譜面のschemaVersionは1を維持する。詳細は [CHART_FORMAT.md](docs/CHART_FORMAT.md)。

## 自動チェック

`Tools > KeyboardRhythm > Run Silent Chart Checks` は実コードに対して、時間窓、物理2キー、キー保持と再押下、LONG/FLOOR_LONGの早い・遅いGREAT/GOOD始点とPERFECT定期判定、LONGの持ち替え、空白境界、MISS連打防止、復帰、開始・終端・低BPMの加算、入力共有、複数ノートの時間順、初期化、不正譜面、FLOOR表示範囲を検査する。Unityでは追加でJSONの読込・イベント保持も確認する。Scene作成メニューも同じチェックを実行し、失敗時は先へ進まない。

.NET単体チェックはUnityの描画と入力を検査しない。UnityでのコンパイルとPlayを別に確認する。確認後は [LONG_DOUBLE_CHECKS.md](docs/LONG_DOUBLE_CHECKS.md) の報告例に沿ってSTATUSへ反映する。
