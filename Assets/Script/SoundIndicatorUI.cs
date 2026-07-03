using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace StarterAssets
{
    public class SoundIndicatorUI : MonoBehaviour
    {
        public static SoundIndicatorUI Instance { get; private set; }

        [Header("References")]
        public Image soundIndicatorImage;
        public CanvasGroup canvasGroup;

        [Header("Visual Settings")]
        public Color veryQuietColor = new Color(0.5f, 0.5f, 1f, 0.5f); // Light blue
        public Color quietColor = new Color(0.5f, 1f, 0.5f, 0.6f);     // Light green
        public Color mediumColor = new Color(1f, 1f, 0.3f, 0.8f);      // Yellow
        public Color loudColor = new Color(1f, 0.3f, 0.3f, 1f);        // Red

        [Header("Animation")]
        public float displayDuration = 0.5f;
        public float fadeSpeed = 5f;
        public float pulseSpeed = 2f;
        public float maxScale = 1.2f;

        private Coroutine currentAnimation;
        private Vector3 originalScale;

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

            if (soundIndicatorImage != null)
            {
                originalScale = soundIndicatorImage.transform.localScale;
            }

            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            // Start invisible
            canvasGroup.alpha = 0f;
        }

        public void ShowSoundIndicator(SoundType soundType)
        {
            if (soundIndicatorImage == null) return;

            // Set color based on sound type
            Color targetColor = GetColorForSoundType(soundType);
            soundIndicatorImage.color = targetColor;

            // Stop previous animation
            if (currentAnimation != null)
            {
                StopCoroutine(currentAnimation);
            }

            // Start new animation
            currentAnimation = StartCoroutine(AnimateSoundIndicator(soundType));
        }

        private IEnumerator AnimateSoundIndicator(SoundType soundType)
        {
            // Fade in and pulse
            float elapsed = 0f;
            
            while (elapsed < displayDuration)
            {
                elapsed += Time.deltaTime;

                // Fade in
                canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 1f, fadeSpeed * Time.deltaTime);

                // Pulse effect based on sound intensity
                float pulseAmount = GetPulseAmount(soundType);
                float pulse = Mathf.Sin(elapsed * pulseSpeed * Mathf.PI * 2) * pulseAmount;
                float scale = 1f + pulse;
                soundIndicatorImage.transform.localScale = originalScale * scale;

                yield return null;
            }

            // Fade out
            while (canvasGroup.alpha > 0.01f)
            {
                canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 0f, fadeSpeed * Time.deltaTime);
                yield return null;
            }

            canvasGroup.alpha = 0f;
            soundIndicatorImage.transform.localScale = originalScale;
        }

        private Color GetColorForSoundType(SoundType soundType)
        {
            switch (soundType)
            {
                case SoundType.VeryQuiet: return veryQuietColor;
                case SoundType.Quiet: return quietColor;
                case SoundType.Medium: return mediumColor;
                case SoundType.Loud: return loudColor;
                default: return Color.white;
            }
        }

        private float GetPulseAmount(SoundType soundType)
        {
            switch (soundType)
            {
                case SoundType.VeryQuiet: return 0.05f;
                case SoundType.Quiet: return 0.1f;
                case SoundType.Medium: return 0.15f;
                case SoundType.Loud: return 0.2f;
                default: return 0f;
            }
        }
    }
}