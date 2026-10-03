using UnityEngine;
using UnityEngine.InputSystem;

public class KeyboardInputCheck : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("入力チェック開始：Game画面をクリックしてキーを押してください。");
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        // キーボードが認識されていない場合は処理しない
        if (keyboard == null)
        {
            return;
        }

        // 各キーを個別に確認するため、同時押しも検出できる
        foreach (var key in keyboard.allKeys)
        {
            if (key.wasPressedThisFrame)
            {
                Debug.Log($"押した: {key.keyCode}");
            }

            if (key.wasReleasedThisFrame)
            {
                Debug.Log($"離した: {key.keyCode}");
            }
        }
    }
}