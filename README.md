# KeyboardRhythm：楽曲付きデモ

Unity 6000.6.4f1、新Input System 1.20.0、uGUIを使う10レーン＋Spaceのリズムゲーム。2026年10月4日更新。

## プレイ

1. Unityのインポート・コンパイル完了後、`Tools > KeyboardRhythm > Open Silent Chart Test` を開いてPlay。
2. 左上のプルダウンで **Will you still cry? / BASIC (1 min)** または **Will you still cry? / ADVANCED (1 min)** を選択し、Enterで開始。
3. 指定MP3（Musics/Will_you_still_cry_.mp3）に、約1分のBASIC 176ノート／ADVANCED 352ノートを配置。BPMはユーザー確認済みの178、開始秒0。音楽に合わせた採譜は未実施。楽曲・ノート描画・判定はDSP時計を共用する。

中央に縦長レーン、左に楽曲・譜面情報と操作、右に判定数を表示する。コンボ表示はレーン中央付近に不透明度50%の文字で重ね、3コンボ以上で表示する（0～2は非表示）。判定結果は画面下から40%の位置に表示し、押した時刻のずれを `FAST 75.0 ms`／`LATE 125.0 ms` の形で示す。GREAT／GOODのずれはFASTが青、LATEが赤。保持中の定期判定やMISSには押下のずれを表示しない。

- マウスのプルダウン：譜面名で選択。選択時は音を停止しREADYへ戻る。F1／F2は使用しない。
- Enter：開始、フォーカス喪失による停止からの再開。
- F3：メトロノーム切替。音源なしでは初期ON、楽曲ありでは初期OFF。
- F4または右側のチェックボックス：ノート取得音をON／OFF。初期ONで、取得成功時に小さな短い音が鳴る。
- F5：現在の譜面を再読込。ノート・結果・コンボを初期化。
- F6／F7：ノート表示と判定を10ms早める／遅らせる。Shift併用で1ms。F8で0ms。範囲±500msはデモ用仮設定。
- 右側のスライダー、F9／F10：落下速度を0.1倍ずつ減速／加速。0.5～3.0倍、初期1.0倍。F11で1.0倍へ戻す。プレイ中も変更可能。

落下速度は表示だけを変え、音楽の速度や判定時刻は維持する。速度と取得音のON／OFFは端末に保存する。取得音はDOUBLEの成立時や長押しの始点でも鳴り、MISS・範囲外入力・長押し中の定期加算では鳴らない。同時取得は1音にまとめる。音量はPlay停止中に`SilentChartPlayer`のInspectorで`Hit Sound Volume`を調整できる（初期0.08）。

端末補正はPlayerPrefsに保存し、全譜面で共用する。READYでは即時適用。開始後の変更はF5または譜面選択後の次回プレイへ適用し、停止からの再開では現在値を維持する。楽曲の再生速度と譜面ごとの開始秒は変更しない。

## 楽曲と譜面を追加する

音源を `Assets/Resources/Music/曲名.wav` 等へ置き、譜面JSONを `Assets/Resources/Charts/任意の名前.json` へ保存する。譜面ファイルから音源への相対パスは `../Music/曲名.wav`。既存の `Musics` フォルダーを使う場合は `../Musics/ファイル名.mp3` と指定できる。UnityがAudioClipとしてインポートできる音源を使用する。

譜面JSONの `title` は選択欄に表示する譜面名、`audio.songTitle` は楽曲名、`audio.songId` は共有する楽曲ID。同じ曲を参照する複数のJSONを作り、それぞれ別の `chartId` と `title` を設定できる。古い音楽情報のない譜面も読み込める。

`audio.chartZeroAtAudioSeconds` は楽曲に対する譜面0拍の秒数。2なら曲の2秒で0拍、-1なら0拍の1秒後に曲が始まる。端末補正とは独立する。式は `ノート時刻 = 音楽秒 - 譜面開始秒 - 端末補正ms / 1000`。

JSONの追加・移動・削除時にはEditorで選択一覧を自動生成する。反映されない場合は `Tools > KeyboardRhythm > Refresh Chart Library` を実行し、Playを再開始する。音源が見つからない場合は画面とConsoleへエラーを出す。通常の保存はPlay停止中に行い、既存.metaは維持する。

外部エディターは `C:/dev/KeyboardRhythmChartEditor/chart-editor.html`。音源の試聴、現在位置を譜面0拍に設定、数値による正負の開始秒の入力に対応。ブラウザーで選んだ音源はJSONへ埋め込まれないため、Unityへ別途コピーする。詳細は [CHART_FORMAT.md](docs/CHART_FORMAT.md)。

同梱のClockwork Neonは、このデモ用に合成した音源。`python tools/generate_demo_music.py` で再生成できる。

## 検証と制限

.NETで既存判定164件・端末補正167件・楽曲同期／表示68件・メトロノーム15件・落下速度／取得音87件、計501件成功。Unityの実DLLでRuntimeとEditorを分けて外部コンパイルし警告0・エラー0。エディターのデータ処理17件、Edgeの操作48項目は過去の確認結果。

**Unityでの実行未実施。** Playの実際の音出し・取得音の音量・スライダー／切替操作・選択・表示・フォーカス停止／再開、実キーボード入力、配布ビルドは未確認。実行手順と記録は [STATUS.md](docs/STATUS.md)。

`dotnet run --project verification/CoreChecks.csproj` で判定と時計の計算を検査できる。Unityメニュー `Tools > KeyboardRhythm > Run Silent Chart Checks` ではさらに登録譜面と音源の読込を検査する。

判定窓±50／100／150ms、DOUBLEの相互差50ms、LONGの空白100ms等は従来の仮設定を維持する。詳しいルールは [SPEC.md](docs/SPEC.md)、長押し・DOUBLEの確認例は [LONG_DOUBLE_CHECKS.md](docs/LONG_DOUBLE_CHECKS.md)。イベント実行、BPM変更／停止、精密な入力イベント時刻、個別遅延の自動校正は未実装。Sceneのビルド登録は今回変更していない。

楽曲が譜面より長い場合も、最終ノートの判定猶予と結果余韻の後に音楽を停止する。今回のBASICは譜面開始から約60.14秒、ADVANCEDは約60.31秒（端末補正0ms）。Enter後のカウントインは別。
