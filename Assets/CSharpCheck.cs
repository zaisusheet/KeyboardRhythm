using UnityEngine;

public class CSharpCheck : MonoBehaviour
{
    void Start()
    {
        Debug.Log("C#の動作確認に成功しました！");
    }

    void Update()
    {
        transform.Rotate(0f, 0f, 60f * Time.deltaTime);
    }
}