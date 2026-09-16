using System.Collections;
using UnityEngine;

/// <summary>
/// Plays the intro background music when the game starts, then automatically
/// switches to the ghost-normal-state music (looped) once the intro clip
/// finishes OR a maximum duration has elapsed - whichever happens first.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioManager : MonoBehaviour
{
    [Header("Audio Clips")]
    [Tooltip("Plays once when the level/game first starts.")]
    public AudioClip introMusic;

    [Tooltip("Loops continuously once the intro finishes.")]
    public AudioClip ghostNormalMusic;

    [Header("Settings")]
    [Tooltip("Maximum time (in seconds) the intro music is allowed to play before cutting to the ghost normal loop.")]
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

        // Wait for whichever is shorter: the intro clip's actual length,
        // or the maxIntroDuration cutoff (default 3 seconds per spec).
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