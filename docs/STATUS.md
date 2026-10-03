# KeyboardRhythm 開発状況

更新日：2026年10月4日（日本時間）

ゲームのルールは[SPEC.md](SPEC.md)で管理する。この文書は保存済みファイルから確認できる実装状況と、実行確認の結果を記録する。

## 記録の対象

| 項目 | 内容 |
| --- | --- |
| 仕様 | v0.1（2026年10月3日更新） |
| Unity Editor | 6000.6.4f1（リビジョン`12bfff696524`） |
| Input System | 1.20.0 |
| 入力設定 | `ProjectSettings/ProjectSettings.asset`の`activeInputHandler: 1`。新Input Systemのみ有効 |
| Universal RP | 17.6.0 |
| Test Framework | 1.8.0 |
| uGUI | 2.6.0 |
| 対象Scene | `Assets/Scenes/SampleScene.unity`、`Assets/Scenes/GamePlayTest.unity` |
| 基準コミット | `7b6e892218a1a290074921780201430f9fc81d0a`（`Initialize Unity project and game specification`） |
| 未コミット変更 | あり。作業開始時は`docs/`が未追跡。今回作成した`AGENTS.md`と本書も未コミット |
| 確認方法 | 設定、仕様書、C#、Scene YAML、`.meta`、入力アセット、Git状態の静的確認。Unityでの実行・ビルドは未実施 |

バージョンは`ProjectSettings/ProjectVersion.txt`、`Packages/manifest.json`を参照する。上記主要パッケージのバージョンは`Packages/packages-lock.json`とも一致している。

## 実装済み・設定済み

| 対象 | 保存済みファイルから確認した内容 |
| --- | --- |
| `SampleScene.unity` | Main Camera、Directional Light、Global Volumeを配置 |
| `GamePlayTest.unity` | 上記構成にCubeを追加 |
| ビルド対象 | `ProjectSettings/EditorBuildSettings.asset`には`SampleScene.unity`のみ有効登録。`GamePlayTest.unity`は未登録 |
| `Assets/CSharpCheck.cs` | `MonoBehaviour`。`Start`で動作確認ログを出し、`Update`でY軸を毎秒60度回転させるコードあり |
| `Assets/NewEmptyCSharpScript.cs` | 空の通常クラス。`MonoBehaviour`の継承なし |
| `Assets/InputSystem_Actions.inputactions` | 汎用の`Player`・`UI`マップあり。音楽ゲーム用の31キー設定はなし |
| テンプレートの説明表示 | `Assets/TutorialInfo/Readme.cs`と`Assets/TutorialInfo/Editor/ReadmeEditor.cs`あり |
| 開発用文書 | `docs/SPEC.md`あり。今回`AGENTS.md`と本書を作成 |

`CSharpCheck.cs`のGUIDは`5423e63593427854396023f044419620`。保存済みの両Sceneには、このスクリプトを参照するコンポーネントがない。コードの存在は確認できるが、Sceneへの接続は未実施。

## 未実装

- 31キーの押下・保持状態、レーン番号、Spaceを画面に表示する入力テスト。
- 音楽再生と譜面時間の管理。
- 10レーン、可変幅ノート、判定ラインの表示。
- TOUCH、DOUBLE、LONG、単発型・長押し型FLOORの取得・保持判定。
- ノート間の入力共有、重複制約、LONGの持ち替え・途中MISS・復帰。
- 判定表示、コンボ加算、低テンポ補正。
- スコア、クリア条件、譜面読み込み、表示速度、設定保存。これらの仕様はU10として未決定。

## Unityで確認済み

この記録に対応するUnityでの実行確認結果は未取得。ログ文言に「成功」と書かれていても、実際の実行成功を示す証拠としては扱わない。

## 未確認

- Unity Editorでのコンパイル、Play Mode、ビルドの成否。
- `CSharpCheck`をSceneに接続した状態でのログ出力・回転。
- 実機での31キー同時入力、押下・保持・離しの検出、JIS・US配列の記号キー識別。
- 音声・入力・表示の時刻対応と遅延。

## 既知の不具合・不足

- 実行によって確認した不具合は未記録。未確認のため、不具合がないと断定しない。
- `CSharpCheck`のScene接続と`GamePlayTest`のビルド登録がない。現時点では未接続・未設定として扱う。
- `SPEC.md`の開発環境欄はセットアップ前の記述のままで、正確なバージョンは本書に記録した。

## 次の作業（提案）

1. `SPEC.md`第10節の提案に沿って、31キーの入力状態・レーン番号・Spaceを確認できるテストSceneを実装する。既存の`GamePlayTest`を使用するかは実装時に判断する。
2. U01の＋・?のキー識別と配列対応を整理する。試作に仮の割り当てを使用する場合は、仮の方式・採用理由・変更箇所を明記する。
3. UnityでコンパイルとPlay Modeを確認し、使用したキーボード配列、ログ、必要な画像とともに結果を本書へ追記する。
4. 音楽と可変幅TOUCHへ進む段階で、U02の判定幅・境界値と音楽時刻・入力時刻の対応を整理する。

未決定事項U01～U10の内容は`SPEC.md`第9節を参照する。本書の作業順や実装案によって未決定事項を確定扱いにしない。
