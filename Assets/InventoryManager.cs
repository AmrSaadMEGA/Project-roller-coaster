using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
	public static InventoryManager Instance;

	[Header("UI Slots (4)")]
	[SerializeField] private InventorySlot[] uiSlots = new InventorySlot[4];

	private Dictionary<string, int> inventory = new Dictionary<string, int>();

	private void Awake()
	{
		if (Instance == null)
			Instance = this;
		else
			Destroy(gameObject);

		UpdateUI(); // Show empty slots on start
	}

	public void AddItem(string itemName)
	{
		if (inventory.ContainsKey(itemName))
			inventory[itemName]++;
		else
			inventory[itemName] = 1;

		Debug.Log($"[Inventory] Added {itemName} → x{inventory[itemName]}");
		UpdateUI();
	}

	private void UpdateUI()
	{
		var entries = new List<KeyValuePair<string, int>>(inventory);

		for (int i = 0; i < uiSlots.Length; i++)
		{
			if (i < entries.Count)
			{
				var kvp = entries[i];
				uiSlots[i].SetSlot(kvp.Key, kvp.Value);
			}
			else
			{
				uiSlots[i].SetSlot("", 0); // Empty slot
			}
		}
	}

	public void ShowInventory()
	{
		Debug.Log("Current Inventory:");
		foreach (var kvp in inventory)
			Debug.Log($" - {kvp.Key} x{kvp.Value}");
	}
}
