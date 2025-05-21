using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class MenuButton
{
	public string buttonName = "Button";
	public enum ButtonType { Normal, WebLink, SceneLoad }
	public ButtonType buttonType = ButtonType.Normal;
	public string linkUrl = "https://example.com";
	public string sceneToLoad = "";
	public UnityEngine.Events.UnityEvent onClick;
}

public class MainMenuController : MonoBehaviour
{
	[Header("Menu Panels")]
	[SerializeField] private GameObject mainMenuPanel;
	[SerializeField] private GameObject creditsPanel;
	[SerializeField] private GameObject instructionsPanel;

	[Header("Menu Toggle Button")]
	[SerializeField] private GameObject menuToggleButton;

	[Header("Game UI Elements")]
	[SerializeField] private List<GameObject> gameUIElements = new List<GameObject>();
	[Tooltip("UI elements that should be hidden when menu is open and visible when menu is closed")]

	[Header("Custom Buttons")]
	[SerializeField] private List<MenuButton> customButtons = new List<MenuButton>();
	[Tooltip("Define custom buttons with special functionality like opening URLs")]

	private float previousTimeScale;
	private bool isMenuOpen = true;
	private bool isFirstOpen = true; // Flag to track first opening

	private void Awake()
	{
		// Ensure all components are properly referenced
		if (mainMenuPanel == null || creditsPanel == null || instructionsPanel == null)
		{
			Debug.LogError("MainMenuController: Menu panels not assigned in inspector!");
		}

		if (menuToggleButton == null)
		{
			Debug.LogError("MainMenuController: Menu toggle button not assigned in inspector!");
		}
	}

	private void Start()
	{
		// Store the current time scale (likely 1.0 at game start)
		previousTimeScale = Time.timeScale;

		// Initialize the menu - pause the game when menu is shown
		Time.timeScale = 0f;

		// Show main menu, hide others
		ShowMainMenu();

		// Hide the toggle button when menu is first opened
		menuToggleButton.SetActive(false);

		// Hide all game UI elements when menu is open
		SetGameUIElementsActive(false);

		// Setup custom buttons if needed
		SetupCustomButtons();

		isFirstOpen = true;
	}

	private void SetupCustomButtons()
	{
		foreach (MenuButton menuButton in customButtons)
		{
			// This is just initialization logic
			// The actual button configuration happens in the editor
			// or through code that calls the public methods
		}
	}

	#region Menu Navigation

	public void ShowMainMenu()
	{

		mainMenuPanel.SetActive(true);
		creditsPanel.SetActive(false);
		instructionsPanel.SetActive(false);
		isMenuOpen = true;

		// Always pause the game when in any menu
		Time.timeScale = 0f;
	}

	public void ShowCredits()
	{
		mainMenuPanel.SetActive(false);
		creditsPanel.SetActive(true);
		instructionsPanel.SetActive(false);
	}

	public void ShowInstructions()
	{
		mainMenuPanel.SetActive(false);
		creditsPanel.SetActive(false);
		instructionsPanel.SetActive(true);
	}

	public void StartGame()
	{
		// Close the menu and resume gameplay
		CloseMenu();
		bool playThemeMusic = true;
		foreach (string audioSource in AudioHandler.instance.playingSoundNames)
		{
			if (audioSource == "Theme Music")
			{
				playThemeMusic = false;
				break;
			}
		}
		if (playThemeMusic)
		{
			AudioHandler.instance.Play("Theme Music");
		}
		if (isFirstOpen)
		{
			// First time clicking play - can optionally load a scene here
			// SceneManager.LoadScene("GameScene");
			isFirstOpen = false;
		}

		// If staying in the same scene, show the menu toggle button
		menuToggleButton.SetActive(true);
	}

	public void QuitGame()
	{

		Application.Quit();
	}

	#endregion

	#region Menu Toggle

	public void ToggleMenu()
	{
		if (isMenuOpen)
		{
			CloseMenu();
		}
		else
		{
			OpenMenu();
		}
	}

	public void OpenMenu()
	{
		// Store current time scale before pausing
		if (Time.timeScale > 0)
		{
			previousTimeScale = Time.timeScale;
		}
		else
		{
			// If time scale was already 0, set a default when we resume
			previousTimeScale = 1f;
		}

		// Show main menu panel
		mainMenuPanel.SetActive(true);
		creditsPanel.SetActive(false);
		instructionsPanel.SetActive(false);

		// Pause the game
		Time.timeScale = 0f;

		isMenuOpen = true;
		menuToggleButton.SetActive(false);

		// Hide game UI elements
		SetGameUIElementsActive(false);
	}

	public void CloseMenu()
	{
		// Hide all menu panels
		mainMenuPanel.SetActive(false);
		creditsPanel.SetActive(false);
		instructionsPanel.SetActive(false);

		// Resume the game with previous time scale (default to 1 if it was 0)
		Time.timeScale = previousTimeScale > 0 ? previousTimeScale : 1f;

		isMenuOpen = false;
		menuToggleButton.SetActive(true);

		// Show game UI elements
		SetGameUIElementsActive(true);
	}

	private void SetGameUIElementsActive(bool active)
	{
		// Toggle all registered game UI elements
		foreach (GameObject uiElement in gameUIElements)
		{
			if (uiElement != null)
			{
				uiElement.SetActive(active);
			}
		}
	}

	#endregion

	#region UI Management

	public void AddGameUIElement(GameObject uiElement)
	{
		if (uiElement != null && !gameUIElements.Contains(uiElement))
		{
			gameUIElements.Add(uiElement);
			// Set initial state based on current menu state
			uiElement.SetActive(!isMenuOpen);
		}
	}

	public void RemoveGameUIElement(GameObject uiElement)
	{
		if (uiElement != null && gameUIElements.Contains(uiElement))
		{
			gameUIElements.Remove(uiElement);
		}
	}

	public void ClearGameUIElements()
	{
		gameUIElements.Clear();
	}

	#endregion

	#region Button Functions

	public void OpenURL(string url)
	{
		if (!string.IsNullOrEmpty(url))
		{
			Application.OpenURL(url);
			Debug.Log("Opening URL: " + url);
		}
	}
	public void DewaInsta()
	{

		Application.OpenURL("https://www.instagram.com/amrsaadmega/");
	}
	public void DewaPay()
	{

		Application.OpenURL("http://paypal.me/amrsa3dmega");
	}
	public void WolfieSite()
	{

		Application.OpenURL(" https://www.wolfieportifolio.com/");
	}
	public void WolfieStore()
	{

		Application.OpenURL("https://wolfu0.itch.io/");
	}

	public void LoadScene(string sceneName)
	{
		if (!string.IsNullOrEmpty(sceneName))
		{
			Time.timeScale = 1f; // Reset time scale before loading new scene
			SceneManager.LoadScene(sceneName);
		}
	}

	public void HandleButtonClick(int buttonIndex)
	{
		if (buttonIndex >= 0 && buttonIndex < customButtons.Count)
		{
			MenuButton button = customButtons[buttonIndex];

			switch (button.buttonType)
			{
				case MenuButton.ButtonType.WebLink:
					OpenURL(button.linkUrl);
					break;

				case MenuButton.ButtonType.SceneLoad:
					LoadScene(button.sceneToLoad);
					break;

				case MenuButton.ButtonType.Normal:
				default:
					button.onClick?.Invoke();
					break;
			}
		}
	}

	#endregion

	// For debugging purposes
	public void LogTimeScale()
	{
		Debug.Log("Current Time Scale: " + Time.timeScale);
		Debug.Log("Previous Time Scale: " + previousTimeScale);
	}
}