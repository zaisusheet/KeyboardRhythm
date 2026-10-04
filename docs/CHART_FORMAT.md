# 譜面形式 v1（無音試作）

更新日：2026年10月4日。将来の音楽・イベント・外部GUI編集を見据えた形式v1。今回の表示・配置方針の変更による構造変更はなく、schemaVersionは1を維持する。判定幅やコンボの最終仕様を確定するものではない。

## 設計

UTF-8のJSONを正本とする。UnityのScene、Prefab、GUIDやScriptableObjectの参照を譜面に入れない。将来、Unity以外で動くGUIでも同じJSONを読み書きできる。現在のUnityローダーは `JsonUtility` を使用し、追加パッケージは不要。

現在の試作は `Assets/Resources/Charts/silent_demo.json` または `long_double_demo.json` をResources経由で読む。F1/F2で切り替えられる。これは読込方法の試作であり、将来ユーザーが追加した任意ファイルを読む仕組みではない。外部GUIの書出し結果は、まずこのJSONに置き換えてUnityで確認できる。将来は外部ファイルローダーを追加し、ChartDataと判定処理を再利用する。

| 項目 | 意味 |
| --- | --- |
| `schemaVersion` | 構造の版。現在は1。非互換の変更時は版を上げ、移行処理を用意する |
| `chartId` / `title` | 譜面識別子と表示名 |
| `audio.path` | 将来の楽曲ファイルの相対参照。譜面ファイルのあるフォルダを起点とし `/` を使用。空文字は音楽なし。現在のプレイヤーは再生しない |
| `audio.chartZeroAtAudioSeconds` | 譜面の0秒が、音楽の何秒に対応するか。将来は `譜面秒 = 音楽の再生秒 - この値`。例えば1.2なら譜面beat 0を音楽1.2秒に合わせる。無音試作では使用しない |
| `timing.initialBpm` | 初期BPM。四分音符1つを1拍とする。現在は一定BPMのみ |
| `notes` | ノート配列。順序は問わず、読込時に時刻・ID順で並べる |
| `events` | イベント配列。空なら `[]`。タグ・パラメーターを保持するが、現在は実行しない |

ノートの `beat` は譜面先頭を0とする拍数。120BPMでは1拍0.5秒、beat 2は1秒。小数に対応する。**現在の換算は `秒 = beat × 60 / initialBpm`**。BPM変更・停止を実装した段階では、拍と秒を相互変換するタイミングマップを追加する。表示速度の変更だけでは拍と秒の対応を変えない。

GUIでの音符分割は、例えば八分音符0.5拍・十六分音符0.25拍として書き出せる。JSONを生成する際は数値を文字列にしない。より細かい分割の編集時は、GUI側で拍を分数として扱い、書出し時に数値へ変換する案とする。

## ノート

```json
{"id":"n01","type":"TOUCH","beat":2,"lane":3,"width":3,"durationBeats":0}
```

この例は1秒時点にレーン3～5のいずれかで新しく1キーを押す。上中下の段を問わず E/D/C、R/F/V、T/G/B が対象になる。

| 種類 | `lane` / `width` | `durationBeats` | 現在のプレイ対応 |
| --- | --- | --- | --- |
| `TOUCH` | 開始レーン1～10、通常は幅2～10。構造上は例外用に幅1も許可。終端が10以下 | 0 | 対応 |
| `FLOOR` | ともに0。Space専用 | 0 | 対応 |
| `DOUBLE` | 開始レーン1～10、幅1～10、終端が10以下 | 0 | 試作対応。異なる物理2キーの新しい押下 |
| `LONG` | 開始レーン1～10、幅1～10、終端が10以下 | 0より大きい拍数 | 試作対応。開始・保持・持ち替え・空白・復帰 |
| `FLOOR_LONG` | ともに0 | 0より大きい拍数 | 試作対応。Space長押し |

### 表示幅と入力範囲

TOUCHは押しやすさを考慮し、通常の譜面では幅2以上を使う。幅1は原則配置しないが、データ形式の許容範囲は1～10を維持する。今回の24ノートには幅1のTOUCHを含めない。

FLOORはTOUCHと同じ10レーン上を流し、同じ判定ラインへ到達する。横端はレーン3の中心とレーン8の中心で、幅は5レーン分。通常キーの入力範囲を表さないため、データは `lane: 0, width: 0` のままとする。Spaceのみで取得する。`lane: 3, width: 6` の通常ノートとして保存しない。FLOOR_LONGも同じ表示範囲で、長押しの帯を表示する。今回の試作で長押し処理を追加した。

`NoteLayout` が表示範囲を計算する。レーン1の左端を0とするレーン単位では、FLOOR左端2.5・右端7.5。この横位置を描画に使い、Spaceの入力条件は `RhythmSession` で判定する。GUIも同じレーン表示にSpace専用ノートを配置する。

ノートIDは同じ譜面内で重複不可。空配列、負の拍、不正な幅、未知の種類を拒否する。同じ拍にエリアが重なるTOUCH/DOUBLEは禁止する。判定時間帯が近接する場合の重複制約はSPEC U09として未決定。この試作では新しい物理キー押下1回につき、対象内で最も時刻が近い未処理ノート1つを取得する。同距離なら予定時刻・IDの順を採用する。LONGの開始・保持には同じ入力を共有でき、DOUBLEの候補には物理キーIDと押下時刻を個別に保存する。これは近接ノートについての暫定方式である。

### LONG・DOUBLEの時間と判定方式

LONG・FLOOR_LONGの終端は `beat + durationBeats`。始点をMISSしても終端までノートを残し、新しい物理キーの押下で残り区間へ途中参加できる。保持済みキーだけでは開始・自動参加しない。同じレーンでも別物理キーの新しい押下は有効（F保持＋R押下等）。Spaceは離して押し直す。途中参加には追加の始点判定を付けず、参加時刻以降の予定保持判定だけを付ける。始点MISSと未取得区間は残し、過去の加算を補わない。一定BPMで拍を秒へ変換し、最後のノート開始だけでなく最も遅い終端までプレイする。DOUBLEは同じレーンを使う異なる物理キーを区別するため、レーンだけに集約した押下では判定しない。入力は31キーそれぞれの新しい押下・現在の保持を同じ譜面秒で渡す。

LONG・FLOOR_LONGの保持中の定期判定はPERFECT。始点がGREAT・GOODでも、その評価を定期判定へ引き継がない。空白・MISSの評価は別に残し、再保持後の定期判定もPERFECTとする。始点のカウントや過去のMISSは上書きしない。保持PERFECT、物理キー単位の新しい開始押下、始点MISS後の途中参加、途中MISS後の復帰はユーザー指定による確定仕様。DOUBLEの相互差、LONGの空白・加算の境界等は試作用の仮設定。具体的な数値と操作例は [LONG_DOUBLE_CHECKS.md](LONG_DOUBLE_CHECKS.md)。判定イベント数と完了ノート数を分け、LONGの加算・途中MISS・復帰で完了数を誤って増やさない。音楽・イベントの未実装状態とschemaVersion 1は維持する。

## 拡張イベント

```json
{
  "id": "e01",
  "beat": 4,
  "tag": "scroll.speed",
  "parameters": [
    {"name":"multiplier", "type":"number", "value":"1.5"}
  ]
}
```

`tag` は任意の文字列。`scroll.speed`、`playback.stop`、`custom.example` はデータ例で、処理は未実装。イベントを削除しても、この試作のノート時刻・移動速度・判定は同じになる。**試作のフォーカス外れによる一時停止と、譜面内の停止イベントは別である。**

パラメーターは `name / type / value` の配列とする。名前の重複は禁止。`type` は `string`、`number`、`boolean`、`json`。`value` はどの型でもJSON上は文字列にする。例えば数値は `"1.5"`、真偽値は `"true"`、複合データは `"{\"color\":\"cyan\"}"`。値の解釈と範囲検証は将来のイベント処理・GUI側で行う。現在のUnityローダーは中身を評価しない。

この配列方式で、Unity標準のJSON読込でも独自タグ・パラメーターを保持できる。GUIは既知タグに入力フォームを出し、未知タグは名称とパラメーターを保持して保存する。新タグの追加だけなら構造の変更は不要。イベントごとに直接新フィールドを追加する方式は採用せず、パラメーターへ入れる。Unityは未知の構造フィールドを無視するため、将来のGUIはschemaで検査し、未知フィールドを消さず警告する。現在のプレイヤーには譜面の保存処理がない。

速度変更は将来「表示速度の倍率」と「BPM変更」を別タグにする。停止イベントについても、音楽まで停止するか・譜面進行だけ止めるか・入力やLONGをどう扱うかを先に決める。今回これらの動作仕様は確定しない。

## 将来のGUI編集

最初のGUIは、タイムライン・共通10レーン上の通常ノート／Space専用ノート配置・イベント属性・JSON読込／書出しを最小機能にする。実装場所はUnity Editorまたは外部のデスクトップ／Webアプリから選べる。次に音楽の波形、再生とプレビュー、BPM・停止のタイミングマップを追加する。現在はGUIエディターを実装していない。将来のGUIではTOUCHの通常配置幅を2以上とし、幅1を選ぶ場合は例外扱いの確認を用意する案とする。

`chart.schema.json` は構造検査用のJSON Schema。レーン終端、ID重複、同時ノートの交差など、項目をまたぐ制約は `ChartValidation` でも検査する。イベント固有の値検査は追加実装が必要。新しい譜面形式の変更はこの文書・Schema・ChartDataをセットで更新する。

Unityの参考資料：

- [JSON serialization](https://docs.unity3d.com/ja/current/Manual/json-serialization.html)
- [Time.realtimeSinceStartupAsDouble](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/Time-realtimeSinceStartupAsDouble.html)
- [AudioSettings.dspTime](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/AudioSettings-dspTime.html)
- [AudioSource.PlayScheduled](https://docs.unity3d.com/6000.6/Documentation/ScriptReference/AudioSource.PlayScheduled.html)
