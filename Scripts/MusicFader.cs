using System.Collections;
using UnityEngine;

public class MusicFader : MonoBehaviour
{
    public static MusicFader Instance { get; private set; }

    [Header("Referencia al AudioSource de la música")]
    [SerializeField] private AudioSource musicSource;

    [Header("Volúmenes")]
    [SerializeField] private float normalVolume = 0.1f;
    [SerializeField] private float combatVolume = 0.4f;

    [Header("Tiempo de fade (segundos)")]
    [SerializeField] private float fadeDuration = 1.5f;

    [Header("Clips opcionales")]
    [SerializeField] private AudioClip normalMusicClip;
    [SerializeField] private AudioClip combatMusicClip;
    [SerializeField] private AudioClip bossMusicClip;   // <<-- AQUI ARRastras la música del boss

    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (!musicSource)
            musicSource = GetComponent<AudioSource>();

        if (musicSource != null)
        {
            // Si no tiene clip pero sí normalMusicClip, lo ponemos
            if (musicSource.clip == null && normalMusicClip != null)
            {
                musicSource.clip = normalMusicClip;
                musicSource.Play();
            }

            musicSource.volume = normalVolume;
        }
    }

    // ===== API PÚBLICA =====
    public void FadeToCombat()
    {
        // si combatMusicClip es null, solo cambia volumen
        StartFade(combatVolume, combatMusicClip);
    }

    public void FadeToNormal()
    {
        StartFade(normalVolume, normalMusicClip);
    }

    public void FadeToBoss()
    {
        // volumen de boss: puedes usar combatVolume o crear otro campo
        StartFade(combatVolume, bossMusicClip);
    }

    // ===== LÓGICA DE FADE =====
    private void StartFade(float targetVolume, AudioClip newClip = null)
    {
        if (musicSource == null) return;

        if (fadeRoutine != null)
            StopCoroutine(fadeRoutine);

        fadeRoutine = StartCoroutine(FadeVolumeCoroutine(targetVolume, newClip));
    }

    private IEnumerator FadeVolumeCoroutine(float targetVolume, AudioClip newClip)
    {
        if (musicSource == null) yield break;

        float startVolume = musicSource.volume;
        float t = 0f;

        bool changeClip = (newClip != null && newClip != musicSource.clip);

        // 1) Si vamos a cambiar de clip, primero hacemos fade OUT hasta 0
        if (changeClip)
        {
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                float factor = t / fadeDuration;
                musicSource.volume = Mathf.Lerp(startVolume, 0f, factor);
                yield return null;
            }

            musicSource.volume = 0f;
            // Cambiamos el clip y reproducimos
            musicSource.clip = newClip;
            musicSource.Play();

            // preparamos para el fade IN
            startVolume = 0f;
            t = 0f;
        }

        // 2) Fade (desde el volumen actual) hasta el volumen objetivo
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float factor = t / fadeDuration;
            musicSource.volume = Mathf.Lerp(startVolume, targetVolume, factor);
            yield return null;
        }

        musicSource.volume = targetVolume;
        fadeRoutine = null;
    }
}
