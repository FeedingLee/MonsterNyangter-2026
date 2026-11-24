using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class Item : MonoBehaviour
{
    public ItemData data;
    public int level;
    public Weapon weapon;
    public Gear gear;
    public static int Exp_Bonus;
    public static int Super_Magnet;

    Image icon;
    Text textLevel;
    Text textName;
    Text textDesc;

    private void Awake()
    {
        icon = GetComponentsInChildren<Image>()[1];
        icon.sprite = data.itemIcon;
        if (ExtraCards.IsExtra(data.itemType))
        {
            icon.preserveAspect = true;                 // 추가 카드 아이콘은 비율 유지
            if (data.itemType == ItemData.ItemType.CoinPouch)
            {
                icon.transform.localScale = Vector3.one * 1.45f;   // 금화 상자 그림이 작아 보여 키움
            }
            if (data.itemType == ItemData.ItemType.LastStand)
            {
                ExtraCards.DecorateLastStand(icon);
            }
            if (data.itemType == ItemData.ItemType.Knockback)
            {
                icon.transform.localScale = Vector3.one * 1.5f;   // 밀어내기 아이콘은 가로로 길어 작아 보이므로 1.5배
            }
            else if (data.itemType == ItemData.ItemType.Pierce)
            {
                icon.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);   // 세로로 긴 화살을 무기 아이콘처럼 대각선으로
                icon.transform.localScale = Vector3.one * 1.3f;
            }
        }

        Text[] texts = GetComponentsInChildren<Text>();
        textLevel = texts[0];
        textName = texts[1];
        textDesc = texts[2];
        textName.text = data.itemName;
    }

    // 실행 중에 만든 카드에 데이터 지정 (ExtraCards)
    public void SetData(ItemData newData)
    {
        data = newData;
        if (icon != null)
        {
            icon.sprite = data.itemIcon;
            icon.preserveAspect = true;                 // 길쭉한 아이콘(화살 등)이 늘어나 보이지 않도록
            textName.text = data.itemName;
        }
    }

    private Image synergyIcon;       // 짝 무기 카드 우측 상단 메인 무기 아이콘

    // 메인 확정 후 이 카드가 메인 무기의 짝(시너지) 무기면 메인 무기 아이콘을 작게 표시
    private void UpdateSynergyIcon()
    {
        MainWeaponController main = MainWeaponController.Current;
        bool show = main != null && main.MainWeapon != null && main.MainWeapon.Data != null && WeaponSynergy.IsPartner(data.itemType);
        if (synergyIcon == null)
        {
            if (!show)
                return;

            // 카드(약 40x73, 아래 가운데 기준) 안에서 무기 그림 왼쪽 아래, Lv 글자 바로 위에 배경 없이 표시
            // 위치는 Lv 글자와 무기 그림의 실제 배치(가운데 기준 좌표)를 따라감
            RectTransform card = (RectTransform)transform;
            float cardWidth = card.rect.width;
            float size = cardWidth * 0.3f;
            RectTransform levelRect = textLevel.rectTransform;
            RectTransform iconRect = icon.rectTransform;
            // 카드 왼쪽 가장자리 안쪽, 아래쪽이 Lv 글자 윗선보다 위에 오도록 (Lv 글자를 가리지 않음)
            float x = -cardWidth * 0.5f + size * 0.5f + cardWidth * 0.06f;
            float levelTop = levelRect.anchoredPosition.y + textLevel.preferredHeight * 0.5f;
            float y = levelTop + size * 0.5f + cardWidth * 0.02f;

            GameObject go = new GameObject("Synergy Icon", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = levelRect.anchorMin;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(size, size);
            synergyIcon = go.AddComponent<Image>();
            synergyIcon.preserveAspect = true;
            synergyIcon.raycastTarget = false;
        }
        synergyIcon.enabled = show;
        if (show)
        {
            synergyIcon.sprite = main.MainWeapon.Data.itemIcon;
            synergyIcon.transform.SetAsLastSibling();
        }

        // 짝 무기 카드: 카드 위로 은은한 금빛 반짝임 (얻으면 좋다는 표시)
        CardSparkles sparkles = GetComponent<CardSparkles>();
        if (show && sparkles == null)
        {
            sparkles = gameObject.AddComponent<CardSparkles>();
        }
        if (sparkles != null)
        {
            sparkles.enabled = show;
        }
    }

    private void OnEnable()
    {
        textLevel.text = "Lv." + (level + 1);
        UpdateSynergyIcon();

        // 레벨업 추가 카드 (밀어내기, 무기 숙련, 폭탄 통 등)
        if (ExtraCards.IsExtra(data.itemType))
        {
            // 회복약(소모품) 카드를 복제해 만들었는데, 원본은 레벨 글자가 투명(알파 0)이라 다시 보이게 함
            for (Transform t = textLevel.transform; t != null && t != transform; t = t.parent)
            {
                t.gameObject.SetActive(true);
            }
            Color levelColor = textLevel.color;
            levelColor.a = 1f;
            textLevel.color = levelColor;

            if (ExtraCards.IsConsumable(data.itemType))
            {
                textLevel.text = ExtraCards.IsStorage(data.itemType) ? "보관" : "즉시";
            }
            textDesc.text = ExtraCards.Describe(this);
            return;
        }

        if (level == 0)
        {
            switch (data.itemType)                                     // 최초 획득에 따른 무기 설명
            {
                case ItemData.ItemType.DualBlades:
                    textDesc.text = string.Format("회전하며 \n마구 벤다냥!");
                    break;
                case ItemData.ItemType.HuntingBow:
                    textDesc.text = string.Format("백발백중!\n반드시\n맞춘다냥!");
                    break;
                case ItemData.ItemType.SniperHBG:
                    textDesc.text = string.Format("내 앞에서\n사라지라냥!");
                    break;
                case ItemData.ItemType.GreatSword:
                    textDesc.text = string.Format("참 모아..라고\n알고있냥?");
                    break;
                case ItemData.ItemType.Lance:
                    textDesc.text = string.Format("이쑤시개\n아니다냥.");
                    break;
                case ItemData.ItemType.Rate_Up:
                case ItemData.ItemType.Speed_Up:
                case ItemData.ItemType.Damage_Up:
                case ItemData.ItemType.Stamina_Up:
                case ItemData.ItemType.Weapon_Upgrade:
                    textDesc.text = string.Format(data.itemDesc,
                        data.damages[level] * 100);
                    break;
                default:
                    textDesc.text = string.Format(data.itemDesc);
                    break;
            }
        }
        else
        {
            switch (data.itemType)                                     // 레벨업에 따른 무기 설명
            {
                case ItemData.ItemType.DualBlades:
                case ItemData.ItemType.HuntingBow:
                case ItemData.ItemType.SniperHBG:
                case ItemData.ItemType.GreatSword:
                case ItemData.ItemType.Lance:
                    textDesc.text = WeaponSpecText();                          // 선택하면 실제로 적용될 최종 능력치
                    break;
                case ItemData.ItemType.Rate_Up:
                case ItemData.ItemType.Speed_Up:
                case ItemData.ItemType.Damage_Up:
                case ItemData.ItemType.Stamina_Up:
                case ItemData.ItemType.Weapon_Upgrade:
                    textDesc.text = string.Format(data.itemDesc,
                        data.damages[level] * 100);
                    break;
                default:
                    textDesc.text = string.Format(data.itemDesc);
                    break;
            }
        }
    }

    // 다음 레벨의 무기 기본 수치 (아이템 데이터 기준, 캐릭터와 능력치 보정 전)
    private void GetNextLevelValues(out float nextDamage, out float nextSpeed, out float nextRate, out int nextCount)
    {
        nextDamage = data.baseDamage + data.baseDamage * data.damages[level];   // 무기 데미지
        nextSpeed = data.baseSpeed + data.baseSpeed * data.W_Speeds[level];     // 무기 회전 속도
        nextRate = data.baseRate + data.baseRate * data.W_Rates[level];         // 무기 연사 속도
        nextCount = data.baseCount + data.counts[level];
    }

    // 레벨업 선택지에 표시할 무기 최종 능력치 (무기의 레벨업 계산과 같은 식을 사용)
    // 필살기처럼 잠깐 적용되는 버프는 포함하지 않음
    private string WeaponSpecText()
    {
        if (weapon == null)
            return data.itemName;

        GetNextLevelValues(out float nextDamage, out float nextSpeed, out float nextRate, out int nextCount);
        Weapon.Stats next = weapon.PreviewLevelUp(nextDamage, nextSpeed, nextRate, nextCount);

        switch (data.itemType)
        {
            // 카드 폭에 맞도록 한 줄을 짧게 (소수점은 한 자리까지)
            case ItemData.ItemType.DualBlades:
                float spinSpeed = next.speed;                                             // 회전 속도 (스테미나는 speed에 한 번만 반영)
                return "공격력 " + Number(next.damage) +
                       "\n공격속도 " + Mathf.RoundToInt(spinSpeed) +
                       "\n쌍검 " + next.count + "개";
            case ItemData.ItemType.HuntingBow:
                return "공격력 " + Number(next.damage) +
                       "\n연사 간격 " + Number(next.rate) + "초" +
                       "\n관통 " + (next.count + ExtraCards.PierceBonus) + "회";   // 관통 화살촉 포함
            case ItemData.ItemType.SniperHBG:
                return "공격력 " + Number(next.damage) +
                       "\n연사 간격 " + Number(next.rate) + "초" +
                       "\n관통 무한";
            case ItemData.ItemType.GreatSword:
                return "공격력 " + Number(next.baseDamage) +
                       "\n공격속도 " + Mathf.RoundToInt(next.speed) +
                       "\n베기 " + next.count + "회" +
                       "\n쿨타임 " + Number(next.rate) + "초";
            case ItemData.ItemType.Lance:
                return "공격력 " + Number(next.baseDamage) +
                       "\n돌진 시간 " + Number(next.speed) + "초" +
                       "\n돌진속도 +" + next.count +
                       "\n쿨타임 " + Number(next.rate) + "초";
            default:
                return data.itemName;
        }
    }

    private static string Number(float value)
    {
        return value.ToString("0.#");
    }

    public void OnClick()
    {
        switch(data.itemType)
        {
            case ItemData.ItemType.DualBlades:
            case ItemData.ItemType.HuntingBow:
            case ItemData.ItemType.SniperHBG:
            case ItemData.ItemType.GreatSword:
            case ItemData.ItemType.Lance:
                if (level == 0)
                {
                    Weapon.RemoveStarter();                         // 처음 얻은 무기가 시작 나무 막대기를 대체
                    GameObject newWeapon = new GameObject();
                    weapon = newWeapon.AddComponent<Weapon>();
                    weapon.Init(data);
                }
                else
                {
                    GetNextLevelValues(out float nextDamage, out float nextSpeed, out float nextRate, out int nextCount);
                    weapon.LevelUp(nextDamage, nextSpeed, nextRate, nextCount);
                }
                level++;
                MainWeaponController.TryDesignate(weapon, level);   // 최초로 메인무기 레벨에 도달한 무기는 메인무기가 됨
                break;
            case ItemData.ItemType.Rate_Up:
            case ItemData.ItemType.Speed_Up:
            case ItemData.ItemType.Damage_Up:
            case ItemData.ItemType.Stamina_Up:
            case ItemData.ItemType.Weapon_Upgrade:
                if (level == 0)
                {
                    GameObject newGear = new GameObject();
                    gear = newGear.AddComponent<Gear>();
                    gear.Init(data);
                }
                else
                {
                    float nextRate = data.damages[level];
                    gear.LevelUp(nextRate);
                }
                level++;
                break;
            case ItemData.ItemType.Heal:
                GameManager.instance.Heal(GameManager.instance.maxHealth);   // 회복 배율 적용 (단식 수행, 배수진)
                SpriteFx.Heal(GameManager.instance.player.transform);
                break;
            case ItemData.ItemType.Magnet:
                GameManager.instance.player.scanner.expScanRange += GimmickCatalog.Instance.netRangeAdd;   // 포획용 그물
                break;
            case ItemData.ItemType.Exp_Coupon:
                Exp_Bonus = 1;   
                GameManager.expbonuscheck = 0;
                break;
            case ItemData.ItemType.Super_Magnet:
                Super_Magnet = 1;
                GameManager.supermagnetcheck = 0;
                break;
            default:
                // 레벨업 추가 카드: 능력치 카드는 레벨 증가, 소모 카드는 즉시 사용 또는 보관
                if (ExtraCards.IsExtra(data.itemType))
                {
                    ExtraCards.Apply(this);
                    if (ExtraCards.IsStatCard(data.itemType))
                    {
                        level++;
                    }
                }
                break;
        }

        if (level == data.damages.Length) 
        {
            GetComponent<Button>().interactable = false;
        }
    }
}
