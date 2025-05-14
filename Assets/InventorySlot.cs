using TMPro;
using UnityEngine;
using UnityEngine.UI;    // or TMPro if you use TMP_Text

public class InventorySlot : MonoBehaviour
{
	[Tooltip("The root GameObject (panel) that contains all visuals for this slot.")]
	[SerializeField] private GameObject contentRoot;

	[Tooltip("Text to show the item count.")]
	[SerializeField] private TMP_Text countText;

	/// <summary>
	/// Show or hide this slot. If itemName is empty or count≤0, hides the slot. Otherwise,
	/// enables contentRoot and updates texts.
	/// </summary>
	public void SetSlot(string itemName, int count)
	{
		bool hasItem = !string.IsNullOrEmpty(itemName) && count > 0;
		contentRoot.SetActive(hasItem);

		if (!hasItem)
			return;

		countText.text = (count > 1) ? $"x{count}" : "";
	}
}
