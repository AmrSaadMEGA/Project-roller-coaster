using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Audio;

[CreateAssetMenu(fileName = "NewSound", menuName = "Audio/SoundData")]
public class SoundData : ScriptableObject
{
	public AudioClip audioClip;
	public string soundName;
	public AudioMixerGroup audioMixerGroup;
	public List<AudioMixerSnapshot> audioMixerSnapshots;
	public Dictionary<string, AudioMixerSnapshot> getSnapShots = new Dictionary<string, AudioMixerSnapshot>();
	[Range(0f, 30f)] public List<float> transitonDurations;
	public Dictionary<string, float> getDurations = new Dictionary<string, float>();
	[HideInInspector]
	public List<string> getDurationsElements = new List<string>();
	[Range(0, 1)] public float volume = 0.265f;
	[Range(0, 0.5f)] public float volumeRandomness = 0;
	[Range(0f, 2f)] public float pitch = 1;
	[Range(0f, 0.5f)] public float pitchRandomness = 0;
	[Range(0f, 1f)] public float spatialBlend = 0;
	public float minDistance = 1;
	public float maxDistance = 500;
	public float dopplerLevel = 0.1f;
	public bool loop = false;
	public bool repeatitive = false;
	[Range(0f, 30f)] public float fadedStart = 0; // Time to fade in
	[Range(0f, 30f)] public float fadedStop = 0;  // Time to fade out
	public List<SoundData> waitForSounds;// Add a list of sounds this sound should wait for 
	public List<SoundData> preventSounds; // Add a list of sounds this sound should prevent from playing
	private void OnValidate()
	{
		if (AudioHandler.instance != null)
		{
			AudioHandler.instance.ApplySoundDataChanges(this);
		}
	}
}
