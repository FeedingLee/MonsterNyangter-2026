using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    [Header("# Checking Is Playing")]
    public GameObject Need_GameResult_Obj;
    public GameObject Need_Player_Obj;
    [Header("# Game Playing BGM")]
    public List<AudioClip> bgmClip = new List<AudioClip>();
    [Header("# Waiting Room BGM")]
    public List<AudioClip> WaitingbgmClip = new List<AudioClip>();
    public float bgmVolume;
    [HideInInspector] public AudioSource bgmPlayer;
    [HideInInspector] public AudioSource WaitingbgmPlayer;
    AudioHighPassFilter bgmEffect;
    // BGM 마지막 재생곡을 체크하기 위한 값
    [HideInInspector] public int LastIndex = -1;
    private const float BgmEndDelay = 0.5f;     // 곡이 끝났다고 판단하기까지 멈춰 있어야 하는 시간(초)
    private float bgmSilentSince = -1f;         // BGM이 멈춘 것을 처음 확인한 시각

    [Header("# SFX")]
    public AudioClip[] sfxClips;
    public float sfxVolume;
    public int channels;
    public AudioSource[] sfxPlayers;
    int channelIndex;

    public enum Sfx { 
        Enemy_Dead = 0,
        Enemy_Hit = 3, 
        Player_LevelUp = 6, 
        Game_Lose = 7, 
        Weapon_Melee = 8,
        Select = 11,
        Weapon_Range = 12,
        Game_Win = 15,
        NyanEaster = 16,
        Weapon_Sniper = 17,
        Weapon_GreatSword = 20,
        Weapon_ChargeMod = 23,
        Anj_FireShoot,
        Anj_Rock,
        Anj_Blade,
        Anj_DashYelling,
        Anj_Dash,
        Anj_Landing,
        Anj_Dead,
        Anj_Jump,
        Exe_Get,
        Player_hit
    }

    private void Awake()
    {
        bgmPlayer = GetComponent<AudioSource>();
        WaitingbgmPlayer = GetComponent<AudioSource>();
        instance = this;
        Init();

        WaitBgmPlay();
    }

    private void Init()
    {
        // BGM Player 생성
        GameObject bgmObject = new GameObject("BgmPlayer");
        bgmObject.transform.parent = transform;
        bgmPlayer = bgmObject.AddComponent<AudioSource>();
        bgmPlayer.playOnAwake = false;
        bgmPlayer.loop = false;
        bgmPlayer.volume = bgmVolume;
        bgmEffect = Camera.main.GetComponent<AudioHighPassFilter>();

        // Waitingbgm Player 생성
        GameObject WaitRoomBGM = new GameObject("Waiting_BgmPlayer");
        WaitRoomBGM.transform.parent = transform;
        WaitingbgmPlayer = WaitRoomBGM.AddComponent<AudioSource>();
        WaitingbgmPlayer.playOnAwake = true;
        WaitingbgmPlayer.loop = true;
        WaitingbgmPlayer.volume = bgmVolume;

        // SFX Player 생성
        GameObject sfxObject = new GameObject("SfxPlayer");
        sfxObject.transform.parent = transform;
        sfxPlayers = new AudioSource[channels];

        for (int index = 0; index < sfxPlayers.Length; index++)
        {
            sfxPlayers[index] = sfxObject.AddComponent<AudioSource>();
            sfxPlayers[index].playOnAwake = false;
            sfxPlayers[index].bypassListenerEffects = true;
            sfxPlayers[index].volume = sfxVolume;
        }

        // 모든 오디오 클립 미리 로드
        foreach (var clip in bgmClip)
        {
            clip.LoadAudioData();
        }

        foreach (var clip in WaitingbgmClip)
        {
            clip.LoadAudioData();
        }
    }

    public void PlayBgm(bool isPlay)
    {
        if (isPlay)
        {
            WaitingbgmPlayer.Stop();
            RandomPlay();
        }
        else 
        {
            bgmPlayer.Stop();
        }
    }

    public void EffectBgm(bool isPlay)
    {
        bgmEffect.enabled = isPlay;
    }

    // 인스펙터에서 직접 지정한 효과음을 현재 SFX 볼륨으로 재생
    public void PlayClip(AudioClip clip)
    {
        if (clip == null)
            return;

        sfxPlayers[channelIndex].PlayOneShot(clip);
    }

    // 중간 위치부터 재생 (파일 앞부분 무음 건너뛰기용, 효과음 볼륨 적용)
    public void PlayClipFrom(AudioClip clip, float startTime)
    {
        if (clip == null)
            return;
        if (startTime <= 0f || startTime >= clip.length)
        {
            PlayClip(clip);
            return;
        }
        AudioSource src = new GameObject("Sfx From " + clip.name).AddComponent<AudioSource>();
        src.transform.SetParent(transform, false);
        src.playOnAwake = false;
        src.bypassListenerEffects = true;
        src.volume = sfxVolume;
        src.clip = clip;
        src.time = startTime;
        src.Play();
        Destroy(src.gameObject, clip.length - startTime + 0.1f);
    }

    // 빈 채널이 없어도 누락되지 않아야 하는 효과음 재생
    public void PlaySfxPriority(Sfx sfx)
    {
        PlayClip(sfxClips[(int)sfx]);
    }

    public void PlaySfx(Sfx sfx)
    {
        for (int index = 0; index < sfxPlayers.Length; index++)
        {
            int loopIndex = (index + channelIndex) % sfxPlayers.Length;

            if (sfxPlayers[loopIndex].isPlaying)
                continue;

            int ranIndex = 0;
            if (sfx == Sfx.Enemy_Dead || sfx == Sfx.Enemy_Hit 
                || sfx == Sfx.Weapon_Melee || sfx == Sfx.Weapon_Range || sfx == Sfx.Weapon_Sniper || sfx == Sfx.Weapon_GreatSword)
            {
                ranIndex = Random.Range(0, 3);
            }

            channelIndex = loopIndex;
            sfxPlayers[loopIndex].clip = sfxClips[(int)sfx + ranIndex];
            sfxPlayers[loopIndex].Play();
            break;
        }
    }
    
    public void Update()
    {   // Player가 On이고, GameResult가 OFF면 게임진행중이니 다시 랜덤 노래 재생
        if (Need_GameResult_Obj.gameObject.activeInHierarchy == false
            && Need_Player_Obj.gameObject.activeInHierarchy == true
            && HasBgmFinished())
        {
            RandomPlay();
        }
    }

    // 창 전환 후 돌아온 직후에는 isPlaying이 잠시 false일 수 있으므로, 일정 시간 계속 멈춰 있을 때만 곡이 끝난 것으로 판단
    private bool HasBgmFinished()
    {
        if (bgmPlayer.isPlaying)
        {
            bgmSilentSince = -1f;
            return false;
        }

        if (bgmSilentSince < 0f)
        {
            bgmSilentSince = Time.unscaledTime;
            return false;
        }

        return Time.unscaledTime - bgmSilentSince >= BgmEndDelay;
    }

    // 노래 랜덤재생 함수
    public void RandomPlay()
    {
        int RanNum = Random.Range(0, bgmClip.Count);

        while (RanNum == LastIndex)
        {
            RanNum = Random.Range(0, bgmClip.Count);
        }

        bgmPlayer.clip = bgmClip[RanNum];
        LastIndex = RanNum;

        bgmPlayer.Play();
    }

    public void WaitBgmPlay()
    {
        // 게임 결과 오브젝트가 비활성화 + 플레이어 오브젝트가 비활성화 = 게임중이 아님.
        if (Need_GameResult_Obj.gameObject.activeInHierarchy == false
            && Need_Player_Obj.gameObject.activeInHierarchy == false)
        {
            WaitingbgmPlayer.clip = WaitingbgmClip[0];
            WaitingbgmPlayer.Play();
        }
    }
}