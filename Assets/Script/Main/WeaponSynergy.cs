using UnityEngine;

// 무기 시너지 판정
// - 메인 무기가 확정된 뒤, 짝 무기 레벨이 resonanceLevel 이상이면 공명, awakeningLevel 이상이면 각성
// - 메인 확정 직후, 무기를 얻거나 레벨이 오를 때마다 Evaluate()로 다시 판정한다
// - 달성 팝업은 레벨업 창이 닫히고 게임이 진행 중일 때 띄운다 (공명·각성 동시 달성이면 각성 팝업 1회만)
// - 효과는 OnResonance / OnAwakening에서 메인 무기에 켜고, 각 무기 코드에서 확인한다
public class WeaponSynergy : MonoBehaviour
{
    public enum Stage { None = 0, Resonance = 1, Awakening = 2 }

    private static WeaponSynergy instance;
    private static WeaponSynergyData data;
    private static Stage pendingPopup = Stage.None;

    // 이번 판의 시너지 단계
    public static Stage Current { get; private set; } = Stage.None;
    public static bool IsResonance => Current >= Stage.Resonance;
    public static bool IsAwakening => Current >= Stage.Awakening;

    public static WeaponSynergyData Data
    {
        get
        {
            if (data == null)
            {
                data = Resources.Load<WeaponSynergyData>("WeaponSynergyData");
                if (data == null)
                {
                    data = ScriptableObject.CreateInstance<WeaponSynergyData>();   // 에셋이 없으면 기본값
                }
            }
            return data;
        }
    }

    // 지금 메인 무기의 시너지 항목 (메인 무기가 없으면 null)
    public static WeaponSynergyData.Entry CurrentEntry
    {
        get
        {
            MainWeaponController main = MainWeaponController.Current;
            return main != null && main.MainWeapon != null ? Data.Find((ItemData.ItemType)main.MainWeapon.id) : null;
        }
    }

    // 메인 확정 후, 이 무기가 메인 무기의 짝 무기인지 (레벨업 카드 표시용)
    public static bool IsPartner(ItemData.ItemType type)
    {
        WeaponSynergyData.Entry entry = CurrentEntry;
        return entry != null && entry.partner == type;
    }

    // 무기 레벨 (Weapon의 무기별 레벨 변수, 얻지 않았으면 0)
    public static int Level(ItemData.ItemType type)
    {
        switch (type)
        {
            case ItemData.ItemType.DualBlades: return Weapon.DB_level;
            case ItemData.ItemType.HuntingBow: return Weapon.HB_level;
            case ItemData.ItemType.SniperHBG: return Weapon.SH_level;
            case ItemData.ItemType.GreatSword: return Weapon.GS_level;
            case ItemData.ItemType.Lance: return Weapon.LC_level;
            default: return 0;
        }
    }

    // 단계 판정 (단계는 올라가기만 함)
    public static void Evaluate()
    {
        MainWeaponController main = MainWeaponController.Current;
        WeaponSynergyData.Entry entry = CurrentEntry;
        if (main == null || entry == null)
            return;

        int level = Level(entry.partner);
        Stage stage = level >= Data.awakeningLevel ? Stage.Awakening : level >= Data.resonanceLevel ? Stage.Resonance : Stage.None;
        if (stage <= Current)
            return;

        Stage previous = Current;
        Current = stage;

        // 각성은 공명 효과를 유지한 채 추가로 얻음
        if (previous < Stage.Resonance)
        {
            OnResonance(main);
        }
        if (stage == Stage.Awakening)
        {
            OnAwakening(main);
        }

        // 팝업은 가장 높은 단계 하나만 (동시 달성이면 각성 1회)
        if (stage > pendingPopup)
        {
            pendingPopup = stage;
        }
        EnsureInstance();
    }

    // 새 판 시작 시 초기화
    public static void ResetRun()
    {
        Current = Stage.None;
        pendingPopup = Stage.None;
    }

    // 공명 효과 적용 지점: 메인 무기에 공명 효과를 켬 (실제 동작은 각 무기 코드에서 확인)
    private static void OnResonance(MainWeaponController main)
    {
        main.EnableResonance();
    }

    // 각성 효과 적용 지점: 메인 무기에 각성 효과를 켬 (공명 효과는 유지)
    private static void OnAwakening(MainWeaponController main)
    {
        main.EnableAwakening();
    }

    private static void EnsureInstance()
    {
        if (instance == null)
        {
            instance = new GameObject("Weapon Synergy").AddComponent<WeaponSynergy>();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    // 레벨업 창 등이 닫히고 게임이 진행 중일 때 대기 중인 팝업을 띄움
    private void Update()
    {
        if (pendingPopup == Stage.None || SynergyPopup.IsOpen)
            return;

        GameManager gm = GameManager.instance;
        if (gm == null || !gm.isLive || (LevelUp.instance != null && !LevelUp.instance.LevelUpFinish))
            return;

        WeaponSynergyData.Entry entry = CurrentEntry;
        Stage stage = pendingPopup;
        pendingPopup = Stage.None;
        if (entry != null)
        {
            SynergyPopup.Show(entry, stage);
        }
    }
}
