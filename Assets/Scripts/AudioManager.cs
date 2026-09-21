using System.Collections;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    [Header("Audio Clips")]
    public AudioClip introMusic;
    public AudioClip ghostNormalMusic;

    [Header("Settings")]
    public float maxIntroDuration = 3f;

    private AudioSource audioSource;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        StartCoroutine(PlayIntroThenLoopNormal());
    }

    private IEnumerator PlayIntroThenLoopNormal()
    {
        if (introMusic == null)
        {
            Debug.LogWarning("AudioManager: introMusic is not assigned.");
        }
        else
        {
            audioSource.clip = introMusic;
            audioSource.loop = false;
            audioSource.Play();
        }

        float clipLength = introMusic != null ? introMusic.length : 0f;
        float waitTime = Mathf.Min(clipLength, maxIntroDuration);

        yield return new WaitForSeconds(waitTime);

        if (ghostNormalMusic == null)
        {
            Debug.LogWarning("AudioManager: ghostNormalMusic is not assigned.");
            yield break;
        }

        audioSource.clip = ghostNormalMusic;
        audioSource.loop = true;
        audioSource.Play();
    }
}