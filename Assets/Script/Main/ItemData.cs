using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "item", menuName ="Scriptble Object/ItemData")]
public class ItemData : ScriptableObject
{
    public enum ItemType 
    {  
        DualBlades, HuntingBow, SniperHBG , GreatSword, Lance,          // 메인무기
        Rate_Up, Speed_Up, Heal, Damage_Up, Stamina_Up, Weapon_Upgrade, Magnet, Exp_Coupon, Super_Magnet,
        // 아래는 실행 중에 추가되는 레벨업 카드 (ExtraCards). 기존 에셋 번호가 밀리지 않도록 항상 맨 뒤에 추가
        Knockback, WeaponMastery, Pierce, LastStand,            // 능력치 카드
        CoinPouch,                                              // 즉시 발동 소모 카드
        BombBarrel, ParalysisToad, Food,                        // 보관형 소모 카드
        // 전투 기믹 카드 (GimmickCards, 수치는 Resources/GimmickCatalog)
        DodgeCounter, CounterInstinct, Focus, ChainLightning, KillExplosion, HuntInstinct,
        TwinWeapon, Shield, FrostStep, Escape, LifeStrike
    }
    
    [Header("Main Info")]
    public ItemType itemType;
    public int itemId;
    public string itemName;
    [TextArea]
    public string itemDesc;
    public Sprite itemIcon;
    public Sprite maxLevelIcon;     // 무기 최대 레벨 아이콘 (메인무기 전용 스킬 버튼에 사용)

    [Header("Weapon Base Data")]
    public float baseDamage;
    public float baseSpeed;   // 회전 속도
    public float baseRate;    // 연사 속도
    public int baseCount;

    [Header("Weapon LevelUp Data")]   
    public float[] damages;
    public float[] W_Speeds;
    public float[] W_Rates;
    public int[] counts;

    [Header("Weapon Data [/MHS Wepaon Data]")]
    public GameObject projectile;

    [Header("Weapon Another Image")]
    public Sprite[] weaponimage;
}
