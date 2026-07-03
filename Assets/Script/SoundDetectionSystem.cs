using UnityEngine;
using System.Collections.Generic;

public enum SoundType
{
    Silent,      // No sound
    VeryQuiet,   // Sneaking (2m range)
    Quiet,       // Slow movements (5m range)
    Medium,      // Walking (10m range)
    Loud         // Running, jumping (20m range)
}

public class SoundDetectionSystem : MonoBehaviour
{
    public static SoundDetectionSystem Instance { get; private set; }

    [Header("Sound Settings")]
    [Tooltip("Enable to see sound radius in Scene view")]
    public bool showDebugRadius = true;

    [Header("Sound Ranges")]
    public float veryQuietRange = 2f;    // Sneaking
    public float quietRange = 5f;         // Slow movement
    public float mediumRange = 10f;       // Walking
    public float loudRange = 20f;         // Running, jumping

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip[] walkFootsteps;
    public AudioClip[] runFootsteps;
    public AudioClip[] sneakFootsteps;
    public AudioClip jumpSound;
    public AudioClip landSound;

    [Range(0f, 1f)]
    public float walkVolume = 0.5f;
    [Range(0f, 1f)]
    public float runVolume = 0.8f;
    [Range(0f, 1f)]
    public float sneakVolume = 0.2f;

    private List<ISoundListener> listeners = new List<ISoundListener>();
    private Vector3 lastSoundPosition;
    private float lastSoundRange;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0f; // 2D sound
        }
    }

    /// <summary>
    /// Register an NPC to listen for sounds
    /// </summary>
    public void RegisterListener(ISoundListener listener)
    {
        if (!listeners.Contains(listener))
        {
            listeners.Add(listener);
        }
    }

    /// <summary>
    /// Unregister an NPC
    /// </summary>
    public void UnregisterListener(ISoundListener listener)
    {
        if (listeners.Contains(listener))
        {
            listeners.Remove(listener);
        }
    }

    /// <summary>
    /// Emit a sound at a position with a specific type
    /// </summary>
    public void EmitSound(Vector3 position, SoundType soundType, GameObject source = null)
    {
        float range = GetSoundRange(soundType);
        
        if (range <= 0) return; // Silent sound

        lastSoundPosition = position;
        lastSoundRange = range;

        // Notify all listeners within range
        foreach (var listener in listeners)
        {
            if (listener == null) continue;

            float distance = Vector3.Distance(position, listener.GetPosition());

            if (distance <= range)
            {
                listener.OnSoundHeard(position, soundType, distance, source);
            }
        }

        // Show UI indicator
        //if (SoundIndicatorUI.Instance != null)
        //{
            //SoundIndicatorUI.Instance.ShowSoundIndicator(soundType);
        //}
    }

    /// <summary>
    /// Play footstep sound based on movement type
    /// </summary>
    public void PlayFootstepSound(SoundType soundType)
    {
        if (audioSource == null) return;

        AudioClip clip = null;
        float volume = 1f;

        switch (soundType)
        {
            case SoundType.VeryQuiet:
                if (sneakFootsteps != null && sneakFootsteps.Length > 0)
                    clip = sneakFootsteps[Random.Range(0, sneakFootsteps.Length)];
                volume = sneakVolume;
                break;

            case SoundType.Medium:
                if (walkFootsteps != null && walkFootsteps.Length > 0)
                    clip = walkFootsteps[Random.Range(0, walkFootsteps.Length)];
                volume = walkVolume;
                break;

            case SoundType.Loud:
                if (runFootsteps != null && runFootsteps.Length > 0)
                    clip = runFootsteps[Random.Range(0, runFootsteps.Length)];
                volume = runVolume;
                break;
        }

        if (clip != null)
        {
            audioSource.PlayOneShot(clip, volume);
        }
    }

    /// <summary>
    /// Play jump sound
    /// </summary>
    public void PlayJumpSound()
    {
        if (audioSource != null && jumpSound != null)
        {
            audioSource.PlayOneShot(jumpSound, runVolume);
        }
    }

    /// <summary>
    /// Play landing sound
    /// </summary>
    public void PlayLandSound()
    {
        if (audioSource != null && landSound != null)
        {
            audioSource.PlayOneShot(landSound, runVolume);
        }
    }

    private float GetSoundRange(SoundType soundType)
    {
        switch (soundType)
        {
            case SoundType.Silent: return 0f;
            case SoundType.VeryQuiet: return veryQuietRange;
            case SoundType.Quiet: return quietRange;
            case SoundType.Medium: return mediumRange;
            case SoundType.Loud: return loudRange;
            default: return 0f;
        }
    }

    void OnDrawGizmos()
    {
        if (!showDebugRadius) return;

        // Draw the last sound position and range
        if (lastSoundRange > 0)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(lastSoundPosition, lastSoundRange);
        }
    }
}

/// <summary>
/// Interface for objects that can hear sounds
/// </summary>
public interface ISoundListener
{
    Vector3 GetPosition();
    void OnSoundHeard(Vector3 soundPosition, SoundType soundType, float distance, GameObject source);
}