using System;
using System.Collections.Generic;
using UnityEngine;

// 타이틀 메뉴(메인 메뉴 · 캐릭터 선택 · 스테이지 선택 · 설정)의 데이터와 수치
// Assets/Resources/MenuCatalog 에셋을 실행 중에 불러와 사용한다. (메뉴 MonNyang > 메뉴 UI 에셋 설정 으로 생성·스프라이트 연결)
// 캐릭터·스테이지·설정 탭은 목록에 항목을 추가하면 화면에 자동으로 늘어난다.
[CreateAssetMenu(fileName = "MenuCatalog", menuName = "Scriptble Object/MenuCatalog")]
public class MenuCatalog : ScriptableObject
{
    private static MenuCatalog instance;

    public static MenuCatalog Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<MenuCatalog>("MenuCatalog");
                if (instance == null)
                {
                    Debug.LogWarning("Resources/MenuCatalog 에셋이 없어 기본값으로 실행합니다. (스프라이트 없음, 메뉴 MonNyang > 메뉴 UI 에셋 설정)");
                    instance = CreateInstance<MenuCatalog>();
                }
            }
            return instance;
        }
    }

    // ---------------- 저장 값 (기존 AudioSettings와 같은 키) ----------------
    public const string BgmKey = "BgmVolume";
    public const string SfxKey = "SfxVolume";

    public static float SavedBgmVolume => PlayerPrefs.GetFloat(BgmKey, Instance.defaultBgmVolume);
    public static float SavedSfxVolume => PlayerPrefs.GetFloat(SfxKey, Instance.defaultSfxVolume);

    // ---------------- 데이터 ----------------
    [Serializable]
    public class CharacterEntry
    {
        public string key = "Player_Palico";                     // 구분용 이름 (프리팹·스프라이트 이름)
        public int playerId;                                     // GameManager.GameStart(id)에 넘기는 번호
        public string displayName;
        public string quote;                                     // 한마디
        public bool quoteSmallItalic;                            // 한마디를 조금 작은 기울임 글씨로
        [TextArea(2, 4)] public string description;
        [Range(0, 5)] public int attack;                         // 공격력 (점수)
        [Range(0, 5)] public int cooldown;                       // 쿨타임 감소 (점수)
        [Range(0, 5)] public int stamina;                        // 스태미나 (점수)
        [Range(0, 5)] public int speed;                          // 이동속도 (점수)
        public string ultimateName;
        [TextArea(2, 4)] public string ultimateDescription;
        public Sprite ultimateIcon;                              // 인게임 필살기 버튼 아이콘과 같은 그림
        public Sprite[] idleFrames;                              // 서 있는 애니메이션 프레임 (첫 장은 초상화)
        public string unlockKey;                                 // AchiveManager가 쓰는 해금 저장 키. 비우면 처음부터 해금
        [TextArea(1, 3)] public string unlockCondition;          // 해금 조건 문구. 비우면 '미설정'

        public bool IsUnlocked => string.IsNullOrEmpty(unlockKey) || PlayerPrefs.GetInt(unlockKey, 0) == 1;
        public Sprite Portrait => idleFrames != null && idleFrames.Length > 0 ? idleFrames[0] : null;
    }

    [Serializable]
    public class StageEntry
    {
        public string displayName;
        public bool playable;                                    // 끄면 잠긴 스테이지 (검게, 이름·정보 숨김, 선택 불가)
        public string sceneName = "Main";                        // 출발하면 불러올 씬
        // 아래 항목은 데이터가 생기면 채운다. 비어 있으면 화면에도 비워 둔다.
        public Sprite image;                                     // 대표 이미지
        [TextArea(2, 4)] public string description;
        public string recommendedLevel;
        public Sprite[] monsterIcons;                            // 주요 등장 몬스터
        public Sprite[] rewardIcons;                             // 최초 클리어 보상
        [TextArea(1, 3)] public string rewardText;
    }

    [Header("# 캐릭터 (목록 순서대로 좌우로 넘김)")]
    public List<CharacterEntry> characters = new List<CharacterEntry>
    {
        new CharacterEntry
        {
            key = "Player_Palico", playerId = 0, displayName = "미케루", quote = "\"주인님은 바쁘다냥\"",
            description = "모자람 없는 든든한 파트너입니다.",
            attack = 3, cooldown = 3, stamina = 3, speed = 3,
            ultimateName = "치유 꿀벌 소환", ultimateDescription = "체력을 소폭 회복시켜 주는 치유 꿀벌을 주위에 소환합니다.",
        },
        new CharacterEntry
        {
            key = "Player_Gazabu", playerId = 1, displayName = "치카부", quote = "\"▣△◎○?!\"",
            description = "누구보다 날렵한 종족으로, 쉴 새 없이 적을 몰아붙입니다.",
            attack = 1, cooldown = 4, stamina = 4, speed = 4,
            ultimateName = "부족의 광란", ultimateDescription = "일정 시간 동안 쿨타임 감소, 공격 속도, 이동속도가 대폭 상승합니다.",
            unlockKey = "UnlockGaza", unlockCondition = "한 번의 수렵 중에 몬스터 800마리 처치",
        },
        new CharacterEntry
        {
            key = "Player_BowaBowa", playerId = 2, displayName = "도스도스", quote = "\"......\"",
            description = "그들의 힘은 타의 추종을 불허합니다.",
            attack = 5, cooldown = 3, stamina = 3, speed = 2,
            ultimateName = "특제 고기 미끼", ultimateDescription = "일정 시간 동안 주변 적들의 시선을 끌어모으는 미끼를 설치합니다.",
            unlockKey = "UnlockBowa", unlockCondition = "수렵 1회 클리어",
        },
        new CharacterEntry
        {
            key = "Player_Wiggler", playerId = 3, displayName = "휘청휘청", quote = "인형탈인 건.. 아니겠죠..?", quoteSmallItalic = true,
            description = "모든 능력이 뒤처지지만, 도망치는 속도만큼은 누구보다 빠릅니다.",
            attack = 1, cooldown = 1, stamina = 1, speed = 5,
            ultimateName = "땅속 파고들기", ultimateDescription = "제자리에 몸을 고정해 이동할 수 없는 대신, 전투 능력이 크게 증폭됩니다.",
            unlockKey = "UnlockWigller", unlockCondition = "헌터랭크 29 달성",
        },
    };
    public string[] statLabels = { "공격력", "쿨타임 감소", "스태미나", "이동속도" };
    [Range(1, 10)] public int statMax = 5;                       // 능력치 칸 수
    public string unlockConditionFallback = "미설정";             // 해금 조건 데이터가 없을 때

    [Header("# 스테이지 (목록 순서대로 위에서 아래로)")]
    public List<StageEntry> stages = new List<StageEntry>
    {
        new StageEntry { displayName = "깊은 숲 캠프거점", playable = true, sceneName = "Main" },
        new StageEntry { playable = false },
        new StageEntry { playable = false },
        new StageEntry { playable = false },
    };

    [Header("# 설정 탭 (실제 옵션이 있는 탭만 내용이 생김)")]
    public string[] settingTabs = { "게임", "그래픽", "사운드", "조작", "기타" };
    [Min(0)] public int soundTabIndex = 2;                       // 배경음·효과음 볼륨이 들어가는 탭
    [Min(0)] public int startTabIndex = 2;                       // 설정을 열었을 때 먼저 보이는 탭

    [Header("# 사운드 (기본값 버튼이 되돌리는 값, 처음 실행할 때의 값)")]
    [Range(0f, 1f)] public float defaultBgmVolume = 0.5f;
    [Min(0f)] public float bgmOutputScale = 1.4f;               // 실제 인게임 배경음 = 설정값 x 이 값 (기본 50%일 때 0.7, 100%일 때 1.4 → 최대 1)
    [Min(0f)] public float titleBgmOutputScale = 1.26f;         // 실제 타이틀 배경음 = 설정값 x 이 값 (기본 50%일 때 0.63 = 예전 0.7의 90%)
    [Range(0f, 1f)] public float defaultSfxVolume = 0.5f;
    public AudioClip clickSound;                                 // 메뉴 버튼 소리. 비우면 타이틀의 기존 시작 버튼 소리
    [Min(0f)] public float clickVolumeScale = 0.4f;             // 버튼 소리 = 효과음 볼륨 x 이 값

    [Header("# 타이틀 씬 (새 메뉴가 대신하는 기존 오브젝트 이름, 실행할 때 숨김)")]
    public string[] hiddenTitleObjects = { };

    [Header("# 타이틀 배경 움직임 (마우스를 따라 배경이 살짝 좌우로)")]
    public string titleBackgroundName = "Title BG";
    [Min(0f)] public float parallaxAmount = 0.12f;              // 마우스가 화면 끝에 있을 때 배경이 움직이는 거리 (월드, 반대 방향)
    [Min(0f)] public float parallaxVertical = 0.08f;            // 위아래 움직임 (0이면 좌우만)
    [Min(1f)] public float parallaxZoom = 1.03f;                // 위아래로 움직여도 화면 위·아래 끝이 비지 않도록 배경을 살짝 키움
    [Min(0.1f)] public float parallaxSmooth = 4f;               // 따라가는 속도

    [Header("# Main 씬 연결 (기존 캐릭터 버튼의 클릭을 그대로 실행)")]
    public string startButtonPath = "Canvas/GameStart/Character Group/Character {0}";
    public string oldSelectPanelPath = "Canvas/GameStart";      // 기존 캐릭터 선택 화면 (메뉴로 들어오면 바로 숨김)
    public string titleSceneName = "Title";

    [Header("# 화면 기준")]
    public Vector2 referenceResolution = new Vector2(1920f, 1080f);
    [Range(0f, 1f)] public float matchWidthOrHeight = 0.5f;
    public int sortingOrder = 10;                                // 타이틀 연출(빛·나뭇잎) 위에 그림
    [Min(0.05f)] public float pixelScale = 1f;                   // 9-slice 테두리 굵기 배율 (작을수록 두껍게)
    [Min(0.05f)] public float frameScale = 1f;                 // 화면 테두리(Panel_2 나무 액자) 굵기 배율 (작을수록 두껍게)

    [Header("# 전환 (초, 실제 시간)")]
    [Min(0.01f)] public float panelFadeTime = 0.22f;
    public float panelSlide = 24f;                               // 새 화면이 아래에서 올라오는 거리
    [Min(0.01f)] public float launchFadeTime = 0.9f;             // 출발할 때 어두워지는 시간
    [Min(0.01f)] public float mainFadeInTime = 0.7f;             // Main 씬에서 밝아지는 시간
    [Min(0.01f)] public float cardPunchTime = 0.18f;
    [Min(1f)] public float cardPunchScale = 1.06f;
    [Min(0.1f)] public float idleFps = 6f;                       // 가운데 캐릭터 서 있는 애니메이션 속도
    [Min(0.1f)] public float toastTime = 1.4f;                   // '적용되었습니다' 표시 시간

    [Header("# 버튼 반응")]
    [Min(0.5f)] public float hoverScale = 1.04f;
    [Min(0.5f)] public float pressScale = 0.95f;
    [Min(1f)] public float scaleSpeed = 18f;
    public Color buttonNormalTint = new Color(0.9f, 0.9f, 0.9f, 1f);
    public Color buttonHoverTint = Color.white;
    public Color buttonPressedTint = new Color(0.68f, 0.64f, 0.58f, 1f);
    public Color buttonDisabledTint = new Color(0.38f, 0.36f, 0.34f, 1f);

    [Header("# 색")]
    public Color backdropColor = new Color(0.07f, 0.045f, 0.03f, 0.97f);   // 하위 화면 바탕 (어두운 목재)
    public Color screenFrameTint = Color.white;       // 화면 테두리 판
    public Color goldColor = new Color(0.98f, 0.8f, 0.36f, 1f);
    public Color goldFrameColor = new Color(0.78f, 0.58f, 0.2f, 1f);         // 버튼·판넬 금색 테두리
    public Color goldFrameHoverColor = new Color(1f, 0.86f, 0.42f, 1f);
    [Min(0f)] public float goldFrameWidth = 2f;
    public Color lightTextColor = new Color(0.97f, 0.91f, 0.79f, 1f);
    public Color darkTextColor = new Color(0.27f, 0.15f, 0.07f, 1f);        // 양피지 위 글자
    public Color headerTextColor = new Color(0.55f, 0.24f, 0.11f, 1f);      // 양피지 위 소제목
    public Color mutedTextColor = new Color(0.55f, 0.48f, 0.42f, 1f);
    public Color sideCardTint = new Color(0.42f, 0.38f, 0.36f, 1f);         // 좌우 미리보기 (어둡게)
    public Color lockedTint = new Color(0.3f, 0.3f, 0.32f, 1f);             // 잠긴 카드·줄
    public Color silhouetteColor = Color.black;                              // 잠긴 캐릭터 그림
    public Color emptyAreaColor = new Color(0.16f, 0.11f, 0.08f, 1f);       // 데이터가 없는 이미지 칸
    public Color sliderFillColor = new Color(0.55f, 0.82f, 0.38f, 1f);

    [Header("# 스프라이트 (메뉴 MonNyang > 메뉴 UI 에셋 설정 으로 채움)")]
    public Sprite logo;                                          // Logo_Title 'Title Logo'
    public Sprite screenFrame;                                   // 메인 배경 패널 (Panel_1 어두운 판)
    public Sprite parchment;                                     // 정보 패널 (Panel_1 양피지)
    public Sprite ornatePanel;                                   // 강조 패널 (Panel_1 장식 어두운 판: 스테이지 목록, 설정 옵션)
    public Sprite headerPlate;                                   // 타이틀 배너 (발바닥 그림 포함)
    public Sprite noticeBanner;                                  // 고지 배너 (어두운 띠, 발바닥 그림 포함)
    public Sprite paw;                                           // 발바닥
    public Sprite buttonGreen;                                   // 강조 버튼 (플레이·다음·출발·적용·선택한 탭)
    public Sprite buttonBrown;                                   // 보통 버튼
    public Sprite cardSelected;                                  // 가운데 캐릭터 (초록 빛 테두리)
    public Sprite cardNormal;                                    // 좌우 미리보기
    public Sprite cardLocked;                                    // 잠긴 캐릭터
    public Sprite arrowLeft;
    public Sprite arrowRight;
    public Sprite statFilled;
    public Sprite statEmpty;
    public Sprite slot;                                          // 초상화·아이콘 칸
    public Sprite divider;
    public Sprite rowNormal;                                     // 스테이지 줄
    public Sprite rowSelected;
    public Sprite rowLocked;
    public Sprite sliderBar;                                     // 슬라이더 홈
    public Sprite sliderFill;                                    // 슬라이더 채움 (초록)
    public Sprite sliderHandle;
    public Sprite bgmIcon;
    public Sprite sfxIcon;
    public Sprite lockIcon;                                      // 비우면 코드로 그린 픽셀 자물쇠
    public Sprite iconBack;                                      // 돌아가기
    public Sprite iconConfirm;                                   // 다음·적용
    public Sprite iconPlay;                                      // 출발
    public Sprite iconReset;                                     // 기본값
    public Sprite[] settingTabIcons;                             // 설정 탭 순서대로 (게임·그래픽·사운드·조작·기타)
    public Vector4 objectPanelBorder = new Vector4(12f, 12f, 12f, 12f);   // 테두리 정보가 없는 Object_Panel 그림에 쓰는 9-slice 테두리

    [Header("# 공통 배치")]
    public float headerTop = 82f;                                // 화면 위에서 제목 띠 중심까지
    public Vector2 headerSize = new Vector2(560f, 92f);
    [Min(1)] public int headerFontSize = 44;
    public Vector2 headerTextOffset = new Vector2(0f, -9f);     // 제목 글자 위치 보정 (제목판 몸통 가운데에 오도록)
    public Vector2 bottomButtonSize = new Vector2(300f, 84f);
    [Range(0f, 1f)] public float buttonIconSize = 0.5f;          // 버튼 아이콘 크기 (버튼 높이 대비)
    public Vector2 bottomButtonMargin = new Vector2(240f, 82f);   // 화면 왼쪽·오른쪽 아래에서 버튼 중심까지
    [Min(1)] public int bottomButtonFontSize = 36;
    public float screenFrameInset = 14f;

    [Header("# 메인 메뉴 (로고 가운데 위, 버튼 오른쪽, 고지 문구 아래 가운데)")]
    public Vector2 logoPosition = new Vector2(0f, 268f);
    public Vector2 logoSize = new Vector2(660f, 234f);
    public Vector2 menuButtonSize = new Vector2(440f, 92f);
    public Vector2 menuFirstButtonPosition = new Vector2(600f, 0f);
    public float menuButtonSpacing = 106f;
    [Min(1)] public int menuFontSize = 44;
    public string noticeText = "몬스터냥터는 비공식 팬 게임으로, CAPCOM과 무관합니다.";
    public Vector2 noticePosition = new Vector2(0f, 74f);         // 화면 아래 가운데 기준
    public Vector2 noticeSize = new Vector2(1140f, 76f);
    [Min(1)] public int noticeFontSize = 28;

    [Header("# 캐릭터 선택")]
    public Vector2 carouselCenter = new Vector2(-300f, 20f);
    public Vector2 centerCardSize = new Vector2(400f, 540f);
    public Vector2 sideCardSize = new Vector2(250f, 350f);
    public float sideCardOffset = 360f;
    public float arrowOffset = 545f;
    public Vector2 arrowSize = new Vector2(48f, 78f);
    public float pageDotsY = -330f;                              // 캐러셀 중심 기준
    public Vector2 infoCardPosition = new Vector2(600f, 10f);
    public Vector2 infoCardSize = new Vector2(640f, 790f);
    [Min(1)] public int cardNameFontSize = 44;
    [Min(1)] public int cardQuoteFontSize = 24;
    [Min(1)] public int infoNameFontSize = 40;
    [Min(1)] public int infoBodyFontSize = 24;
    [Min(1)] public int infoHeaderFontSize = 28;
    public Vector2 statCellSize = new Vector2(30f, 30f);
    public float statCellGap = 6f;

    [Header("# 스테이지 선택")]
    public Vector2 stageListPosition = new Vector2(-560f, -10f);
    public Vector2 stageListSize = new Vector2(600f, 720f);
    public Vector2 stageRowSize = new Vector2(540f, 132f);
    public float stageRowGap = 14f;
    public Vector2 stageDetailPosition = new Vector2(400f, -10f);
    public Vector2 stageDetailSize = new Vector2(860f, 720f);
    public Vector2 stageImageSize = new Vector2(720f, 240f);
    [Min(1)] public int stageNameFontSize = 40;
    [Min(1)] public int stageBodyFontSize = 26;
    public Vector2 stageIconSize = new Vector2(56f, 56f);

    [Header("# 설정")]
    public Vector2 tabButtonSize = new Vector2(300f, 88f);
    public Vector2 tabFirstPosition = new Vector2(-640f, 230f);
    public float tabSpacing = 108f;
    public Vector2 settingsPanelPosition = new Vector2(190f, -10f);
    public Vector2 settingsPanelSize = new Vector2(1180f, 640f);
    public float settingRowHeight = 110f;
    public Vector2 sliderSize = new Vector2(520f, 34f);
    public Vector2 sliderHandleSize = new Vector2(30f, 52f);
    [Min(1)] public int settingFontSize = 32;
}
