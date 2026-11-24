using UnityEditor;
using UnityEngine;

// 메인무기 데이터 5종 만들기 (Resources/MainWeapon/MainWeapon_0~4)
// 값 = 1119 데이터 분리 직전, 메인무기 전환 직후 실제로 쓰이던 값 (아이루, 카드·상점·젬 0)
// 성장(5레벨까지의 회전 속도·연사 간격 성장)과 공격속도 상한 2배를 기본 시간에 녹이고 성장 배율은 1.0
// 이미 있는 에셋은 덮어쓰지 않음 (인스펙터에서 조정한 값 보존)
public static class MainWeaponDataSetup
{
    private const string Folder = "Assets/Resources/MainWeapon";

    [MenuItem("MonNyang/메인무기 데이터 만들기")]
    public static void Run()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Resources", "MainWeapon");
        int made = 0;
        for (int id = 0; id < 5; id++)
        {
            string path = Folder + "/MainWeapon_" + id + ".asset";
            if (AssetDatabase.LoadAssetAtPath<MainWeaponData>(path) != null) continue;
            MainWeaponData d = ScriptableObject.CreateInstance<MainWeaponData>();
            d.weaponId = id;
            Fill(d, id);
            AssetDatabase.CreateAsset(d, path);
            made++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[메인무기 데이터] 새로 만든 에셋 " + made + "개 (" + Folder + ")");
    }

    private static void Fill(MainWeaponData d, int id)
    {
        d.attackSpeedGrowth = 1f;
        d.cooldownGrowth = 1f;
        switch (id)
        {
            case 0: // 쌍검: 공격속도 성장 3.0 → 상한 2.0, 쿨타임 성장 1.0
                d.baseDamage = 27.5f;
                d.slashTime = 0.18f / 2f;
                d.stepDelay = 0.2f / 2f;
                d.spinDuration = 1f / 2f;
                d.spinSpeed = 1080f * 2f;
                d.comboCooldown = 0.8f;
                d.dashCooldown = 6f;
                break;
            case 1: // 활: 연사 간격 성장 0.70 (화살 간격에만), 쿨타임 성장 1.0
                d.baseDamage = 28f;
                d.fireInterval = 0.45f * 0.7f;
                d.pierce = 1;
                d.fanChargeTime = 3f;
                d.rainCooldown = 10f;
                break;
            case 2: // 헤비보우건: 공격속도 성장 1.0, 쿨타임 성장 0.65 (탄 충전)
                d.baseDamage = 129f;                         // 메인 탄 피해에는 쓰지 않음 (시너지 짝 피해 대체값)
                d.heavyDamage = 150f;
                d.rapidDamage = 10f;
                d.heavyInterval = 1f;
                d.rapidPerSecond = 10f;
                d.heavyRechargeTime = 5f * 0.65f;
                d.rapidRechargePerSecond = 5f / 0.65f;       // 기존 계산 그대로 (중량탄은 곱, 경량탄은 나눔 - 버그 후보)
                d.rapidPierce = 0;
                d.sniperCooldown = 30f;
                d.sniperMobDamage = 500f;
                d.sniperBossDamage = 150f;
                break;
            case 3: // 대검: 공격속도 성장 2.2 → 상한 2.0, 쿨타임 성장 0.5
                d.baseDamage = 53.5f;
                d.slamRaiseTime = 0.14f / 2f;
                d.slamStrikeTime = 0.08f / 2f;
                d.sweepTime = 0.28f / 2f;
                d.upperSwingTime = 0.1f / 2f;
                d.justRecoverTime = 0.2f * 0.5f;
                d.failRecoverTime = 0.5f * 0.5f;
                d.nextAttackDelay = 1f * 0.5f;
                d.chargeCooldown = 8f;
                break;
            case 4: // 랜스: 공격속도 성장 없음, 쿨타임 성장 0.7
                d.baseDamage = 28.125f;
                d.thrustTime = 0.15f;
                d.thrustInterval = 0.08f;
                d.thrustComboCooldown = 0.8f * 0.7f;
                d.gsComboCooldown = 0.6f * 0.7f;
                d.zoneCooldown = 15f;
                break;
        }
    }
}
