using System.Collections;
using UnityEngine;

// 화면 흔들림 (어디서든 CameraShake.Play(세기, 시간))
// 메인 카메라는 Cinemachine이 매 프레임 위치를 정하므로, 그 다음(LateUpdate 마지막)에 흔들림만큼 옮겨 그리고
// 화면을 그린 뒤 원래 위치로 되돌린다. 게임 정지(timeScale 0) 중에는 흔들지 않는다.
[DefaultExecutionOrder(10000)]
public class CameraShake : MonoBehaviour
{
    private static CameraShake instance;

    private float power;
    private float duration;
    private float remain;
    private Vector3 applied;
    private readonly WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();
    private bool restoring;

    // 세기(월드 단위), 시간(초). 더 센 흔들림이 오면 그쪽으로 덮어씀
    public static void Play(float power, float time)
    {
        Camera cam = Camera.main;
        if (cam == null || power <= 0f || time <= 0f)
            return;

        if (instance == null || instance.gameObject != cam.gameObject)
        {
            instance = cam.GetComponent<CameraShake>();
            if (instance == null)
            {
                instance = cam.gameObject.AddComponent<CameraShake>();
            }
        }

        if (instance.remain <= 0f || power >= instance.power * (instance.remain / Mathf.Max(0.01f, instance.duration)))
        {
            instance.power = power;
            instance.duration = time;
            instance.remain = time;
        }
    }

    // 약: 무기 전용 스킬, 필살기 / 강: 각성 효과 (세기·시간은 시너지 데이터 인스펙터에서 조정)
    public static void Weak()
    {
        WeaponSynergyData d = WeaponSynergy.Data;
        Play(d.shakeWeakPower, d.shakeWeakTime);
    }

    public static void Strong()
    {
        WeaponSynergyData d = WeaponSynergy.Data;
        Play(d.shakeStrongPower, d.shakeStrongTime);
    }

    private void LateUpdate()
    {
        if (remain <= 0f || Time.timeScale <= 0f)
            return;

        remain -= Time.deltaTime;
        float k = Mathf.Clamp01(remain / Mathf.Max(0.01f, duration));      // 점점 약해짐
        applied = (Vector3)(Random.insideUnitCircle * power * k);
        transform.position += applied;

        if (!restoring)
        {
            StartCoroutine(Restore());
        }
    }

    // 화면을 그린 뒤 원래 위치로 되돌림
    private IEnumerator Restore()
    {
        restoring = true;
        yield return endOfFrame;
        transform.position -= applied;
        applied = Vector3.zero;
        restoring = false;
    }

    private void OnDisable()
    {
        if (applied != Vector3.zero)
        {
            transform.position -= applied;
            applied = Vector3.zero;
        }
        restoring = false;
        remain = 0f;
    }
}
