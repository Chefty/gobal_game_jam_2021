﻿using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using UnityEngine.UI;

[RequireComponent(typeof(AudioSource))]
public class SoundtrackManager : MonoBehaviour
{
    private static SoundtrackManager instance;
    private AudioSource audioSource;
    private AudioClip[] ostArray;
    private int previousLevel;
    private Toggle AudioToggle;
    private bool isMuted;

    private void Awake() {
        Debug.Log("01 Awake");
        audioSource = GetComponent<AudioSource>();
        if (SceneManager.GetActiveScene().buildIndex != previousLevel ||
            SceneManager.GetActiveScene().buildIndex == 0)
            OSTHandler();
        previousLevel = SceneManager.GetActiveScene().buildIndex;

        if (!DDOLCheck())
            return;

        if (!isMuted)
        {
            StartCoroutine(MusicFadeIn(2f));
        }
    }

    void OnEnable() => SceneManager.sceneLoaded += OnLevelFinishedLoading;

    void OnDisable() => SceneManager.sceneLoaded -= OnLevelFinishedLoading;

    void OnLevelFinishedLoading(Scene scene, LoadSceneMode mode) {
        Debug.Log(SceneManager.GetActiveScene().buildIndex + " - " + previousLevel);
        AudioMuteHandler();
        InitializeAllAudioSources();
        DisplayProperMuteIcon();
    }

    private bool DDOLCheck() {
        //DDOL
        if (instance == null) {
            instance = this;
            DontDestroyOnLoad(this.gameObject);
            return true;
        } else if (instance != this)
            Destroy(this.gameObject);
        return false;
    }


    private void OSTHandler() {
        Debug.Log("01.1 OSTHandler");
        ostArray = Resources.LoadAll<AudioClip>("OST");
        string sceneNameLowerCase = SceneManager.GetActiveScene().name.ToLower();
        //looking for the dedicated level ost
        for (int i = 0; i < ostArray.Length; i++) {

            string ostNameLowerCase = ostArray[i].name.ToLower();

            if (sceneNameLowerCase.Contains(ostNameLowerCase)) {
                audioSource.clip = ostArray[i];
                StartCoroutine(MusicFadeIn(2f));
                return;
            }
        }
        //no dedicated ost found - load default soundtrack
        audioSource.clip = ostArray.FirstOrDefault(o => o.name.Contains("Default"));
    }

    private void AudioMuteHandler() {
        Debug.Log("03 AudioHandler");
        GameObject audioMute = GameObject.FindGameObjectWithTag("AudioMute");

        if (audioMute) {
            AudioToggle = audioMute.GetComponent<Toggle>();
            AudioToggle.onValueChanged.AddListener(MuteAudio);
        }
    }

    void InitializeAllAudioSources() 
    {
        Debug.Log("04 InitializeAllAudioSources");
        var sources = FindObjectsOfType<AudioSource>();

        foreach (var item in sources)
        {
            if (isMuted)
            {
                item.volume = .0f;
            }
            else
            {
                item.volume = .5f;
            }
        }
    }

    IEnumerator MusicFadeIn(float fadeIn) 
    {
        if (AudioToggle)
            AudioToggle.isOn = isMuted;

        if (!isMuted)
        {
            audioSource.volume = 0.0f;
            audioSource.Play();
            audioSource.DOFade(.5f, fadeIn);
        }

        yield return null;
    }

    void DisplayProperMuteIcon() 
    {
        print("DisplayProperMuteIcon " + isMuted);
        AudioToggle.isOn = isMuted;
    }

    void MuteAudio(bool value)
    {
        print("MuteAudio" + value);
        audioSource.volume = value ? .0f : .5f;
        isMuted = value;
        InitializeAllAudioSources();
    }
}
