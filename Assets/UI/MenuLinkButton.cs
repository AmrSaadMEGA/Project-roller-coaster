using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

public class MenuLinkButton : MonoBehaviour
{
    [SerializeField] private string url = "https://example.com";
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text buttonText;
    [SerializeField] private bool showUrlOnHover = true;
    
    private string originalButtonText;
    
    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }
        
        if (buttonText == null)
        {
            buttonText = GetComponentInChildren<TMP_Text>();
        }
        
        if (buttonText != null)
        {
            originalButtonText = buttonText.text;
        }
    }
    
    private void Start()
    {
        if (button != null)
        {
            button.onClick.AddListener(OpenUrl);
            
            if (showUrlOnHover && buttonText != null)
            {
                // Add hover events using event trigger
                EventTrigger eventTrigger = button.gameObject.GetComponent<EventTrigger>();
                if (eventTrigger == null)
                {
                    eventTrigger = button.gameObject.AddComponent<EventTrigger>();
                }
                
                // Setup hover enter event
                EventTrigger.Entry enterEntry = new EventTrigger.Entry();
                enterEntry.eventID = EventTriggerType.PointerEnter;
                enterEntry.callback.AddListener((data) => { OnPointerEnter(); });
                eventTrigger.triggers.Add(enterEntry);
                
                // Setup hover exit event
                EventTrigger.Entry exitEntry = new EventTrigger.Entry();
                exitEntry.eventID = EventTriggerType.PointerExit;
                exitEntry.callback.AddListener((data) => { OnPointerExit(); });
                eventTrigger.triggers.Add(exitEntry);
            }
        }
    }
    
    private void OnPointerEnter()
    {
        if (buttonText != null && showUrlOnHover)
        {
            buttonText.text = url;
        }
    }
    
    private void OnPointerExit()
    {
        if (buttonText != null && showUrlOnHover)
        {
            buttonText.text = originalButtonText;
        }
    }
    
    public void OpenUrl()
    {
        if (!string.IsNullOrEmpty(url))
        {
            Application.OpenURL(url);
            Debug.Log("Opening URL: " + url);
        }
    }
    
    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OpenUrl);
        }
    }
    
    // Public method to change the URL at runtime
    public void SetUrl(string newUrl)
    {
        url = newUrl;
    }
    
    // Public method to change button text at runtime
    public void SetButtonText(string newText)
    {
        if (buttonText != null)
        {
            buttonText.text = newText;
            originalButtonText = newText;
        }
    }
}
