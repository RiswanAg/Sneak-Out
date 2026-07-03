using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace StarterAssets
{
    /// <summary>
    /// UI for Stamina Boost visual feedback
    /// Shows when player has unlimited stamina from Soda
    /// </summary>
    public class StaminaBoostUI : MonoBehaviour
    {
        public static StaminaBoostUI Instance { get; private set; }
        
        [Header("UI Elements")]
        [Tooltip("Panel that shows during boost")]
        public GameObject boostPanel;
        
        [Tooltip("Text showing remaining time")]
        public TMP_Text timerText;
        
        [Tooltip("Icon/Image for boost")]
        public Image boostIcon;
        
        [Tooltip("Fill image for countdown")]
        public Image countdownFill;
        
        [Header("Colors")]
        public Color boostActiveColor = Color.cyan;
        public Color boostEndingColor = Color.yellow;
        
        [Header("Animation")]
        public float pulseSpeed = 2f;
        public float pulseScale = 1.1f;
        
        [Header("Audio")]
        public AudioSource audioSource;
        public AudioClip boostStartSound;
        public AudioClip boostEndSound;
        public AudioClip tickSound;
        
        private float boostDuration;
        private float remainingTime;
        private bool isBoostActive = false;
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
                return;
            }
            
            if (boostPanel != null)
            {
                boostPanel.SetActive(false);
                originalScale = boostPanel.transform.localScale;
            }
        }
        
        /// <summary>
        /// Show boost UI with countdown
        /// </summary>
        public void ShowBoost(float duration)
        {
            boostDuration = duration;
            remainingTime = duration;
            isBoostActive = true;
            
            if (boostPanel != null)
                boostPanel.SetActive(true);
            
            // Play start sound
            if (audioSource != null && boostStartSound != null)
                audioSource.PlayOneShot(boostStartSound);
            
            GameLog.Log($"[StaminaBoostUI] Showing boost for {duration} seconds");
        }
        
        /// <summary>
        /// Hide boost UI
        /// </summary>
        public void HideBoost()
        {
            isBoostActive = false;
            
            if (boostPanel != null)
                boostPanel.SetActive(false);
            
            // Play end sound
            if (audioSource != null && boostEndSound != null)
                audioSource.PlayOneShot(boostEndSound);
            
            GameLog.Log("[StaminaBoostUI] Boost ended");
        }
        
        void Update()
        {
            if (!isBoostActive) return;
            
            remainingTime -= Time.deltaTime;
            
            // Update timer text
            if (timerText != null)
            {
                timerText.text = Mathf.CeilToInt(remainingTime).ToString();
            }
            
            // Update countdown fill
            if (countdownFill != null)
            {
                countdownFill.fillAmount = remainingTime / boostDuration;
            }
            
            // Change color when ending (last 3 seconds)
            if (remainingTime <= 3f)
            {
                if (boostIcon != null)
                    boostIcon.color = boostEndingColor;
                if (timerText != null)
                    timerText.color = boostEndingColor;
                
                // Play tick sound
                if (remainingTime <= 3f && Mathf.FloorToInt(remainingTime) != Mathf.FloorToInt(remainingTime + Time.deltaTime))
                {
                    if (audioSource != null && tickSound != null)
                        audioSource.PlayOneShot(tickSound);
                }
            }
            else
            {
                if (boostIcon != null)
                    boostIcon.color = boostActiveColor;
                if (timerText != null)
                    timerText.color = boostActiveColor;
            }
            
            // Pulse animation
            if (boostPanel != null)
            {
                float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * (pulseScale - 1f) * 0.5f;
                boostPanel.transform.localScale = originalScale * pulse;
            }
            
            // Auto hide when time runs out
            if (remainingTime <= 0)
            {
                HideBoost();
            }
        }
    }
}