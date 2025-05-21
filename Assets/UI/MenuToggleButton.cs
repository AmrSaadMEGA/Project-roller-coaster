using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class MenuToggleButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
	[SerializeField] private MainMenuController menuController;
	[SerializeField] private Button toggleButton;
	[SerializeField] private TMP_Text buttonText;
	[SerializeField] private string buttonLabel = "Menu";
	[SerializeField] private float fadeTime = 0.5f;
	[SerializeField] private float hoverAlpha = 1.0f;
	[SerializeField] private float idleAlpha = 0.5f;

	private CanvasGroup canvasGroup;
	private Coroutine fadeCoroutine;

	private void Awake()
	{
		if (menuController == null)
		{
			Debug.LogError("MenuToggleButton: MainMenuController reference not set!");
		}

		if (toggleButton == null)
		{
			toggleButton = GetComponent<Button>();
		}

		canvasGroup = GetComponent<CanvasGroup>();
		if (canvasGroup == null)
		{
			canvasGroup = gameObject.AddComponent<CanvasGroup>();
		}

		// Setup button text if available
		if (buttonText != null)
		{
			buttonText.text = buttonLabel;
		}
	}

	private void Start()
	{
		// Add click listener
		toggleButton.onClick.AddListener(ToggleMenu);

		// Set initial alpha
		canvasGroup.alpha = idleAlpha;
	}

	private void ToggleMenu()
	{
		if (menuController != null)
		{
			menuController.ToggleMenu();
		}
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		// Fade to full opacity when hovered
		if (fadeCoroutine != null)
		{
			StopCoroutine(fadeCoroutine);
		}
		fadeCoroutine = StartCoroutine(FadeCanvasGroup(canvasGroup, hoverAlpha, fadeTime));
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		// Fade to semi-transparent when not hovered
		if (fadeCoroutine != null)
		{
			StopCoroutine(fadeCoroutine);
		}
		fadeCoroutine = StartCoroutine(FadeCanvasGroup(canvasGroup, idleAlpha, fadeTime));
	}

	private System.Collections.IEnumerator FadeCanvasGroup(CanvasGroup cg, float targetAlpha, float duration)
	{
		float startAlpha = cg.alpha;
		float elapsed = 0f;

		while (elapsed < duration)
		{
			cg.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
			elapsed += Time.unscaledDeltaTime;
			yield return null;
		}

		cg.alpha = targetAlpha;
	}

	private void OnDestroy()
	{
		// Clean up listener when destroyed
		toggleButton.onClick.RemoveListener(ToggleMenu);
	}
}