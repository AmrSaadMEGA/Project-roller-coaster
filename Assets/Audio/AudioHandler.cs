using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using System.Linq;

public class AudioHandler : MonoBehaviour
{
	public AudioMixer audioMixer;
	public static AudioHandler instance;
	public List<SoundData> soundDataList; // List of SoundData ScriptableObjects
	public List<SoundData> unrepetitveSounds;
	private Dictionary<string, List<AudioSource>> audioSources = new Dictionary<string, List<AudioSource>>(); // Mapping sound names to list of AudioSources
	public HashSet<string> playingSoundNames = new HashSet<string>(); // Track currently playing sound types
	private Dictionary<string, SoundData> soundDataByName = new Dictionary<string, SoundData>(); // Mapping sound names to SoundData
	private Dictionary<AudioSource, string> sourceToSoundName = new Dictionary<AudioSource, string>(); // Track which sound name each source belongs to

	// Maximum number of simultaneous instances per sound
	[Tooltip("Maximum instances of the same sound that can play simultaneously")]
	public int maxInstancesPerSound = 5;

	// Lists to track audio states
	public List<AudioSource> fadingInSources = new List<AudioSource>();
	public List<AudioSource> fadingOutSources = new List<AudioSource>();
	public List<AudioSource> currentlyPlayingSources = new List<AudioSource>();
	public List<string> notPlayingClips = new List<string>();

	// For unique instance tracking
	private int instanceCounter = 0;


	private void Awake()
	{
		if (instance == null)
		{
			instance = this;
			DontDestroyOnLoad(gameObject);
		}
		else
		{
			Destroy(gameObject);
		}
	}

	private void Start() => InitializeAudioSources();

	private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

	private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => InitializeAudioSources();

	private void InitializeAudioSources()
	{
		// Clear existing mappings to prevent duplicates
		soundDataByName.Clear();

		foreach (var soundData in soundDataList)
		{
			soundDataByName[soundData.soundName] = soundData; // Initialize soundDataByName

			if (!audioSources.ContainsKey(soundData.soundName))
			{
				audioSources[soundData.soundName] = new List<AudioSource>();

				if (soundData.repeatitive)
				{
					soundData.getSnapShots.Clear();
					soundData.getDurationsElements.Clear();
					soundData.getDurations.Clear();

					// Create initial audio source
					var audioSource = CreateAudioSource(soundData);
					audioSources[soundData.soundName].Add(audioSource);
					notPlayingClips.Add(soundData.soundName); // Add all clips to not playing initially
				}
			}
		}
	}

	private AudioSource CreateAudioSource(SoundData soundData)
	{
		// Create a unique name for this instance
		instanceCounter++;
		string instanceName = $"{soundData.soundName}_instance_{instanceCounter}";

		var soundObject = new GameObject(instanceName);
		soundObject.transform.parent = transform;
		var audioSource = soundObject.AddComponent<AudioSource>();
		ConfigureAudioSource(audioSource, soundData);

		// Track which sound this source belongs to
		sourceToSoundName[audioSource] = soundData.soundName;

		return audioSource;
	}

	private void ConfigureAudioSource(AudioSource audioSource, SoundData soundData)
	{
		if (soundData.repeatitive == false)
		{
			if (!unrepetitveSounds.Contains(soundData))
				unrepetitveSounds.Add(soundData);
			return;
		}

		audioSource.outputAudioMixerGroup = soundData.audioMixerGroup;

		// Clear and repopulate snapshots
		soundData.getSnapShots.Clear();
		soundData.getDurationsElements.Clear();
		soundData.getDurations.Clear();

		foreach (AudioMixerSnapshot snapshot in soundData.audioMixerSnapshots)
		{
			soundData.getSnapShots.Add(snapshot.name, snapshot);
			soundData.getDurationsElements.Add(snapshot.name);
		}

		for (int i = 0; i < soundData.transitonDurations.Count && i < soundData.getDurationsElements.Count; i++)
		{
			soundData.getDurations.Add(soundData.getDurationsElements[i], soundData.transitonDurations[i]);
		}

		audioSource.clip = soundData.audioClip;
		audioSource.loop = soundData.loop;
		audioSource.spatialBlend = soundData.spatialBlend;
		audioSource.minDistance = soundData.minDistance;
		audioSource.maxDistance = soundData.maxDistance;
		audioSource.dopplerLevel = soundData.dopplerLevel;
		audioSource.volume = soundData.volume;
		audioSource.pitch = soundData.pitch;
	}

	private void RandomizeAudioSource(AudioSource audioSource, SoundData soundData)
	{
		// Store original values
		float originalVolume = audioSource.volume;
		float originalPitch = audioSource.pitch;

		// Apply randomization
		float randomizedVolume = originalVolume * Random.Range(1 - soundData.volumeRandomness, 1 + soundData.volumeRandomness);
		float randomizedPitch = originalPitch * Random.Range(1 - soundData.pitchRandomness, 1 + soundData.pitchRandomness);

		// Set values directly on the audio source
		audioSource.volume = randomizedVolume;
		audioSource.pitch = randomizedPitch;
	}

	private HashSet<string> loggedWarnings = new HashSet<string>(); // Track logged warnings

	private void LogWarningOnce(string message)
	{
		if (loggedWarnings.Add(message))
		{
			Debug.LogWarning(message);
		}
	}

	public AudioSource GetAudio(string soundName)
	{
		if (audioSources.TryGetValue(soundName, out List<AudioSource> sources) && sources.Count > 0)
		{
			// Return the first available source
			return sources.FirstOrDefault(s => s != null);
		}
		return null;
	}

	// Get all audio sources for a sound
	public List<AudioSource> GetAllAudioSources(string soundName)
	{
		if (audioSources.TryGetValue(soundName, out List<AudioSource> sources))
		{
			return sources.Where(s => s != null).ToList();
		}
		return new List<AudioSource>();
	}

	// Find an available audio source or create a new one
	private AudioSource GetAvailableAudioSource(SoundData soundData)
	{
		string soundName = soundData.soundName;

		if (!audioSources.ContainsKey(soundName))
		{
			audioSources[soundName] = new List<AudioSource>();
		}

		List<AudioSource> sources = audioSources[soundName];

		// First, try to find a source that's not playing
		AudioSource availableSource = sources.FirstOrDefault(s => s != null && !s.isPlaying &&
															 !fadingInSources.Contains(s) &&
															 !fadingOutSources.Contains(s));

		// If no available source and we haven't reached max instances, create a new one
		if (availableSource == null && sources.Count < maxInstancesPerSound)
		{
			availableSource = CreateAudioSource(soundData);
			sources.Add(availableSource);
		}

		// If still no available source, find the oldest one that isn't fading
		if (availableSource == null)
		{
			availableSource = sources.FirstOrDefault(s => s != null &&
													 !fadingInSources.Contains(s) &&
													 !fadingOutSources.Contains(s));
		}

		// If still no available source, just pick the first one
		if (availableSource == null && sources.Count > 0)
		{
			availableSource = sources[0];
		}

		return availableSource;
	}

	public void Play(string soundName)
	{
		if (!soundDataByName.TryGetValue(soundName, out var soundData))
		{
			LogWarningOnce($"Sound '{soundName}' not found in soundDataByName!");
			return;
		}

		// Clean up unused sources
		if (notPlayingClips.Count > 0 && unrepetitveSounds.Count > 0)
		{
			CleanUpUnusedAudioSources();
		}

		// Stop any sounds in the prevent list
		foreach (var preventSound in soundData.preventSounds)
		{
			StopAllInstances(preventSound.soundName);
		}

		// Get available audio source
		AudioSource audioSource = GetAvailableAudioSource(soundData);
		if (audioSource == null) return;

		// If source is fading out, stop the fade out
		if (fadingOutSources.Contains(audioSource))
		{
			// Don't call StopCoroutine directly as it won't work for specific instance
			// Instead remove from tracking list and let the coroutine check this
			fadingOutSources.Remove(audioSource);
		}

		// Configure audio source (in case it's an existing one being reused)
		ConfigureAudioSource(audioSource, soundData);

		// Apply randomization
		RandomizeAudioSource(audioSource, soundData);

		// If already playing, stop if it's being reused
		if (audioSource.isPlaying)
		{
			audioSource.Stop();
		}

		// Update state trackers
		if (!notPlayingClips.Contains(soundName))
		{
			notPlayingClips.Add(soundName);
		}
		if (!playingSoundNames.Contains(soundName))
		{
			playingSoundNames.Add(soundName);
		}

		// Add to currently playing list
		if (!currentlyPlayingSources.Contains(audioSource))
		{
			currentlyPlayingSources.Add(audioSource);
		}

		// Handle faded start
		if (soundData.fadedStart > 0)
		{
			StartFadeIn(audioSource, soundData);
		}
		else
		{
			// Direct play
			audioSource.Play();
		}
	}

	private void StopImmediate(AudioSource audioSource)
	{
		if (audioSource == null) return;

		// Get the sound name for this source
		string soundName = sourceToSoundName.ContainsKey(audioSource) ? sourceToSoundName[audioSource] : null;
		if (soundName == null) return;

		audioSource.Stop();

		// Remove from tracking lists
		fadingOutSources.Remove(audioSource);
		fadingInSources.Remove(audioSource);
		currentlyPlayingSources.Remove(audioSource);

		// Check if this was the last instance of this sound playing
		bool isLastInstance = !GetAllAudioSources(soundName).Any(s => s != audioSource && s.isPlaying);
		if (isLastInstance)
		{
			playingSoundNames.Remove(soundName);
			if (!notPlayingClips.Contains(soundName))
			{
				notPlayingClips.Add(soundName);
			}
			TriggerWaitingSounds(soundName);
		}
	}

	// Stop all instances of a sound
	public void StopAllInstances(string soundName)
	{
		if (!audioSources.TryGetValue(soundName, out var sources))
		{
			return;
		}

		foreach (var source in sources.Where(s => s != null && s.isPlaying).ToList())
		{
			StopImmediate(source);
		}
	}

	public void Stop(string soundName)
	{
		if (!soundDataByName.TryGetValue(soundName, out var soundData))
		{
			LogWarningOnce($"Sound '{soundName}' not found in soundDataByName!");
			return;
		}

		if (notPlayingClips.Count > 0 && unrepetitveSounds.Count > 0)
		{
			CleanUpUnusedAudioSources();
		}

		if (!audioSources.TryGetValue(soundName, out var sources) || sources.Count == 0)
		{
			return;
		}

		// Stop all playing instances of this sound
		foreach (var audioSource in sources.Where(s => s != null && s.isPlaying).ToList())
		{
			// Check if fade-out is required
			if (soundData.fadedStop > 0)
			{
				if (!fadingOutSources.Contains(audioSource))
				{
					fadingOutSources.Add(audioSource);
					StartCoroutine(FadeOut(audioSource, soundData.fadedStop));
				}
			}
			else
			{
				// Immediate stop
				StopImmediate(audioSource);
			}
		}
	}

	private void StartFadeIn(AudioSource audioSource, SoundData soundData)
	{
		// Store original volume for fading
		float originalVolume = audioSource.volume;
		audioSource.volume = 0;

		// Start playing at zero volume
		audioSource.Play();

		// Update state tracking
		if (!fadingInSources.Contains(audioSource))
		{
			fadingInSources.Add(audioSource);
		}

		// Start fade-in coroutine
		StartCoroutine(FadeIn(audioSource, soundData.fadedStart, originalVolume));
	}

	private IEnumerator FadeIn(AudioSource audioSource, float duration, float targetVolume)
	{
		if (audioSource == null) yield break;

		float startTime = Time.time;
		float endTime = startTime + duration;

		while (Time.time < endTime && audioSource != null && audioSource.isPlaying)
		{
			float elapsedTime = Time.time - startTime;
			float t = elapsedTime / duration;
			audioSource.volume = Mathf.Lerp(0, targetVolume, t);
			yield return null;
		}

		if (audioSource != null && audioSource.isPlaying)
		{
			audioSource.volume = targetVolume;
		}

		// Remove from fading in list
		if (audioSource != null && fadingInSources.Contains(audioSource))
		{
			fadingInSources.Remove(audioSource);
		}

		// Handle non-looping sounds completion tracking in Update method
	}

	private IEnumerator FadeOut(AudioSource audioSource, float duration)
	{
		if (audioSource == null || !audioSource.isPlaying) yield break;

		// Add to fading out tracking if not already there
		if (!fadingOutSources.Contains(audioSource))
		{
			fadingOutSources.Add(audioSource);
		}

		// Remove from fading in if it was fading in
		if (fadingInSources.Contains(audioSource))
		{
			fadingInSources.Remove(audioSource);
		}

		// Get the sound name this source is playing
		string soundName = sourceToSoundName.ContainsKey(audioSource) ? sourceToSoundName[audioSource] : null;
		if (soundName == null) yield break;

		float startVolume = audioSource.volume;
		float startTime = Time.time;
		float endTime = startTime + duration;

		while (Time.time < endTime && audioSource != null && audioSource.isPlaying)
		{
			// Check if source was removed from fading out list (e.g., to play again)
			if (!fadingOutSources.Contains(audioSource))
			{
				yield break;
			}

			float elapsedTime = Time.time - startTime;
			float t = elapsedTime / duration;
			audioSource.volume = Mathf.Lerp(startVolume, 0, t);
			yield return null;
		}

		if (audioSource != null)
		{
			audioSource.Stop();
			audioSource.volume = startVolume; // Reset volume for future use

			// Remove from tracking lists
			currentlyPlayingSources.Remove(audioSource);
			fadingOutSources.Remove(audioSource);

			// Check if this was the last instance of this sound
			bool isLastInstance = !GetAllAudioSources(soundName).Any(s => s != audioSource && s.isPlaying);
			if (isLastInstance)
			{
				playingSoundNames.Remove(soundName);
				if (!notPlayingClips.Contains(soundName))
				{
					notPlayingClips.Add(soundName);
				}
				TriggerWaitingSounds(soundName);
			}
		}
	}

	private void TriggerWaitingSounds(string soundName)
	{
		if (soundDataByName.TryGetValue(soundName, out var soundData))
		{
			foreach (var waitingSound in soundData.waitForSounds)
			{
				Play(waitingSound.soundName);
			}
		}
	}

	private void CleanUpUnusedAudioSources()
	{
		foreach (var soundName in audioSources.Keys.ToList())
		{
			if (!soundDataByName.TryGetValue(soundName, out var soundData)) continue;

			if (!soundData.repeatitive)
			{
				var sources = audioSources[soundName].ToList();
				foreach (var source in sources)
				{
					if (source == null) continue;

					if (!source.isPlaying &&
						!fadingInSources.Contains(source) &&
						!fadingOutSources.Contains(source))
					{
						audioSources[soundName].Remove(source);
						sourceToSoundName.Remove(source);
						unrepetitveSounds.Remove(soundData);
						Destroy(source.gameObject);
					}
				}
			}
		}
	}

	public void ApplySoundDataChanges(SoundData soundData)
	{
		if (audioSources.TryGetValue(soundData.soundName, out var sources))
		{
			foreach (var source in sources.Where(s => s != null))
			{
				ConfigureAudioSource(source, soundData);
			}
		}
		else
		{
			var newSource = CreateAudioSource(soundData);
			audioSources[soundData.soundName] = new List<AudioSource> { newSource };
		}
	}

	// Play a sound at a specific position in 3D space
	public void PlayAtPosition(string soundName, Vector3 position)
	{
		if (!soundDataByName.TryGetValue(soundName, out var soundData))
		{
			LogWarningOnce($"Sound '{soundName}' not found in soundDataByName!");
			return;
		}

		// Get available audio source
		AudioSource audioSource = GetAvailableAudioSource(soundData);
		if (audioSource == null) return;

		// Position the game object in 3D space
		audioSource.transform.position = position;

		// Set the spatial blend to 3D (1.0f) if not already set
		audioSource.spatialBlend = 1.0f;

		// Continue with normal play process
		Play(soundName);
	}

	// Get count of currently playing instances of a sound
	public int GetPlayingInstanceCount(string soundName)
	{
		if (!audioSources.TryGetValue(soundName, out var sources))
		{
			return 0;
		}

		return sources.Count(s => s != null && s.isPlaying);
	}

	// Useful for handling audio completion events
	private void Update()
	{
		List<AudioSource> finishedSources = new List<AudioSource>();

		// Check for finished non-looping sounds
		foreach (var source in currentlyPlayingSources.ToList())
		{
			if (source == null)
			{
				currentlyPlayingSources.Remove(source);
				continue;
			}

			if (!source.isPlaying &&
				!fadingInSources.Contains(source) &&
				!fadingOutSources.Contains(source))
			{
				finishedSources.Add(source);
			}
		}

		// Update states for finished sounds
		foreach (var source in finishedSources)
		{
			// Get the sound name this source was playing
			if (!sourceToSoundName.TryGetValue(source, out string soundName))
			{
				currentlyPlayingSources.Remove(source);
				continue;
			}

			currentlyPlayingSources.Remove(source);

			// Check if this was the last instance of this sound
			bool isLastInstance = !GetAllAudioSources(soundName).Any(s => s != source && s.isPlaying);
			if (isLastInstance)
			{
				playingSoundNames.Remove(soundName);
				if (!notPlayingClips.Contains(soundName))
				{
					notPlayingClips.Add(soundName);
				}
				TriggerWaitingSounds(soundName);
			}
		}
	}
}