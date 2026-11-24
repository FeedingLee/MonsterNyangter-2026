// 필살기로 적용되는 일시 배율
// 무기와 플레이어의 원본 수치를 바꾸지 않으므로 레벨업과 겹쳐도 해제 시 원래 상태로 돌아간다.
// 테스트 모드가 켜져 있으면 테스트 모드의 배율이 추가로 곱해진다.
public static class PlayerBuff
{
    private static float moveSpeed = 1f;
    private static float stamina = 1f;
    private static float rateScale = 1f;
    private static float damage = 1f;

    // 이동속도 배율 (상점 강화, 아침 선택지 포함)
    public static float MoveSpeed { get => moveSpeed * TestMode.MoveSpeedScale * RunStats.Get(RunStat.MoveSpeed) * ExtraCards.MoveScale; set => moveSpeed = value; }

    // 스테미나(무기 회전, 돌진 유지) 배율
    public static float Stamina { get => stamina * TestMode.AttackSpeedScale * RunStats.Get(RunStat.AttackSpeed) * GimmickCards.AttackSpeedScale; set => stamina = value; }   // 반격 본능 포함

    // 공격 쿨타임 배율 (작을수록 빠름)
    public static float RateScale { get => rateScale * TestMode.CooldownScale * RunStats.Get(RunStat.Cooldown); set => rateScale = value; }

    // 공격력 배율
    // (배수진 발동, 음식 섭취 포함)
    // 피해 증가(공격력 능력치·한계 돌파·음식·집중)는 합산 후 한 번 곱함 (GimmickCards.IncreaseSum). 필살기 버프·테스트 모드는 따로 곱함
    public static float Damage { get => damage * TestMode.DamageScale * (1f + GimmickCards.IncreaseSum); set => damage = value; }

    // 테스트 모드 보정을 뺀 필살기 배율 (테스트 모드 패널 표시용)
    public static float RawMoveSpeed => moveSpeed;
    public static float RawStamina => stamina;
    public static float RawRateScale => rateScale;
    public static float RawDamage => damage;

    public static void Reset()
    {
        moveSpeed = 1f;
        stamina = 1f;
        rateScale = 1f;
        damage = 1f;
    }
}
