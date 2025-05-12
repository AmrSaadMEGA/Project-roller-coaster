using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
	public static InventoryManager Instance;

	private Dictionary<string, int> inventory = new Dictionary<string, int>();

	private void Awake()
	{
		if (Instance == null)
			Instance = this;
	}

	public void AddItem(string itemName)
	{
		if (inventory.ContainsKey(itemName))
			inventory[itemName]++;
		else
			inventory[itemName] = 1;

		Debug.Log($"Added: {itemName} (now x{inventory[itemName]})");
	}

	public void ShowInventory()
	{
		foreach (var kvp in inventory)
		{
			Debug.Log($"Inventory: {kvp.Key} x{kvp.Value}");
		}
	}

}
