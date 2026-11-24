using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 메뉴 버튼 반응: 마우스를 올리면 살짝 커지고 금색 테두리가 밝아짐, 누르면 작아짐 (실제 시간 기준)
public class MenuButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public Selectable target;
    public Outline frame;                // 금색 테두리 (없어도 됨)
    public Color frameColor;
    public Color frameHoverColor;

    private bool hover, pressed;
    private Vector3 baseScale = Vector3.one;

    private void Awake()
    {
        baseScale = transform.localScale;
    }

    private void OnDisable()
    {
        hover = pressed = false;
        transform.localScale = baseScale;
        if (frame != null) frame.effectColor = frameColor;
    }

    public void OnPointerEnter(PointerEventData eventData) { hover = true; }
    public void OnPointerExit(PointerEventData eventData) { hover = false; pressed = false; }
    public void OnPointerDown(PointerEventData eventData) { pressed = true; }
    public void OnPointerUp(PointerEventData eventData) { pressed = false; }

    private void Update()
    {
        MenuCatalog cat = MenuCatalog.Instance;
        bool active = target == null || target.IsInteractable();
        float goal = !active ? 1f : pressed ? cat.pressScale : hover ? cat.hoverScale : 1f;
        float k = 1f - Mathf.Exp(-cat.scaleSpeed * Time.unscaledDeltaTime);
        transform.localScale = Vector3.Lerp(transform.localScale, baseScale * goal, k);

        if (frame != null)
        {
            frame.effectColor = active && (hover || pressed) ? frameHoverColor : frameColor;
        }
    }
}
