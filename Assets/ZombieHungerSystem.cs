// ZombieHungerSystem.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro; // Add this if using TextMeshPro

public class ZombieHungerSystem : MonoBehaviour
{
    [Header("Hunger Settings")]
    [SerializeField] private float maxHunger = 100f;
    [SerializeField] private float hungerIncreaseRate = 8f;
    [SerializeField] private float hungerDecreasePerHead = 25f;
    
    [Header("UI References")]
    [SerializeField] private Slider hungerSlider;
    [SerializeField] private Image hungerFillImage;
    [SerializeField] private Color lowHungerColor = Color.green;
    [SerializeField] private Color highHungerColor = Color.red;
    [SerializeField] private TMP_Text headCountText; // Or use UnityEngine.UI.Text

    private float currentHunger;
    private int headsEaten;
    private RollerCoasterGameManager gameManager;

    private void Awake()
    {
        gameManager = FindFirstObjectByType<RollerCoasterGameManager>();
        InitializeHungerUI();
    }

    private void Start()
    {
        currentHunger = 0f;
        headsEaten = 0;
        UpdateAllUI();
    }

    private void Update()
    {
		IncreaseHunger(hungerIncreaseRate * Time.deltaTime);
	}

    public void DecreaseHunger()
    {
        currentHunger = Mathf.Clamp(currentHunger - hungerDecreasePerHead, 0f, maxHunger);
        headsEaten++;
        UpdateAllUI();
    }

    public void ResetHungerSystem()
    {
        currentHunger = 0f;
        headsEaten = 0;
        UpdateAllUI();
    }

    private void InitializeHungerUI()
    {
        if (hungerSlider != null)
        {
            hungerSlider.minValue = 0f;
            hungerSlider.maxValue = maxHunger;
        }
        UpdateAllUI();
    }

    private void IncreaseHunger(float amount)
    {
        currentHunger = Mathf.Clamp(currentHunger + amount, 0f, maxHunger);
        UpdateAllUI();

        if (currentHunger >= maxHunger)
        {
            gameManager.GameOverDueToHunger();
        }
    }

    private void UpdateAllUI()
    {
        UpdateHungerUI();
        UpdateHeadCounterUI();
    }

    private void UpdateHungerUI()
    {
        if (hungerSlider != null)
        {
            hungerSlider.value = currentHunger;
            hungerFillImage.color = Color.Lerp(lowHungerColor, highHungerColor, currentHunger / maxHunger);
        }
    }

    private void UpdateHeadCounterUI()
    {
        if (headCountText != null)
        {
            headCountText.text = $"{headsEaten}";
        }
    }

    // For testing in editor
    private void OnValidate()
    {
        maxHunger = Mathf.Clamp(maxHunger, 1f, float.MaxValue);
        hungerIncreaseRate = Mathf.Clamp(hungerIncreaseRate, 0f, float.MaxValue);
        hungerDecreasePerHead = Mathf.Clamp(hungerDecreasePerHead, 0f, maxHunger);
    }
}