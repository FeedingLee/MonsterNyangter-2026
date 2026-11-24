# 작업 인계 (HANDOFF)

## 테스트 후 지워야 할 임시 항목
캠프 비비기 처벌이 잘 작동하는 것을 확인하면 아래를 지운다.
- `Assets/Script/Main/CampLoiterDebug.cs` 파일 전체 (경고 영역 붉은 테두리, 상점 예외 영역 파란 테두리, 화면 왼쪽 확인용 수치)
- `CampLoiterPenalty.Create`의 `go.AddComponent<CampLoiterDebug>();` 한 줄
- `TestMode.cs`의 "캠프 처벌 확인창 열기/닫기" 버튼 (`CampLoiterDebug.Visible`을 쓰므로 위 파일을 지우면 같이 지워야 컴파일됨)
- `TestMode.cs`의 '[임시] 군락 생성 제외 구역 표시' 토글과 `ColonySystem.ShowZones`·`UpdateZones` (확인 끝나면 삭제)

## 캠프 비비기 처벌 (CampLoiterPenalty)
- 경고 영역은 캠프 바닥 타일(이름이 `loiterZoneTilePrefix` "TX Tileset Grass"로 시작) 전체 범위다. 타일을 못 찾으면 출입 금지 영역 + `loiterZoneWidth`를 쓴다.
- 낮에 경고 영역 안에 머문 시간을 누적한다. 밖에 있어도 줄지 않는다.
- 그날 n번째 처벌 기준(`loiterThresholds`: 20 → 10 → 5초)에 닿으면 처벌이 발동하고 게이지가 `penaltyStartGauge`(10)가 된다.
- 처벌 중에는 영역 안에서 게이지가 초당 +1, 밖에서 초당 −0.5이고, 상한은 30이다. 0이 되면 해제되고 누적은 처음부터 다시 쌓인다.
- 게임 시작 유예(`loiterStartGrace` 30초): 캠프 안에서 시작하므로, 처음 영역을 벗어나거나 30초가 지날 때까지 쌓지 않는다.
- 밤과 보스전에는 쌓이지 않는다. 밤이 되는 순간 누적, 게이지, 처벌, 처벌 횟수가 모두 초기화된다.
- 상점 이용 거리 안(`UpgradeShop.PlayerInRange`)은 예외다.
- 처벌 효과는 일반 몬스터(Enemy)에만 적용된다.
  - 머리 위 Enemy_Eye 애니메이션을 표시한다.
  - 발동할 때 powerup_25 효과음을 재생한다.
  - 소환 위치를 화면 바로 밖으로 당긴다(`penaltySpawnMargin`).
  - 화면 밖 몬스터를 플레이어가 캠프를 도는 방향 앞쪽 화면 밖으로 옮긴다.
- 처벌을 무시하고 버티면 스택이 쌓인다.
  - 처벌 중 영역 안에 있던 시간 10초마다 1스택이고, 최대 5스택이다.
  - 스택당 새로 소환되는 일반 몬스터의 최대 체력이 +20%다.
  - 스택에 비례해 몸이 붉어지고 눈이 커진다.
  - 처벌이 해제되면(밤 포함) 강화된 몬스터의 추가 체력과 붉은 색이 `penaltyRecoverTime`(4초)에 걸쳐 원래대로 돌아간다. 남은 체력 비율은 유지된다. 처벌 뒤 못 잡는 몬스터 때문에 레벨업이 막히는 악순환을 막기 위해서다.
  - 스택은 처벌이 해제될 때와 밤에 초기화된다.
- 발동 효과음은 powerup_1이다.
- 테스트 모드에 "캠프 처벌 +10초", "버틴 시간 +10초", "메인무기 제거" 버튼이 있다.
- 수치는 RunConfig의 "캠프 비비기 처벌" 항목에서 조절한다.

## 설계 결정
- 보스는 텐트 울타리를 통과한다. CampZone이 보스와 벽의 충돌을 끄고 밀어내지 않으며, 보스는 우회하지 않는다. 보스 돌진은 원래 방식(예고가 끝날 때 플레이어 쪽으로)이다.
- 중간보스 케스킹 돌진:
  - 울타리에 가려 있으면 돌아 들어가며 기다리고, 오래 가려져 있으면 모서리로 질러간다.
  - 모서리 돌진 뒤에는 쿨타임 없이 짧은 예고로 바로 이어서 돌진한다.
  - 예고 중 울타리 뒤로 숨으면 벽을 타고 들이받는다.
  - 예고 화살표는 실제 돌진 범위(몸 앞부분 기준)에 맞춰 그린다. `chargeDistance`는 11이다.
- 울타리 그림은 씬에 직접 배치한다. 출입 금지 영역은 그 배치에 자동으로 맞춘다(`fitAreaToFence`). 앞뒤 정렬은 FenceDepthSort가 맡는다.

- 일반 몬스터와 중간보스는 이동할 때 `CampZone.SlideAround`로 울타리 벽을 따라 미끄러진다(몸 반지름 + `campSlidePadding`). 벽을 정면으로 밀면 가까운 모서리 쪽으로 돌아 나간다. 벽 충돌체에 몸을 밀어 넣거나, 모서리에서 가장 가까운 변으로 되밀려 제자리에 끼는 문제를 막기 위해서다.
- 문 애니메이션은 문틀 그림이 프레임마다 조금씩 달라 꿈틀거려 보인다. 그래서 중간 프레임 하나만 쓴다(열림: Open_2 → Open_4, 닫힘: Close_2 → Close_4, 12fps).

## 헤비보우건 잔탄 UI
- 캐릭터 아래 게이지는 잔탄을 보여 줄 때 네모난 탄창 모양이다(테두리 안에 칸). 중량탄은 한 칸이 한 발이고, 연사탄은 `rapidSegments`(10)칸이다. 탄 변경 진행이나 쿨타임처럼 다른 것을 보여 줄 때는 기존 둥근 막대를 쓴다.
- 왼쪽 잔탄 표시는 네모난 판(금색 겉테 + 안쪽 선, `HeavyAmmoHud.MagazineSprite`)이고, 아래에 탄창 칸이 있다. 랜스 스택 표시는 기존 둥근 판(`PillSprite`) 그대로다.

## 맵 나뭇잎 (MapLeafFall)
- 게임을 시작할 때 `RunConfig.mapLeafSets` 5종(분홍 벚꽃잎, 흰 벚꽃잎, 초록 잎, 노란 잎, 꽃송이) 중 하나를 무작위로 골라, 그 판 내내 그 종류만 화면에 흩날린다.
- `leaf_effect.png`에 cherry, whitepetal, ginkgo, blossom 줄을 추가로 잘랐다(메타에 항목만 덧붙였고, 기존 green ID는 유지).

## Grass_path 타일
- `MHS IMAGE/Grass_path.png`를 20칸(`Grass_path_0~19`, 시트 왼쪽 위부터)으로 잘랐다(PPU 223, Point, 압축 없음).
- 타일 에셋은 `Tile Palette/Grass Path/`에 있고, TP Grass 팔레트의 기존 타일 아래 줄에 추가했다.
- 그림이 가로로 조금 길어(223 x 약 208px) 타일 에셋의 transform으로 정확히 1 x 1칸에 맞췄다(세로 약 7% 늘림).

## 길 타일 2x2 (Pal 팔레트)
- `Object_Ground_Path.png`의 _2와 _3을 4등분해 `Object_Ground_Path_2_2x2_BL/BR/TL/TR`, `_3_2x2_*` 스프라이트를 추가했다. 메타에 항목만 덧붙였고 기존 ID는 유지했다.
- 타일 에셋 transform으로 1칸 크기에 맞추고(_2는 3.33배, _3은 2배) LockTransform을 걸었다. 그래서 팔레트에서 회전되지 않는다(사용자 결정: 회전 불필요).
- 타일 에셋은 `MHS Tile/Path/길 직선 A·B 2x2 (왼쪽 아래 등).asset`이다. Pal 팔레트의 기존 길 타일 오른쪽에 2x2 묶음으로 놓았다(A: x 5~6, B: x 8~9). 네 칸을 함께 선택해 칠하면 된다.

## 헤비보우건
- 탄을 다 써도 자동으로 다른 탄으로 바뀌지 않는다(우클릭으로만 변경).

## 타이틀 메뉴 (TitleMenu, 2026-11-17)
- Title 씬이 열리면 `TitleMenu`가 자동 생성된다(씬 파일 수정 없음). 기존 Title UI(클릭하여 시작하기, 예전 로고, 화면 전체 시작 버튼, BlackOut)는 씬에서 지웠다. Title 씬의 Canvas 아래에는 Title Bgm과 Title BG만 남아 있고 둘 다 필요하다. 배경·태양 빛·나뭇잎은 기존 TitleAmbience 그대로다.
- 흐름: 메인 메뉴(플레이·설정·나가기) → 캐릭터 선택 → 스테이지 선택 → 출발(페이드 후 Main 씬). ESC와 [돌아가기]로 한 단계씩 뒤로 간다.
- 데이터와 수치는 `Assets/Resources/MenuCatalog.asset`에 있다. 캐릭터 문구·능력치 점수, 스테이지 목록, 설정 탭, 기본 볼륨, 배치, 색, 고지 문구를 여기서 바꾼다. 스프라이트는 메뉴 MonNyang > 메뉴 UI 에셋 설정으로 다시 연결한다. 판·배너·버튼·슬라이더는 `Panel_1.png`, 아이콘(돌아가기·확인·출발·기본값·탭·자물쇠·능력치 칸)은 `Icon_1.png`를 잘라 쓴다(MenuAssetSetup의 PanelParts/IconParts). Morning_card에도 메뉴용 조각을 추가했다(기존 ID 유지).
- 메인 메뉴: 로고는 가운데 위, 버튼 3개는 오른쪽, 아래 가운데에 고지 배너("몬스터냥터는 비공식 팬 게임으로, CAPCOM과 무관합니다.").
- 캐릭터 그림은 모든 프레임에 같은 배율을 쓰고 발(그림 아래)을 같은 자리에 맞춘다. Stand 애니메이션 중 한 프레임만 위로 5px 길어서, 칸 맞춤(preserveAspect)이나 가운데 맞춤이면 몸이 눌리거나 발이 들썩인다.
- 해금 여부는 AchiveManager와 같은 PlayerPrefs 키(UnlockGaza/UnlockBowa/UnlockWigller)로 읽는다. 잠긴 캐릭터는 실루엣과 이름, 해금 조건만 보이고 [다음]이 막힌다.
- 스테이지는 1번 '깊은 숲 캠프거점'만 플레이할 수 있다. 대표 이미지·설명·권장 레벨·몬스터·보상은 데이터가 없어 비어 있고, 채우면 화면에 나온다. 별점·평가 UI는 만들지 않는다.
- 스테이지 1 대표 이미지는 `Stage_Image_1.png`(3:1). 이미지 칸을 꽉 채우고 비율이 다르면 넘치는 부분을 자른다(AspectRatioFitter).
- companyName은 AILEE가 맞다(의도된 변경). PlayerPrefs 저장 위치가 바뀌어 예전 해금·볼륨 저장 값은 새 위치에 없다.
- 설정은 사운드 탭(배경음·효과음)만 내용이 있다. 저장 키는 기존 AudioSettings와 같다(BgmVolume/SfxVolume). 슬라이더는 바로 들리고 [적용]해야 저장된다. [기본값]은 배경음 50% / 효과음 50%로 되돌리고, 돌아가기는 저장하지 않은 변경을 취소한다. 인게임 AudioSettings도 저장 값이 없으면 같은 기본값을 쓴다.
- 배경음은 저장 값(슬라이더 %)과 실제 소리를 분리했다. 인게임 실제 볼륨 = 설정값 x `bgmOutputScale`(1.4, 50%일 때 0.7), 타이틀 = 설정값 x `titleBgmOutputScale`(1.26, 50%일 때 0.63 = 예전 0.7의 90%, 타이틀 곡만 낮춤).
- 게임 시작 연결(`GameLaunch`): 출발하면 선택이 저장되고, Main 씬이 열리면 기존 캐릭터 버튼(Character N)의 클릭을 그대로 실행한다. 선택은 그만두기 전까지 유지되므로 다시하기는 같은 캐릭터로 바로 처음부터 시작한다. Main 씬을 에디터에서 바로 실행하면 선택이 없어 기존 캐릭터 선택 화면이 나온다(테스트용).
- 옵션 창의 해골 버튼(Object_Panel_28) = 포기하기 (2026-11-18 변경): 확인 창(`ConfirmPopup`) 뒤 옵션을 닫고 체력을 0으로 만들어 사망과 같은 실패 흐름(Quest Failed → 결과 화면)으로 간다. 결과 화면의 다시하기가 캐릭터 선택으로 간다.
- 캐릭터 이름은 미케루·치카부·도스도스·휘청휘청으로 통일했다(Main 씬의 해금 알림과 기존 선택 화면 문구 포함).

## 능력치 통합 · 상점 3지선다 (2026-11-18)
- 능력치 공통 정의는 `Assets/Resources/StatCatalog.asset`(StatCatalog.cs)이다. 16종의 이름·분야·단위·증가/감소, 상점 등급별 수치 14종, 골드 특수 선택지 9종, 가격(30 / +3 / 최대 60), 등급 확률(60/30/10), 특수 확률 0.3, 하한·상한(attackCdFloor 0.4, skillCdFloor 0.5, coinGainCap 0.4), 상점 UI 그림·배치를 모두 여기서 조정한다. 스프라이트는 메뉴 MonNyang > 상점 UI 에셋 설정으로 다시 연결한다.
- 능력치 종류는 따로 StatType을 만들지 않고 기존 `RunStat`에 SubWeaponDamage, DamageTaken, WeaponSize, MaxHealthFlat, StartCoins를 추가했다.
- RunStats는 출처(상점·젬·특수 젬·아침 선택지)별로 값을 쌓고 규칙대로 계산한다. 증가형은 모든 출처를 합산한 뒤 한 번 곱하고, 감소형은 출처 안에서 합산한 뒤 출처끼리 곱한다. 최대 체력은 (100 + 고정값) x 배율이다. 아침 선택지의 `Multiply`는 16종이면 변화량(1.08 → +8%)으로 바꿔 넣고, 그 외 능력치(스폰·엘리트·보스·밤)는 예전처럼 곱한다.
- 적용 지점: 보조무기 피해는 `Bullet.HitDamage`(보조무기가 Init 뒤 `SubWeapon` 표시), 받는 피해는 `MainWeaponController.DamageTakenScale`, 무기·총알 크기는 `Bullet.ApplyWeaponSize`(보조무기와 메인무기 기본 공격만, 전용 스킬 제외), 최대 체력은 `DayNightCycle.ApplyMaxHealth`.
- 필살기 위력·시작 냥냥코인은 젬 전용이라 정의만 있다(시작 코인 지급 지점은 젬 작업 때).
- 상점(UpgradeShop): 열면 카드 3장이 스르륵 올라오고, 카드를 고른 뒤 [선택]을 누를 때 냥냥코인을 낸다. 나갔다 와도 같은 카드가 남아 리롤이 되지 않고, 사면 새 3장이 나온다. 같은 능력치·같은 등급은 한 번에 겹치지 않는다. 판넬·고양이·랜턴·코인 띠는 각각 독립 UI다(Ingame_shop 시트, 자는 고양이는 안 씀). 효과음은 SfxManager가 없어 RunConfig의 shopOpen/Buy/Click 슬롯을 쓴다.
- 예전 상점 항목(ShopItemDef, RunConfig.shopItems/shopIcons/shopPageSize)은 지웠다. 자연 회복·큰 그물은 상점에서 빠졌고, 아침 선택지가 쓰는 Regen·ExpRange 능력치는 남아 있다.
- 테스트 모드에 "최종 능력치 확인 (출처별)" 토글이 있다.

## 레벨업 개편 · 전투 기믹 (2026-11-18)
- 선택 규칙 (LevelUp.Next): 17의 배수 = 소모품만 → 5의 배수 = 무기만 → 짝수 = 무기 1장 보장 + 무작위 / 홀수 = 모두 무작위. 무작위 칸은 consumableSlotRate(0.15) 확률로 소모품, 아니면 무기·기믹 중 하나. 낼 카드가 없으면 소모품으로 채움.
- 레벨업 선택지에서 뺀 카드(데이터는 유지): 공격력·쿨타임·스태미나·이동속도 증가, 무기 강화, 무기 숙련 (`LevelUp.IsRemoved`).
- 전투 기믹 14종: 기존 밀어내기·관통 화살촉·한계 돌파 + 신규 11종(`ItemType` 끝에 추가). 효과는 `GimmickCards`, 매 프레임 처리·이펙트는 `GimmickRunner`, 수치·그림·레벨업 화면 배치는 `Resources/GimmickCatalog.asset`(메뉴 MonNyang > 레벨업 UI 에셋 설정).
- 피해 쪽 기믹은 발사체·범위 피해가 들어가기 직전 `GimmickCards.ModifyHit`, 들어간 뒤 `AfterHit`로 처리한다. 보스 피해는 기존 TakeDamage(1회 상한)를 그대로 거친다. 메인무기 확정 후에는 보조무기(`Bullet.SubWeapon`) 공격이 회피 반격·생명의 일격을 쓰지 않는다.
- 보호막은 `Player.TryBlockHit`(랜스 패링 다음)과 말벌 접촉에서 막고, 위기 탈출 무적은 `Player.IsInvincible`에 포함된다. 반격 본능은 무기 5종 우클릭 동작(쌍검 백대시, 활 부채꼴, 헤비보우건 탄 변경, 대검 저스트 입력, 랜스 가드)에서 발동한다.
- 레벨업 화면은 `LevelUpView`(1920x1080 새 캔버스)가 그리고, 기존 LevelUp 판넬·카드는 데이터·클릭 처리용으로 투명하게 남아 있다. 카드를 고르면 기존 카드 버튼 클릭을 그대로 실행한다. 카드 이름표 색은 무기·소모품·기믹으로 구분한다.
- 이펙트: 보호막·냉기 발자국 = Shield&ice_effact, 연쇄 번개 = lightning_effect(lightningLifetime 뒤 삭제). 슈퍼 그물망 아이콘은 Object_Net의 붉은 복사본 `Object_Net_Red.png`(포획용 그물은 원래 색).
- 테스트 모드: "이 레벨로 레벨업", "전투 기믹 카드 올리기".
- 생명의 일격은 준비된 뒤 처음 적중한 판정 1개에만 들어간다: 관통 투사체면 그 1발이 맞힌 적 모두(`Bullet.ShotId`), 범위 공격이면 그 1회에 맞은 적 모두, 여러 발이면 처음 맞은 1발만.
- 보호막·냉기 이펙트는 시트 프레임마다 그림 위치가 달라 넘기면 좌우로 꿀렁거려서 한 장만 쓰고 투명도로 숨쉬게 했다. 냉기는 몬스터 바로 뒤에 그리고 몸을 푸르스름하게 칠한다(풀리면 원래 색).
- 활성 기믹 표시(`GimmickBuffBar`): 한계 돌파·위기 탈출·생명의 일격·집중 중 보유한 것만 머리 위에 가로 나열. 우클릭 쿨타임 표시(`OverheadChargeUI.Active`)가 보이면 그 위, 없으면 캐릭터 위 buffHeadOffsetY. 상태는 아이콘 진하기(buffAlphaMin~Max).

## 피해 규칙 · 넉백 (2026-11-18)
- 피해 증가(공격력 능력치·한계 돌파·음식·집중·회피 반격·사냥 본능)는 합산 후 한 번 곱한다: 기본 x (1 + 합). 항상 붙는 것은 `GimmickCards.IncreaseSum`(PlayerBuff.Damage에 반영), 적중마다 붙는 회피 반격·사냥 본능은 `AddIncrease`로 같은 합에 더한다. 필살기 버프·테스트 모드 배율, 보조무기 피해 배율은 따로 곱한다. 생명의 일격 추가 피해에는 증가가 붙지 않는다.
- 정밀 저격·강철의 영역은 `CombatUtil.DamageInCircleSplit`(공통 규칙: 공격력·기믹, 번개 적중 수, 처치 폭발, 보스·중간보스 1회 상한)을 쓴다.
- 처치 폭발 기준 피해는 생명의 일격 추가분을 뺀 값. 각성 시너지 피해는 `GimmickCards.SynergyDamage`(피해 증가만). 연쇄 번개에도 사냥 본능이 붙는다.
- 넉백은 공격별 확률(`GimmickCatalog` knock* 값)로 발동하고 거리 = knockbackBaseDist x (1 + 밀어내기 단계). 같은 몬스터는 knockbackCooldown(0.5초) 안에 다시 밀리지 않는다. 보스는 면역, 중간보스는 확률 x miniBossKnockbackMul. 확률은 `Bullet.KnockChance`(지정 없으면 메인무기 좌클릭 값)와 `CombatUtil` 함수의 knockChance로 전달한다. 연쇄 번개·각성·폭탄 통은 넉백 없음.
- 보조 활 인식 범위 = scanRange x `Scanner.subBowRangeScale`(1.5).

## 캐릭터 강화 (젬) 1차 (2026-11-18)
- 저장: `GemSave` → `Application.persistentDataPath/monnyang_save.json` (일반 젬 16종 개수, 슬롯 9칸, 클리어 횟수, 특수 젬·조각 자리). 장착·해제·합성·결과 보상 때마다 저장. 수치·그림은 `Resources/GemCatalog.asset`(메뉴 MonNyang > 젬 UI 에셋 설정).
- 장착한 젬은 `DayNightCycle.StartRun`에서 '젬' 출처로 적용(시작 리롤·시작 냥냥코인은 바로 지급).
- RunStats: 이득은 모든 출처 합산 후 1회 곱, 손해(증가형의 음수, 감소형의 양수: 상점 특수 선택지 대가·특수 젬 감소 옵션·아침 선택지 리스크)는 하나씩 따로 곱한다.
- 결과: 게임 종료 시 `GameResultView.Show`가 젬 지급과 클리어 횟수 증가 후, Quest Clear·Failed 아이콘(제자리 페이드, Failed는 `resultFailedIconHeight`로 더 크게) + '결과 보기' → 결과 패널(생존 시간·버틴 밤·처치 수·보스 처치·획득 젬·특수 젬/조각 자리). 다시하기 = 타이틀 캐릭터 선택, 나가기 = 타이틀 메인. 기존 GameResult는 흐름용으로 켜 둔 채 투명.
- 젬 보상 (2026-11-18 수정): 클리어 `gemRewardClear`(4). 실패(사망·포기하기 같은 계산) = 버틴 밤 / `failNightsPerGem`(3) 버림, 최대 `failGemMax`(2). 버틴 밤 = `DayNightCycle.NightsSurvived`(밤이 끝나고 아침을 맞을 때 +1, 보스 등장 뒤 증가 없음). 특수 젬 드롭: 클리어 `specialDropClear`(10%), 실패는 버틴 밤 `specialDropMinNights`(3) 이상일 때 `specialDropFail`(3%).
- 낮 75초 / 밤 60초, 보스는 게임 시간 600초(Main 씬 `maxGameTime`) = 5일차 낮 60초쯤 등장 → 끝없는 밤 없이 버틸 수 있는 밤은 최대 4회(실패 보상 최대 1개). 2개는 끝없는 밤으로 밤이 늘어야 가능.
- UI 에셋 (2026-11-18): 하위 화면 테두리 = `Menu_Frame_P2.png`(Panel_2 나무 액자 복사본, 가운데 판·안쪽 양피지 제거), 제목판 = Panel_2 잎 장식판에서 발바닥 혹을 지운 복사본(`UI_Clean_Parts.png`, 비율 유지, `headerTextOffset`). 결과 목재 판도 같은 파일의 복사본(위 가로대의 잘린 판 제거), 메뉴·젬 버튼 = Panel_2 금테 버튼. 젬 양피지 판 = `Gem_Paper_Clean.png`(Gem_shop_panel 복사본: 띠의 다이아몬드 제거판 / 띠 없는 판). 제목은 판 위 띠 안(`paperBandCenter`), 발바닥 띠는 비율 유지. 젬 관리 왼쪽은 '보석 전문가'(Gem_shopper 18프레임, 발 위치 기준으로 잘라 좌우 흔들림 없음, 오른쪽을 봄, 앞에 Table.png 테이블 `shopperTable*`). 대기는 한 그림(`shopperIdleFrame`)에 멈춘 채 숨쉬기만 하고(크기 변화는 픽셀이 꾸물거려 쓰지 않고, `shopperBreathPeriod` 3.2초에 `shopperBreathPixels` 2px를 정수 픽셀로 위아래), `shopperIdleTime`(5~9초)마다 `shopperActions`(인사·젬 자랑·고민·반짝) 중 하나를 `shopperFps`(8) 속도로 한 번 재생한다(같은 동작 연속 없음). 대기 그림 6장을 계속 돌리면 포즈 차이 때문에 뚝뚝 끊겨 보여서 바꿨다. '장착 효과'·'획득한 젬' 띠는 판 왼쪽 위 모서리에 걸침. 원본 이미지는 그대로.
- 보스전에는 시간 제한이 없다(보스 처치 또는 사망까지). 보스 등장 후 낮·밤이 멈춰 일차도 오르지 않는다.
- 타이틀: '젬 관리'(플레이 아래) → 캐릭터 강화 메인(슬롯 9칸·특수 3칸 자리·장착 효과·보관함/합성/재설정·분해 비활성) / 보관함(탭, 개수, E, 장착·해제) / 일반 젬 합성 1+1(장착 중 제외, 결과 분야 선택). 슬롯은 분야별 처음 1칸, 클리어마다 1칸.
- 테스트 모드: 젬 +5, 젬 전종 +1, 클리어 횟수 0/1/2, 저장 초기화.
- 보관함 '전체 해제' 버튼(`GemSave.UnequipAll`), 선택한 젬 이름은 한 줄(넘치면 글자 크기만 줄임).

## 캐릭터 강화 2차: 특수 젬 (2026-11-18)
- 데이터: `GemSave.SpecialGem`(id, 증가 옵션·정수 수치, 감소 옵션·정수 수치, 잠금). 저장 `specials`, `specialSlots`(id), `shards`. 옵션 범위 22개는 `GemCatalog.specialUpOptions / specialDownOptions`(from·to 정수, scale: Percent 0.01, 최대 체력 1).
- 생성: 증가 11종 균등 → 감소는 같은 능력치를 뺀 종류 중 균등. 옵션 종류는 이후 바뀌지 않음. 이름 = 증가 옵션 이름 + " 특수 젬", 그림 = 증가 옵션 분야(Unique_Gem_1, 광채 포함 원본 그대로).
- 획득: 특수 젬 합성(장착하지 않은 일반 젬 `specialCombineCount`개, 조각 비용 없음) / 결과 드롭. 구매 불가.
- 장착: 슬롯 `specialSlotCount`(3), 처음 1칸 + 클리어마다 1칸. 같은 증가 옵션은 함께 장착 불가. 게임 시작 때 '특수 젬' 출처로 적용, 감소는 RunStats 규칙대로 따로 곱(공격속도 상점 +20% · 특수 -7% → 1.2 x 0.93 = 1.116 확인).
- 재설정: 기본 `rerollCost`(10) 두 줄 모두, 한 줄 잠금(한 번에 한 줄) 시 x`lockCostMul`(2). 분해: 1개당 `shardPerGem`(5), 여러 개 선택 + 확인 창. 잠금·장착 중인 젬은 분해 불가.
- 화면: 메인 특수 칸 3개·장착 효과(감소는 빨간 글자)·오른쪽 위 조각, 보관함 '특수' 탭(두 줄 카드, 잠금 버튼), 합성 탭 전환, 재설정·분해 화면(`TitleMenu.GemSpecial`). 레퍼런스 이미지와 다른 값(조각 30, 소수 수치)은 지시서대로.
- 테스트 모드: 특수 젬 +1 생성, 조각 +50.
- 장착 효과는 능력치마다 인게임과 같은 계산의 최종값 한 개(`TitleMenu.NetEffect`, 퍼센트는 소수 첫째 자리). 감소 옵션이 섞이면 빨간 글자. 예: 경험치 +18% 와 -15% → 1.18 x 0.85 = +0.3%.
- 특수 젬 칸: 장착하면 빈 칸 그림을 숨기고 같은 크기로 젬 그림이 대신 들어감. `gp_slot_special_frame`은 보라 광채까지 자름(432,680 ~ 625,898).
- 분해 '일괄 분해': 잠금·장착 중인 젬을 뺀 전부를 골라 확인 창. 특수 합성 결과 연출은 뱃지 뒤 금빛 고리 + 톡 커짐(`UiPop`) + 가장자리 반짝임(`UiFx.Play(..., behind: true)`는 대상 바로 뒤에 그림).
- 결과 화면 다시하기·나가기는 클릭 소리 없음. 포기 확인 문구 "정말로 포기할 거냥?". 테스트 패널 폭 `TestMode.panelWidth`(470), 버튼 많은 줄은 `buttonsPerRow`(3)개씩.
- 경고 문구 규칙: 본문과 떨어진 줄에 빨갛고 조금 작게 (`GemCatalog.warningColor / warningFontScale / warningGap`). `ConfirmPopup.Show(본문, 경고, 확인 글자, ...)`는 판 높이를 줄 수에 맞춰 자동 계산. 합성 화면 "장착 중인 젬은 재료로 쓸 수 없습니다", 보관함 장착 불가 이유도 같은 방식.
- 특수 합성 ?: `Gem_Unknown_Special_Split.png`(Gem_Icon의 ? 특수 젬을 엠블렘 / 보라 광채로 나눈 복사본). 합성 전에는 광채 없는 ?, 합성하면 결과 뒤에서 광채가 피어난 뒤 은은하게 남음(`UiGlowBloom`, `specialGlow*` 수치).

- 특수 젬 그림: 광채 포함(`specialAttack` 등)은 메인 화면 장착 칸에만, 그 외(보관함·합성·재설정·분해·결과)는 광채를 지운 복사본 `Unique_Gem_Plain.png`(`SpecialIcon` = 광채 없음, `SpecialIconGlow` = 포함). 재설정 화면 반짝임은 증가 옵션이 최대치(`to`)일 때만. 합성 광채 `specialGlowSize` 230.

## 필살기 위력 제거 · 보너스 선택 (2026-11-19, 지시서 1118_젬_필살기제거_보너스선택)
- 필살기 위력 일반 젬·특수 증가 옵션은 `retired`(풀에서 제외, 데이터는 남김). `RandomGem`·`CreateSpecial`이 제외.
- 일반 젬 '보너스 선택 젬'(유틸, 목록 맨 끝 번호 16, 능력치 `RunStat.BonusPicks` 맨 끝): 개당 `bonusPickPerGem`(1)회. `DayNightCycle.StartRun`에서 젬 적용 직후 `LevelUp.ShowBonus(N)`. 보너스 모드는 레벨 규칙 없이 무작위 칸만(consumableSlotRate·등장 조건 그대로), 제목 "보너스 선택 n/N", 레벨·경험치 변화 없음, 리롤 가능. 아이콘은 `normal_gem_13`(필살기 위력 젬이 쓰던 그림).
- 특수 증가 옵션 무기·총알 크기 15~25(유틸). 보조 쌍검(`Weapon.DualBlades` + `Bullet` LateUpdate)·메인 쌍검(`PlaceBlade`) 거리에 크기 배율을 곱해 손잡이가 캐릭터 밖에. 대검 기본 공격(내려찍기·올려치기 원 반지름과 범위 표시, 후리기는 커진 대검 판정 + 쥔 자리를 크기 비율만큼 바깥으로)도 크기 배율 적용(`WeaponSizeScale`). 메인 랜스: `UpdateHeldLance`가 매 프레임 크기를 덮어써 크기 변화가 반영되지 않던 것을 직접 곱하도록 수정, 쥔 자리·찌르기 거리도 크기만큼(`LanceHold`), 패링 방패는 크기 그대로. 헤비보우건 연사탄 궤적 그림·각성 칼날 중량탄 길이, 보조 대검 배치 거리(`Bullet.OrbitScaled`)도 크기 적용. 테스트 모드 '세부 능력치 더하기'의 [-]는 감소 항목(따로 곱)으로 들어감.
- 저장 버전 3: 필살기 위력 일반 젬 → 같은 개수 보너스 선택 젬(장착 칸 포함), 필살기 위력 특수 젬 → 증가만 무기·총알 크기(새 범위에서 다시 뽑음, 감소·잠금 유지). 카탈로그에 새 항목이 없으면 교체를 미룸. 새 항목은 `MonNyang/젬 UI 에셋 설정`이 기존 에셋에 추가(`EnsureBonusPick`).

## 밤 디펜스 기반 (2026-11-19, 지시서 1119_밤디펜스_기반)
- 흐름: 낮 `dayDuration`(100) → 귀환 준비 `returnPrepTime`(15, `DayNightCycle.Phase.ReturnPrep`) → 밤 `nightDuration`(80) → 아침 선택지. 4사이클 뒤 5일 차 낮 `lastDayTime`(60) → 보스. `GameManager.maxGameTime`은 시작 때 `RunConfig.totalTime`(780)으로 설정, 귀환 준비 동안 `gameTime` 정지(`DayNightCycle.TimerPaused`). 마지막 날 = `LastDay`(계산값 5).
- 귀환 준비: 낮 스폰 정지(`DaySpawnPaused`, 밤 동안에도 정지 → 아침 재개), 상점·캠프 처벌은 낮 조건이라 함께 멈춤. 화면 위 "몬스터가 몰려옵니다! 준비하세요!" + 경고음([MHS] Beep~), 기지(`MapCenter`)가 화면 밖이면 가장자리 표시, 기지 방향 60° 안으로 움직이면 이동속도 +50%(`NightDefense.ReturnSpeedScale`, Player 이동에 곱함). 포탑 연결용 이벤트 `DayNightCycle.ReturnPrepStarted / NightEnded`.
- 웨이브 경로: Main 씬 `Wave Path`(자식 웨이포인트 3개: 숲 (-41.4,-8.9) → 모서리 (0,-8.9) → 입구 문 (0,1.85)), 기즈모 표시, 길이 52.15칸(길 타일 시작 x -38 기준이면 약 48.8).
- 밤 웨이브(`NightDefense`): 밤 동안 경로 시작점에서 `nightSpawnInterval`(1.2/1.0/0.8/0.6)마다 그날 몬스터(`nightSpecies` 0~3). `Enemy.BeginNightWave`: 경로를 따라 이동(속도 = 길이 / `nightTravelTime` 25, 걷기 애니메이션 비례), 길 폭 안에서만 흩어짐(`nightLaneWidth`), 플레이어 접촉 피해는 그대로. 체력·공격 +`nightBuffRate`(20%, `DayNightCycle.MonsterBuff`도 이 값 x 아침 선택지 밤 강화 배율로 바뀜). 처치 시 코인 100% + 경험치 구슬 1개.
- 입구·기지: 경로 끝 도달 시 기지 체력 -1(`baseMaxHp` 100), 보상 없이 사라짐. 0이면 `gm.health = 0` → 플레이어 사망과 같은 실패 처리. 게이지는 화면 위 가운데(전체 타이머 아래).
- 아침: 길 위 밤 몬스터 즉시 소멸(보상·기지 피해 없음). 넉백: 밤 몬스터는 경로를 거슬러 `넉백 거리 x nightKnockbackMul`(0.5)만큼 뒤로, 시작점보다 뒤로는 안 감, 길 밖으로 안 나감.
- 중간보스: n일 차 낮 시작 + `miniBossDelayFirst`(50) / `miniBossDelay`(30) (`MiniBossConfig.stageTimes`는 더 이상 쓰지 않음).
- 테스트 모드 화면 왼쪽 밤 디펜스 상태 텍스트(`TestMode.DrawNightDebug`)는 2026-11-23 삭제(아침 효과·대장 일정 표시 포함). 테스트 모드 버튼 '밤 즉시 시작', '기지 체력 -10'.
- `colonyPenaltyRate`(0.3)는 변수만 (군락은 다음 작업).

## 메인무기 공격속도·쿨타임 적용 (2026-11-19, 지시서 1119_메인무기_능력치적용)
- 공통(`MainWeaponController`): 공격속도 배율 상한 `attackSpeedMax`(2.0), 공격 대기·충전 하한 `attackCooldownMin`(0.4), R 쿨타임 = 기본 x `Character.WeaponRate` x 공격 쿨감 x 무기 스킬 쿨감, 하한 `skillCooldownMin`(0.5). 값은 GameManager > mainWeapon 인스펙터.
- 공격속도(동작·간격 ÷ 배율): 쌍검 1·2단 동작과 단계 사이(`stepDelay`)·3단 회전 지속시간, 활 화살 간격(`baseCooldown`), 헤비보우건 경량탄 연사 간격·중량탄 발사 후 대기, 대검 들어 올리기·내려찍기·후리기, 랜스 찌르기 동작과 사이(`thrustInterval`)·각성 대검 단계 사이(`gsStepInterval`).
- 쿨타임 감소(대기·충전 x 배율): 쌍검 3단 후, 활 부채꼴 충전, 헤비보우건 탄 충전, 대검 원위치(`justRecoverTime`·`failRecoverTime`)와 찍힘이 끝난 뒤 다음 기본 공격까지(`baseCooldown`, 이전엔 쓰이지 않아 0초), 랜스 3연 후·각성 대검 3단 후, 5종 R.
- 고정(능력치 영향 없음): 쌍검 백대쉬 충전, 랜스 패링, 대검 저스트 구간·찍혀 있는 시간(`recoverTime`), 헤비보우건 탄 변경·은신, 랜스 R 낙하, 대검 R 차지 단계, 무적.
- 활: 레벨 성장(`LevelRateGrowth` = weapon.rate에서 캐릭터 배율·레벨업 쿨감을 뺀 비율)은 화살 간격에 곱함(보조무기 때처럼 레벨이 오르면 빨라짐, 5레벨 0.45 → 0.315초). 부채꼴 충전·대기는 레벨 성장 없이 캐릭터 배율·쿨감만. 보조무기 활은 `Weapon.rate`로 그대로.

## 메인무기 데이터 분리 (2026-11-19, 지시서 1119_메인무기_데이터분리)
- 메인무기 수치는 `Resources/MainWeapon/MainWeapon_0~4`(`MainWeaponData`)만 사용. 보조무기 레벨 표(damages·W_Speeds·W_Rates·counts)와 `Weapon`의 damage/speed/rate/count는 메인무기에서 읽지 않음. 처음 값은 `MonNyang/메인무기 데이터 만들기`(없는 에셋만 생성).
- 기본값 = 분리 직전 메인무기 전환 직후 실제 값(아이루, 카드·상점·젬 0). 5레벨까지의 성장과 그때의 공격속도 상한을 기본 시간에 녹이고 `attackSpeedGrowth`·`cooldownGrowth`는 1.0 (예: 쌍검 베기 0.18/2 = 0.09, 대검 다음 공격 대기 1.0x0.5 = 0.5, 활 화살 간격 0.45x0.7 = 0.315).
- 실행 시: 피해 = 데이터 기본 피해 x 캐릭터 공격력 x 레벨업 공격력 카드, 공격속도 = 성장 x 캐릭터 공격속도 x 스태미나 카드 x 능력치·버프(상한 2), 쿨타임 = 성장 x 캐릭터 쿨타임 배율 x 쿨감 카드 x 능력치·버프(하한 0.4), R = 데이터 쿨타임 x SkillCooldownFactor(하한 0.5). 캐릭터 배율은 한 번만.
- 이전과 달라진 체감: 쌍검·대검은 성장이 상한에 잘려 있어 상점 공격속도가 효과 없었는데, 이제 새 기본값 기준이라 빨라짐(+50% → 쌍검 베기 0.09→0.06, 대검 찍기 0.04→0.027).
- 능력치 카드(`Gear`)는 메인무기 `Weapon`을 건너뜀. 테스트 모드 '무기 레벨 +1'도 메인무기 제외.
- [버그 후보] 헤비보우건 탄 충전: 중량탄은 충전 시간 x 쿨타임 배율, 경량탄은 초당 충전량 ÷ 쿨타임 배율로 계산 방식이 다름. 분리 때 현재값(경량탄 초당 5/0.65 = 7.69) 그대로 옮김.

## 낮 몬스터 은신 보관 (2026-11-19, 지시서 1119_낮몬스터_은신보관)
- `DayMonsterStash`: 귀환 준비 시작(`BeginReturnPrep`, 끝없는 밤처럼 준비 없이 밤이 되면 `BeginNight`) 때 필드의 낮 일반 몬스터·중간보스가 `hideFxTime`(0.6)·`hideSinkDistance`(0.6)만큼 가라앉으며 투명해진 뒤 꺼짐. 다음 날 아침(`BeginMorning`)에 카메라 밖(낮 스포너와 같은 거리)에서 같은 상태로 다시 추격. 보스전 시작·새 판이면 비움.
- 일반 몬스터는 풀 오브젝트라(밤 웨이브가 다시 꺼내 씀) `Enemy.DayState` 기록(종류, 최대·현재 체력, 속도, 엘리트, 접촉 배율, 몸 색, 처벌 스택·배율·색·복귀 진행, 격려, 보상 없음)으로 저장 후 풀에서 새로 꺼내 `RestoreDayState`.
- 중간보스는 오브젝트를 그대로 꺼 두고(`MiniBoss.HideForNight/Reappear`) 숨는 동안 `MiniBoss.Alive`에서 빠짐 → 무리·처치 수 유지(이틀에 걸쳐 잡아도 보상 상자). 비드론은 다시 나올 때 `Group.waspChasing = true`(원 진형 해제, 개별 추적).
- 제외: 밤 웨이브, `Enemy.ColonyGuard`(군락 호위, 군락은 다음 작업).

## 포탑 시스템 (2026-11-19, 지시서 1119_포탑_시스템)
- 설정 `Resources/TurretConfig`(포탑 4종 사거리·배율·주기·크기 1.0/1.1/1.2/1.35, 발판 위치·종류·가격, buildStayTime 1.5, 감속·폭발·화상, 효과음). 그림은 `MonNyang/포탑 에셋 설정`(Turret_A~D를 칸마다 불투명 영역으로 자르고 받침이 같은 자리에 오도록 칸별 pivot, Point 필터).
- 발판 5곳(Ground/Map Object/Tilemap의 2x2 돌 타일): (1)(-11,-5.9) 석궁 50 / (2)(-3,-5.9) 끈적이 100 / (3)(-15,-11.9) 석궁 50 / (4)(-7,-11.9) 화룡포 300 / (5)(1,-11.9) 발리스타 200.
- `TurretSystem`: 귀환 준비 시작(`ReturnPrepStarted`) ~ 밤 끝(`NightEnded`) 돌 타일 + 가격 표시(부족하면 붉게), 발판 위 1.5초 서 있으면 코인 차감 후 건설(원형 게이지, 벗어나면 초기화, 크기 0→1 0.2초). 낮에는 2x2 칸을 바로 옆 풀 칸 타일로 채움(실행 중에만, 씬 원본은 그대로) + 포탑 천막 휴면. 보스전(`BossStarted`)에 포탑·표시 철거, 발판은 풀인 채로. 포탑은 물리 충돌 없음.
- `Turret`: 석궁·끈적이·화룡포는 사거리 안 `Enemy.PathProgress`가 가장 큰 밤 몬스터, 발리스타는 엘리트 우선 후 체력이 가장 높은 적. 회전 머리는 대상 방향, 왼쪽이면 세로 반전. 피해 = `turretBaseDamage`(25) x 캐릭터 공격력 x 배율(상점·젬·버프·아침 미적용).
- `TurretProjectile`: 화살(단일), 덩어리(포물선 → 튀김·반경 피해 → `SlowField` 3초, 감속 40% 중첩 없음, 경로 이동에만), 대형 화살(일직선 무한 관통), 불덩이(포물선 → 폭발 반경 2 x2.0 + 화상 초당 x0.3 3초(시간만 갱신) + 바닥 불꽃 3초).
- `Enemy.TakeTurretDamage`: 넉백·플레이어 기믹 없음, 생명의 일격 처치 수에 안 셈, 코인·경험치 드롭은 평소대로.
- [임시] 테스트 모드 '포탑 사거리 원 표시' 토글 - 확인 끝나면 삭제. 버튼 '포탑 전체 건설'(코인 없이), '포탑 초기화'.

## 아침 선택지 재구성 (2026-11-19, 지시서 1119_아침선택지_재구성)
- 18장(성장 6 / 도전 6 / 지옥 6), 수치는 `RunConfig.morningCards`(`MorningCardValues`). `MorningChoice.Rework`가 기존 목록을 고쳐 씀.
- 제거 13장(발톱 다듬기, 사뿐한 발걸음, 코끝 킁킁, 고양이 직감, 몰이사냥, 단식 수행, 필살기 봉인, 어둠 사냥, 토벌 의뢰, 꿈속 수련, 유리 발톱, 폭주, 끝없는 밤)은 `retired`로 풀에서만 제외(데이터 유지).
- 5일 차(마지막 날 `LastDay`) 아침 제외(`lastDayBan`): 기지 수리, 공병 고양이, 둥지 습격, 우두머리 현상금, 질주하는 밤, 피의 계약, 배수진, 무방비 캠프. 둥지 습격은 군락이 없어 조건이 항상 거짓.
- 새 능력치(`RunStat` 끝에 추가, 아침 선택지 출처): `TurretCost`(공병 고양이 x0.7, 가격 표시 포함), `NightTravelTime`(질주하는 밤 18/25), `MiniBossHealthToday`(우두머리 현상금, 그날 새로 나오는 중간보스만), `MiniBossBounty`(상자 코인 배율, 1보다 크면 무리 전멸 시 레벨업), `BaseMaxHp`(배수진 x0.5). 질주하는 밤 코인은 기존 `NightCoinDrop`을 밤 웨이브 코인에도 곱함.
- 적용 지점: `NightDefense.Repair/ClampBaseHp/BaseMaxHp`, 밤 웨이브 도착 시간·코인, `TurretSystem.Seal`(무방비 캠프 = 보스전 철거와 같은 처리)·`HasUnbuiltPad`·비용, `MiniBossDirector` 체력·상자·레벨업. 생포 의뢰 공격력 -25%는 RunStats 손해로 따로 곱(포탑 피해는 RunStats 미사용이라 영향 없음).
- 아이콘: 아침 선택지 UI는 카드별 아이콘이 없고 등급(성장·도전·지옥) 아이콘만 써서 임시 아이콘 사용 카드 없음.
- 테스트 모드: '아침 선택지 열기', '아침 카드 골라서 제시'(그 등급 자리에 강제 제시, `MorningChoice.ForcedId`). (화면 왼쪽 상태 텍스트는 2026-11-23 삭제)

## 맵·포탑·HUD·이펙트 통합 수정 (2026-11-20)
- 식생 랜덤 배치: `Camp Fence Wall`·`Wave Spawner/Wall`(Wall 태그 내부 충돌체)을 맵 가장자리로 잘못 잡아 배치 영역이 줄어든 문제. `MapDecorGenerator.FindArea`가 두께 3 초과 또는 길이가 맵의 절반 미만인 벽은 가장자리에서 빼고 배치 금지 영역(`interiorWalls`)으로 처리.
- 밤 시야 `nightVisionRadius` 0.22 → 0.286(+30%). 상인 별도 그림자 제거(그림 자체 그림자 사용). 상점 표지 104x40, '상점 열기 [F]' 240x56으로 축소. 아침/밤 배너 y 300 → 170.
- 기지 문 도착: `Enemy.FadeVanish`(gateFadeTime 0.45초 페이드) + 문 위치 별·먼지 효과(gateHitFxSize) + 문 오브젝트(`gateObjectNames`: Gate_Open_0, Gate_Close_4) 좌우 흔들림(gateShakeTime/Amount). 값은 RunConfig.
- 포탑 발판: `Build_turret` 그림(`TurretConfig.padSprite`, 에셋 설정 메뉴가 자름) + 가격, 게이지는 발판 모양으로 아래→위 채움(`gaugeColor` 민트). 건설하면 그 발판 타일은 풀로 바뀌고 다시 돌로 돌아오지 않음(테스트 '포탑 초기화' 시 복구).
- 포탑 그림: 머리 pivot을 무기 높이로 다시 잡아 받침과 붙게(A·C·D), 대기 꾸물거림 끔(`idleAnimate` false → 첫 장 고정). 크기 A 1.0 / B 0.95 / 발리스타 0.74 / 화룡포 0.86(그림 원본 크기 차이 보정, C·D는 조금만 크게), 투사체 0.85 / 0.75 / 1.8 / 1.3. 끈적이는 headOffset 0(발판 위 높이 맞춤), 대상 쪽으로 좌우 반전(`spriteFacesRight`).
- HUD: 기지 체력을 화면 오른쪽 위 양피지 판(천막 아이콘 + '기지 체력' + n / max 초록 바, `baseHpPanelOffset/Size/IconSize`, `baseHpBarSize`)으로 옮기고 능력치 판은 `statPanelOffset` (-10,-215)로 아래로. HR 표시는 그대로. 버튼 한 줄: 회피 (-15,25), E 필살기 (-39,25), R 무기 스킬 (-63,25)(Main.unity 직렬화 값).
- 헤비보우건 R: 사거리 `sniperRadius`는 2 그대로(커지지 않았음) → 스코프 렌즈 지름을 실제 피해 지름에 맞춤(`scopeFitRange`, `scopeRangeFit` 1). 끄면 예전 `scopeScreenFraction`. 소리: 발사 `[MHS]HeavyBow_R_Shoot`(sniperShotSound), 탄피·장전 `[MHS]HeavyBow_R_Reload`(shellDropSound, 발사 0.35초 뒤).
- 플레이 확인: 기지 체력 판·능력치 판·HR, 발판 그림·가격, 건설 후 발판 제거, 포탑 조립·크기, 상인·표지 확인. 문 효과·배너·밤 시야·스코프 크기·새 사운드·R 버튼 줄(무기 스킬 획득 후 표시)은 미확인.

## NPC·알림 UI·밤 몬스터 밀침·헤비보우건 R 수정 (2026-11-20)
- 상점 표지 발바닥 아이콘 24 → `shopSignPawSize` 18. '상점 열기 [F]' 판 배경 투명도 `shopPromptAlpha` 0.6. 'N일차 낮/밤' 문구는 HUD 캔버스 안 하위 캔버스(`bannerSortingOrder` 90)로 최상단 (HUD와 함께 숨겨지는 동작 유지).
- 밤 웨이브 몬스터는 경로로 강제 이동(MovePosition)이라 플레이어를 밀었음 → `BeginNightWave`에서 플레이어와 물리 충돌을 끄고(`Physics2D.IgnoreCollision`), `Enemy.NightContact`가 겹침(`Physics2D.Distance`)으로 `Player.ContactHit`(기존 접촉 피해 본문 그대로)를 호출. `Init`·`Vanish`에서 충돌 복구.
- 헤비보우건 R: 스코프 어둠 그림 바깥에 어둠 판 4장(`darkEdges`)을 붙여 조준점이 움직여도 화면 가장자리가 비지 않음. 발사음 파일 앞 약 0.51초 무음 → `AudioManager.PlayClipFrom`으로 `sniperShotSoundStart` 0.5초부터 재생(원본 파일은 그대로). 조준 시작음 = `[MHS]HeavyBow_R_Reload`(scopeInSound), 탄피·장전음(shellDropSound) 비움, 비어 있으면 대체 효과음도 내지 않음.
- 컴파일 오류 없음(Editor.log). Unity MCP 연결이 끊겨 플레이 확인은 못 함.

## 포탑 게이지·파츠, HUD 레이아웃, 헤비보우건 R 종료음 (2026-11-20)
- 헤비보우건 R 종료음 제거(`scopeOutSound` 비어 있으면 대체음도 없음). 플레이 중 Main 씬 메모리 값도 R 사운드(조준 시작 Reload, 탄피 없음)로 맞춤.
- 포탑 제작 게이지: 흰 사각 그림 + 노란색(`gaugeColor`), 하위 캔버스로 `gaugeSortingLayer` Weapon(-5)에 그려 캐릭터 위에서 아래→위로 보임(`gaugeInset` 0.82). 채움 방식은 그림이 없으면 전체가 칠해지므로 그림 필수.
- 포탑 머리: 받침 높이의 약 30% 위에 얹히도록 headOffset A 1.62 / 발리스타 1.96 / 화룡포 2.01(전에는 머리가 받침 바닥까지 내려와 묻혀 보였음). 석궁 대기 그림을 발사 그림(ta_head_2)으로 통일해 발사 시 발밑 풀 깜빡임 제거(`TurretAssetSetup`).
- 하단 버튼: 회피·필살기·무기 스킬 전체 배율 0.7(`buttonScale`, `skillButtonScale`)로 아이콘·글자 정렬 유지한 채 축소, 위치 (-13,19)/(-31,19)/(-49,19). 보관 슬롯은 기존 `AlignSlot`이 회피 버튼 크기에 맞춤.
- 기지 체력: `bar_green`은 게이지가 아니라 이름표 판 그림이라 깨져 보였음 → `baseHpFillSprite` Object_Panel_69(초록 바) / `baseHpBackSprite` Object_Panel_70, 채움은 RectMask2D로 잘라 표시. 판 투명도 `baseHpPanelAlpha` 0.45, 판 폭 300.
- 냥냥코인 표시를 킬 수와 같은 줄(`coinCounterOffset` 347,-85), HR(Level) x -20 → -38.4로 경험치 바 오른쪽 끝에 맞춤.
- 능력치 판: 줄마다 [아이콘 + 글자] 구조(`statIconSize` 24, `statIconGap`, `statRowGap`), 아이콘은 상점 능력치 아이콘(StatCatalog.Def(stat).icon), 폭 300. 프로젝트에 한글 폰트가 neodgm 하나뿐이라 폰트 교체는 못 함(폰트 파일 추가 필요).
- 일차 문구 하위 캔버스 정렬이 HUD 비활성 상태에서 풀려 -5로 남던 문제 → 문구 표시 중 다시 90으로 적용.
- Main.unity는 에디터에 저장 안 된 변경이 있어 다시 불러오지 않았고, 파일에 바꾼 값(HR 위치, 버튼 위치, R 사운드)을 열린 씬에도 같게 적용함. 씬 저장 필요.
- 플레이 확인: 코인·HR·버튼 줄·능력치 아이콘·기지 체력 바, 제작 게이지 절반 표시(캐릭터 위), 포탑 A·D 조립, 스코프 가장자리 어둠, 일차 문구 정렬 90, 밤 몬스터 충돌 무시 10/10 + 접촉 피해 유지. 소리는 확인 불가.

## HUD 레퍼런스 맞춤 · 게이지 층 · 헤비보우건 장전음 (2026-11-20)
- 포탑 제작 게이지 층: `gaugeSortingLayer` ForeGround / 6 (발판 위, 캐릭터 아래).
- 헤비보우건 R: 조준 시작음 없음, 한 발 쏜 뒤 `shellDropDelay` 0.6초에 장전음(`shellDropSound` = [MHS]HeavyBow_R_Reload), 마지막 탄 뒤에는 없음. Main 씬 저장함.
- 기지 체력 판(레퍼런스 배치): 위쪽 [천막 아이콘][제목 / 현재 체력(오른쪽, 외곽선 없음)], 아래쪽 판 너비 게이지. `baseHpPanelSize` 300x110, `baseHpPanelScale` 0.8, `baseHpIconSize` 52, `baseHpFontSize` 22.
- 능력치 판: 줄마다 [아이콘][이름 ……… 값(오른쪽 정렬)], 머리글 앞 발바닥, `statPanelScale` 0.8, `statPanelOffset` (-10,-208).
- 글꼴: `RunConfig.panelFont` = MHS FONT/NotoSansKR-VariableFont_wght, `panelFontStyle` Bold(`RunUI.ApplyPanelFont`). 노토 가변 글꼴은 기본 굵기가 100(Thin)이라 가늘게 보여, MHS FONT 후보 5종(나눔스퀘어라운드 B/EB, 나눔스퀘어 EB, 나눔고딕 EB, 나눔스퀘어네오 Eb)을 화면 비교해 나눔스퀘어라운드 EB(Normal)로 확정.
- 좌측 상단: 킬 수(Kill x 20 → 5, Main 씬 저장), 그 아래 냥냥코인(`coinCounterOffset` 118,-132), 시계 (70,-235)·일차 글자 (70,-315)로 아래로.

## 글꼴 정리 · 시계 글자 · 기지 피격 흔들림 · 케스킹 돌진 (2026-11-20)
- 시계 밑 'N일차 낮/밤' 글자도 `RunUI.ApplyPanelFont`(나눔스퀘어라운드 EB).
- 기지 체력이 깎일 때 `CameraShake.Play(baseHitShakePower 0.08, baseHitShakeTime 0.18)`.
- MHS FONT: 나눔스퀘어라운드 TTF(B/EB/L/R)만 남기고 나머지(노토, 나눔 계열 15폴더)는 휴지통으로 이동(314MB → 4MB). 참조되던 글꼴은 EB 하나뿐이었음.
- 케스킹 돌진: 공격 그림이 한 장(Kestodon_6)이라 미끄러지듯 보임 → 돌진 중 달리기 그림 4장을 `chargeRunAnimMul` 3.5배로 재생 + 발밑 먼지 더 자주(`chargeRunFrames`로 끄면 예전 방식). 화면 확인은 못 함(테스트 중 사망).
- 시계 바늘: 시침 = 하루(낮 100초 + 밤 80초) 한 바퀴(낮 0→200°, 밤 200→360°, 귀환 준비 중 정지), 분침 = 낮/밤 각각 한 바퀴. 계산은 맞지만 시계판이 나침반 모양이라 낮/밤 구간 표시가 없어 읽기 어려움 → 개선은 미정.
- 레벨업·아침 선택지에 나눔스퀘어라운드 EB를 실행 중에만 바꿔 비교(저장 안 함): 작은 설명 글자가 또렷해지고 한 줄에 더 들어감.

## 아침 선택지 꾸미기 · 선택지 글꼴 · 시계 글자 (2026-11-20)
- 아침 선택지 잘림: 가로 화면에서 캔버스(720x1280 기준, 가로세로 0.5 맞춤) 높이가 약 720인데 내용이 약 770 → 내용 묶음 `Content`에 `UiFitHeight`(새 스크립트, `morningContentHeight` 860 기준으로 축소).
- 아침 카드: 큰 아이콘을 카드 효과 아이콘으로(`MorningChoice.CardIcon`, 상점 능력치 아이콘·석궁 포탑·천막·보상 상자·폭탄, 없으면 등급 아이콘), 등급 이름판에 작은 등급 아이콘, 보상/위험 상자 위에 진한 색 머리표(아이콘 + 흰 글자). 이름·본문은 나눔스퀘어라운드 EB.
- 레벨업 카드: 설명(Desc)·단계(Step) 글자만 나눔스퀘어라운드 EB, 제목·이름표는 픽셀 글꼴 유지.
- 시계 밑 글자: `clockFontSize` 24 → 20, 외곽선 `clockLabelOutline` 1(검정 불투명)로 얇게 → 번져 보이던 현상 완화.

## 시계판 낮밤 · 코인 글자 · 버튼 글꼴 · 아침 카드 레벨업 그림 (2026-11-20)
- 시계판: 원형 부채꼴 두 장(`Night Zone` 남색 전체 + `Day Zone` 노랑, 12시부터 시계 방향으로 낮 비율 100/180). 시침이 노랑 구간이면 낮, 남색이면 밤. `clockZoneSize` 0.62, `clockDayZoneColor`, `clockNightZoneColor`.
- 냥냥코인 수: 반투명 외곽선 한 겹이라 흐려 보임 → 킬 수 글자처럼 불투명 검정 두 겹(`coinOutline` 1.5), 크기 `coinFontSize` 34.
- 회피·필살기·무기 스킬 버튼 글자(labelFont, skillButtonFont)를 나눔스퀘어라운드 EB로(Main 씬 저장). 픽셀 글꼴을 0.7배로 줄이며 흐려지던 현상 해소.
- 아침 카드: 레벨업 카드 그림으로 통일(lv_card 틀, lv_button_grey 이름표에 등급 색 + 카드 이름, lv_divider). 효과 아이콘, 가운데 보상(▲ 초록) / 구분선 / 위험(▼ 빨강) 각 구역 세로 가운데, 아래 단계판에 '성장 · 즉시 / 도전 · 아침까지 / 지옥 · 영구'. `morningCardSize` 280x470, `morningCardSpacing` 300. 예전 상자 방식(`CreateTextBox`)은 남아 있으나 사용 안 함.
- RunConfig `nightLaneWidth` 주석이 다른 줄 끝에 붙어 있던 것 정리.

## 시계 빛 고리 · 헤비보우건 탄창 게이지 위치 · 포탑 머리 깃발 (2026-11-20)
- 시계 낮·밤: 시계판 위 부채꼴 → 시계판 뒤 `Clock Zones`(지름 `clockZoneSize` 1.1)로 옮겨 시계판 테두리 바깥으로만 금색(낮)/파랑(밤) 고리가 보임.
- 헤비보우건 캐릭터 아래 탄창 게이지가 안 보이던 버그: `gaugeOffsetY` -1.4 고정 위치가 화면 비율 변경 뒤 체력바(화면 UI)와 겹쳐 가려짐 → `gaugeBelowHpBar`(기본 켬)로 매 프레임 체력바 아래 끝을 월드로 바꿔 그 아래(`gaugeHpGap` 0.05)에 둠.
- 왼쪽 헤비보우건 잔탄 숫자·랜스 스택 숫자를 나눔스퀘어라운드 EB + 검정 외곽선.
- 포탑 머리 깃발: 머리 그림의 깃발과 받침 깃발이 위아래로 두 개 보이던 문제 → `TurretAssetSetup.CutHead`로 A·C·D 머리 그림을 회전 중심 아래 72/75/78px까지만 자름(원본 그림 그대로, 자르는 범위만). 받침 깃발 하나만 보임. 발리스타·화룡포 headOffset 1.77 / 1.82 (조금 내림).

## 밤 웨이브 야간 강화 제거 · 대장 몬스터 (2026-11-20, 지시서 1120_밤웨이브_대장몬스터)
- 야간 강화 제거: `DayNightCycle.MonsterBuff`는 0 (붉은 색·접촉 x1.2 없음, `nightBuffRate`·`bloodNightBuffMul`은 값만 남김). 제거 전 측정한 밤 체력 6/60/96/120 = 새 표 `nightHp`와 같음 (이중 적용 없었음).
- 밤 일반 몬스터 최종 체력 = `nightHp[일차] x (1 + RunStat.NightHpAdd)` (`NightDefense.NightMonsterHealth`). NightHpAdd는 더하기 능력치 (피의 계약 0.3, 이후 군락 페널티도 여기에 더함). 밤에는 엘리트 없음.
- 대장 몬스터: `RunConfig.bossUnitPlans`(1~4밤 등장 구간, 구간 하나 = 한 마리) + 피의 계약 `NightBossExtra`마다 `bossUnitExtraWindow`(5~25초). 밤 시작부터 잰 시간(게임 정지 시 멈춤). 체력 x`bossUnitHpMul` 20, 이동 시간 x`bossUnitTravelMul` 2 (질주하는 밤 반영 → 36초), 크기 1.6, 보라 색·발밑 오라, 넉백·밀침 면역, 감속은 적용, 입구 도달 시 기지 -`bossUnitGateDamage` 10, 보상은 일반 밤 몬스터와 같음, 등장 시 상단 작은 알림 "대장 몬스터 출현" + `bossUnitSound`(비어 있으면 소리 없음).
- 대장은 `Enemy.BossUnit`. `IsElite`가 아님 → 엘리트 코인(엘리트 토벌)·엘리트 체력·공격·크기·등장 배율 전부 적용 안 됨. 사냥 본능 피해 계산만 `Enemy.HuntKind`(엘리트 또는 대장 → Elite)를 씀 (Enemy·CombatUtil·MainWeaponController·GimmickCards·SynergyAwakening).
- 감속: 냉기 발자국과 끈적이 장판은 곱하지 않고 가장 강한 하나 (`Enemy.NightMove`의 Mathf.Min). 발리스타 표적: 대장 우선, 그다음 체력.
- 아침 카드: 피의 계약 = 레벨업 4·코인 200 / 밤 체력 +30%, 밤마다 대장 +1 (2~3일 차 아침에만). 엘리트의 행진은 낮 엘리트 크기 x`elitesMarchScale` 1.5 추가 (`RunStat.EliteScale`, 새로 나오는 엘리트부터). 엘리트 토벌·질주하는 밤·우두머리 현상금은 문구만 변경.
- 밤 나뭇잎: 밤에는 화면 밖으로 나간 잎이 다시 들어오지 않고 쉼, 아침에 `mapLeafResumeSpread`(4초)에 걸쳐 다시 들어옴.
- 능력치 패널 '밤: 몬스터 강화 +0%' 줄 → 밤에 체력 증가가 있을 때만 '밤: 몬스터 체력 +30%'.
- 테스트 모드 '대장 몬스터 즉시 소환 (밤)' 버튼, [임시] 대장 예정 시점 텍스트 (맨 위 임시 항목 참고).

## 대장 알림·귀환 아이콘·밤 웨이브 움직임·체력바 떨림 (2026-11-20)
- 대장 몬스터: 크기 `bossUnitScale` 2.08 (1.6 x 1.3), 등장음 [MHS] King_wave Sound. 알림 '대장 몬스터 출현!!' + 아이콘(special_icon_3)을 'N일차 밤이 찾아왔다냥!' 알림 자리(화면 가운데 기준 y `bossUnitTextY` 170)에, 글자 `bossUnitFontSize` 40, 문구만 깜빡임(`bossUnitBlinkSpeed`), 아이콘은 그대로.
- 귀환 준비 효과음 → [MHS] Ready for night.
- 귀환 버프 아이콘: 귀환 준비 중 기지 쪽으로 달려 이동속도 버프가 붙는 동안 체력바 아래(헤비보우건이면 탄창 게이지 아래)에 달리기 아이콘(special_icon_7). `NightDefense.UpdateReturnBuffIcon`, 크기·간격은 1080p 화면 픽셀 기준(`returnBuffIconSize` 34, `returnBuffIconGap` 4). 체력바 부모 Health는 픽셀아트 캔버스라 한 칸이 화면 약 7.5px.
- 시계 낮·밤 고리 색: 낮 진한 노랑(알파 0.82), 밤 보라(알파 0.68).
- 체력바 떨림 원인: 메인 카메라 PixelPerfectCamera가 그릴 때 정사영 크기를 바꿈(10 → 9.64) + Follow가 FixedUpdate에서 일반 카메라 값으로 위치 계산 → 플레이어가 화면 가운데에서 벗어나 움직이면 어긋나 떨림. 화면 좌표로 따라가는 방식은 순서를 바꿔도 떨림이 남아서, `Follow`가 게임 시작 때 체력바(Health)를 플레이어 자식 월드 캔버스 `Health World Canvas`(플레이어 그림 층, 순서 +`sortingOffset` 12)로 옮김. 크기는 원래 화면 캔버스 크기를 카메라 확대에 맞춰 유지, 원래 부모 HUD가 꺼지면 함께 숨김. 탄창 게이지 높이(`GaugeLocalY`)와 귀환 아이콘은 월드 캔버스 기준으로 계산.
- 대장 그리기 순서 `bossUnitSortingOrder` 3 (일반 몬스터 2와 같으면 겹칠 때 번갈아 그려져 깜빡임, `Init`에서 2로 되돌림).
- 밤 웨이브: 걷기 애니 x`nightWalkAnimMul` 0.7 (이동 속도 그대로). 밤 동안 Rigidbody2D를 Kinematic + useFullKinematicContacts + Interpolate로 바꿔 서로 밀지 않음(`Init`에서 Dynamic으로 되돌림). 꺾이는 곳에서 옆줄 위치가 1칸 넘게 튀던 것 → `WavePath.LanePositionAt`(꺾이는 곳 앞뒤 `cornerBlend` 1.2 동안 방향을 섞음). 밤 넉백 최소 시간 `nightKnockbackMinTime` 0.3초.

## 군락 시스템 (2026-11-20, 지시서 1120_군락_시스템)
- `ColonySystem`(DayNightCycle에 붙음): 2~4일 차 낮(아침 선택 닫힌 뒤) `colonyCount`곳, 둥지 습격(`RunStat.ColonyRaid`, 아침 해제)이면 `nestCount`곳·보상 x`nestRewardMul`. 위치는 맵 벽 범위(`OffscreenSpawn.TryGetMapRect`) - `colonyEdgeMargin`, 기지·둥지끼리 `colonyMinDist`, 캠프(루핑 경고 영역)+6, 밤 길 4, 포탑 발판 4, 플레이어 10 밖.
- `ColonyNest`: Mob_colony 시트(4종 x 6프레임)를 실행 중 잘라 씀(원본·메타 수정 없음). Enemy 층/레이어, 트리거 충돌체(접촉 피해 없음), 발사체·범위 공격(CombatUtil)·연쇄 번개·시너지 발사체로 피해, 사냥 본능·처치 폭발·생명의 일격 처치 수 없음. 파괴 시 경험치 구슬 `colonyExpMul`개 + 코인 흩뿌림(Anj_Rock).
- 호위: `Spawner.SpawnDayMonsterAt`(엘리트 규칙 그대로), `Enemy.MakeColonyGuard` 머리 위 붉은 stat_icon_1. 둥지 `colonyAggroRange` 안 진입·무리 중 하나 피격·둥지 피격 시 무리 전체 추적. 대기 중에는 `RePostion`(플레이어 쪽 재배치) 제외.
- 귀환 준비 시작: 못 깬 둥지 붉은 기운 후 사라짐 + `NightHpAdd`에 0.3 x 미처치 수(UntilMorning) + 경고 문구([MHS] Warning), 호위 전부 보상 없이 소멸(은신 보관 제외).
- 기지 체력 판 피격: 붉게 번쩍 + 좌우 흔들림(`baseHitPanel*`). 체력바 월드 캔버스는 가장 위 정렬 층(JOY STICK, 순서 100)이라 나무에 가려지지 않음. 탄창 게이지는 아직 플레이어 층.

## 군락 에셋·표시 개선, 스폰·포탑 버그 (2026-11-21)
- 둥지 그림 줄 = 그날 몬스터 종류(`colonyRowBySpriteType`: Hercudrome→1번째 나무, Kestodon→2번째 돌, Mosswine→3번째 흙, Vespoid→4번째 벌집). 크기 `colonyNestWidth` 5.6. 시트 칸 위치가 프레임마다 달라 꿈틀거려 `colonyNestFps` 0(첫 프레임 고정).
- 둥지 방향 표시: 연두 고리·화살표(`colonyIndicatorFrameColor`/`ArrowColor`), 등장 직후 `colonyAppearPulseTime` 동안 커졌다 작아짐. 등장 시 연두 문구 '군락이 나타났다냥! 둥지를 부수면 보상!' + [MHS] Beep~.
- 둥지 습격 카드 문구: 보상 '오늘 군락 파괴 보상 2배', 위험 '오늘 군락 2곳 생성, 못 깬 곳마다 밤 체력 +30%' (3줄이 넘쳐 깨져 보이던 것).
- 낮 스폰 표 단계 = 일차 - 1 (`Spawner.CurrentLevel`). 예전에는 경과 시간 ÷ (전체 시간 / 5)라 2일 차 낮 중간에 3일 차 Vespoid가 나옴. 중간보스 능력치 기준도 같은 단계.
- 중간보스 등장 깜빡임: Rigidbody2D 보간이 원점에서 시작해 첫 프레임에 원점과 등장 위치 사이(화면 안)에 보였음 → 보간을 첫 FixedUpdate 뒤에 켬.
- 포탑: 표적에서 꺼진 풀 오브젝트·사라지는 중·맵 벽 밖 제외. 석궁 화살은 대상이 죽거나 다시 소환되면(`Enemy.SpawnSerial`) 추적을 멈춤.
- 포탑이 빈 곳·맵 밖을 조준·발사하던 원인: `RePostion`(플레이어 주변 Area를 벗어난 적을 플레이어 반대편으로 옮김)이 밤 웨이브 몬스터에도 작동 → 한 물리 프레임 동안 길에서 60~80칸 떨어진 곳(맵 밖 포함)에 있다가 길로 돌아옴. 플레이어가 움직일 때만 생김. 밤 웨이브 몬스터는 재배치 제외.

## 군락 알림 판·둥지 배치 (2026-11-23)
- 등장 문구 '어디선가 군락이 등장했다냥!'(노랑), 실패 문구 `colonyFailText` '군락지 파괴 실패 : 이번 밤 몬스터 체력 +N%'(빨강). 판 없이 글자만(화면을 덜 가림), 외곽선 불투명 검정 두 겹. 등장 효과음은 비움(`colonyAppearSound`). `colonyPanel*` 값은 쓰지 않음.
- 둥지 방향 표시: 금색 고리 그림 + 둥지 아이콘 + 노랑 화살표, 반투명 없음, 중간보스 표시의 `colonyIndicatorScale` 1.35배. 고리 그림 안의 둥근 바탕은 제거(고리 그림이 없을 때만 `colonyIndicatorBackColor` 원).
- 둥지 크기 `colonyNestWidth` 6.6, 그림자 끔(`colonyShadow`). 호위는 둥지에서 `colonyGuardMinRadius` 2.2~`colonyGuardMaxRadius` 4.5 사이 무작위 자리(방향·거리 모두 무작위)에 서고, 대기 중에는 자기 자리 반경 `colonyGuardWanderRadius` 1.2 안에서 서성임.
- 큰 글씨 외곽선 검수: `RunUI.Label`은 외곽선을 불투명 검정으로, 글자 30 이상이면 `SoftOutline`(8방향 복사, 두께 최대 2.5). 기본 Outline은 대각선 4방향만이라 두껍게 하면 모서리가 뾰족뾰족했음. 반투명 한 겹을 두껍게 주면 부드러운 글꼴에서 번져 보였음. ConfirmPopup·GameResultView 버튼 글자도 SoftOutline, LevelUpView 외곽선 불투명. '[ 우두머리 등장!! ]' → '우두머리 등장!!'.

## 헤비보우건 정밀 저격 어두움 (2026-11-23)
- R 직후 바로 쏘면 화면 바깥이 덜 어두워지던 버그: 어두움(`HeavyScopeUI.fade`)이 열리는 중(Opening)에만 올라가는데 발사로 Firing이 되면서 멈춤 → Firing·Idle에서도 0.25초에 걸쳐 끝까지 어두워짐.

## 밤 웨이브 변주·대장 스폰 (2026-11-23, 지시서 1123_밤웨이브_변주_대장스폰)
- 대장이 새벽까지 못 오던 원인: 밤 시작 판정에 '게임 진행 중'이 섞여 레벨업 등 일시정지가 끝날 때마다 대장 일정이 다시 잡힘(추가 스폰·늦은 스폰) + 대장도 피격 애니메이션 중 멈춤. → 밤 시작은 낮/밤 전환만 봄(`BeginNightWaves`), 대장은 `bossIgnoreHitStall`로 맞아도 이동(감속은 적용).
- 대장 스폰 마감 `BossDeadline` = 밤 길이 - 대장 이동 시간 x `bossArrivalMargin` 1.3 (기본 15초, 질주하는 밤이면 33초). 구간 끝은 마감까지, 마감이 지나면 남은 대장 즉시. 구간 1~2일 차 5~15초, 3~4일 차 5~10/12~15초, 피의 계약 추가분도 마감까지.
- 밤 일반 몬스터 속도 3종(`speedTypeRate` 65/20/15%): 일반 x(1±0.1), 돌격 x0.8 → 경로 30~70% 지점에서 1.5초 x2.5 돌진, 굼벵이 x0.6. 기본 속도는 가중 평균 이동 시간이 nightTravelTime(질주 18)이 되게 `BaseNightSpeed`로 계산 (측정: 일반 22.3 / 돌격 24.6 / 굼벵이 37.1초, 가중 평균 25.0초).
- 스폰 단계: 잔잔 x1.6 4~6초 / 보통 x1.0 6~10초 / 러시 x0.4 3~5초, 첫 단계 보통, 같은 확률, 러시 연속 금지, 대장 직후 3초 러시 금지(러시 중이면 보통). 총 수 = floor(밤 길이 / 기존 간격) = 66/80/100/133으로 예전과 같고, 다 내면 종료. 남은 수를 러시로만 낼 수 있으면 러시 고정, 예정보다 `phasePaceSlack` 4마리 넘게 앞서면 잔잔(스폰이 밤 끝까지 이어지게).

## 레벨업 짝 무기 표시·스킬 모드 표시 (2026-11-23)
- 새 레벨업 화면(LevelUpView) 카드에 짝(시너지) 무기 표시가 빠져 있었음 → 무기 그림 오른쪽 위 모서리에 작은 메인 무기 아이콘(`CardView.synergy`) + 카드 금빛 반짝임(`CardSparkles`, 예전 카드와 같은 효과). 예전 Item 카드의 표시는 화면에 안 보이는 카드라 보이지 않았음.
- 능력치 판 '무기 스킬 모드 중': 조건이 `DamageTakenScale`(능력치 「받는 피해 감소」 포함)이라 받는 피해 능력치만 있어도 항상 표시 → `MainWeaponController.SkillModeActive`(이동 배율·스킬 받는 피해·철벽 모으기만).

## 알려진 문제
- `Wood_Fence.png.meta`가 git에 올라가 있지 않다. 스프라이트를 다시 자르면 ID가 바뀌어 참조가 끊길 수 있다.

