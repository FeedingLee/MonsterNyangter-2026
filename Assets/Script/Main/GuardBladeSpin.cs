using UnityEngine;

// 시너지 「칼날 호위」: 칼날을 붙인 중심 오브젝트를 보조 쌍검처럼 계속 회전
public class GuardBladeSpin : MonoBehaviour
{
    public float speed = 300f;                               // 도/초

    private void Update()
    {
        transform.Rotate(Vector3.back * speed * Time.deltaTime);
    }
}
