using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 체력바를 플레이어에 붙임
// 화면 좌표로 매 프레임 따라가게 하면 카메라(Cinemachine·PixelPerfectCamera) 갱신 순서·투영 차이로 덜덜 떨린다.
// 그래서 게임이 시작되면 체력바(Health)를 플레이어 자식 월드 캔버스로 옮겨 탄창 게이지처럼 캐릭터와 함께 그린다.
// 크기는 원래 화면 캔버스에서 보이던 크기(카메라 확대에 맞춰)를 그대로 유지하고, 원래 HUD가 꺼지면 함께 숨긴다.
public class Follow : MonoBehaviour
{
    public int sortingOrder = 100;                           // 맨 위 정렬 층 안 순서 (나무·텐트 등 어떤 월드 그림보다 앞)

    RectTransform rect;
    Transform home;                                          // 원래 부모 (HUD): 꺼지면 체력바도 숨김
    Canvas worldCanvas;
    float screenPxPerUnit = 1f;                              // 원래 화면 캔버스에서 한 칸의 화면 픽셀

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    private void LateUpdate()
    {
        if (worldCanvas == null)
        {
            AttachToPlayer();
            return;
        }
        bool show = home == null || home.gameObject.activeInHierarchy;
        if (worldCanvas.enabled != show) worldCanvas.enabled = show;
        Camera cam = Camera.main;
        if (cam != null && cam.orthographic && Screen.height > 0)
        {
            // 화면 픽셀 → 월드: 카메라 확대가 바뀌어도 화면에서 같은 크기
            float worldPerPx = 2f * cam.orthographicSize / Screen.height;
            Vector3 parentScale = worldCanvas.transform.parent.lossyScale;
            float k = screenPxPerUnit * worldPerPx;
            worldCanvas.transform.localScale = new Vector3(k / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)), k / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)), 1f);
        }
    }

    private void AttachToPlayer()
    {
        GameManager gm = GameManager.instance;
        if (gm == null || gm.player == null || Camera.main == null)
            return;
        Canvas screenCanvas = GetComponentInParent<Canvas>();
        if (screenCanvas == null)
            return;
        screenPxPerUnit = screenCanvas.rootCanvas.scaleFactor;
        home = transform.parent;

        GameObject go = new GameObject("Health World Canvas", typeof(RectTransform));
        go.transform.SetParent(gm.player.transform, false);
        ((RectTransform)go.transform).sizeDelta = Vector2.zero;
        worldCanvas = go.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;
        SortingLayer[] layers = SortingLayer.layers;          // 가장 위 정렬 층 (나무에 가려지지 않게 항상 맨 앞)
        if (layers.Length > 0) worldCanvas.sortingLayerID = layers[layers.Length - 1].id;
        worldCanvas.sortingOrder = sortingOrder;

        rect.SetParent(go.transform, false);                 // 크기(localScale)는 그대로, 위치만 플레이어 중심으로
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.localPosition = Vector3.zero;
    }
}
