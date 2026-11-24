using UnityEngine;

// 스프라이트 프레임을 순서대로 반복 재생 (위치·크기는 건드리지 않고 그림만 바꿈)
// 예: Wave Spawner/Wave_forest_0 의 숲 입구 (Wave_forest 시트 24프레임)
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteLoopAnimator : MonoBehaviour
{
    [Tooltip("재생할 프레임 (순서대로)")]
    public Sprite[] frames;
    [Min(0.1f)] public float fps = 8f;
    [Tooltip("한 바퀴 끝난 뒤 다시 시작하기 전 쉬는 시간 (초)")]
    [Min(0f)] public float loopDelay = 0f;
    [Tooltip("게임이 멈춰도(일시정지) 계속 재생")]
    public bool useUnscaledTime = false;

    private SpriteRenderer sr;
    private float time;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0)
            return;
        time += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float cycle = frames.Length / fps + loopDelay;
        float t = Mathf.Repeat(time, cycle);
        int index = Mathf.Min(frames.Length - 1, Mathf.FloorToInt(t * fps));
        if (frames[index] != null && sr.sprite != frames[index])
        {
            sr.sprite = frames[index];
        }
    }
}
