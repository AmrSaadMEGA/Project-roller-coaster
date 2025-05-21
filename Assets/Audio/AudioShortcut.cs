using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioShortcut : MonoBehaviour
{
    public static AudioShortcut instance;
    public AudioHandler audioHandler;
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(this);
        }
        if (transform.parent != null)
        {
            audioHandler = GetComponentInParent<AudioHandler>();
        }
    }
    //public void PlayThemeMusic()
    //{
    //    foreach (AudioSource audioSource in AudioHandler.instance.GetComponentsInChildren<AudioSource>())
    //    {
    //        if (audioSource == null) continue;
    //        if (audioSource.name == "Theme Music" && audioSource.isPlaying) return;
    //    }
    //    AudioHandler.instance.Play("Theme Music", null, false, Vector3.zero, Quaternion.identity);
    //}
    //public void StopThemeMusic()
    //{
    //    AudioHandler.instance.Stop("Theme Music", false);
    //}
    //public void PlayMenuMusic()
    //{
    //    foreach (AudioSource audioSource in AudioHandler.instance.GetComponentsInChildren<AudioSource>())
    //    {
    //        if (audioSource == null) continue;
    //        if (audioSource.name == "Menu Music" && audioSource.isPlaying) return;
    //    }
    //    AudioHandler.instance.Play("Menu Music", null, false, Vector3.zero, Quaternion.identity);
    //}
    //public void StopMenuMusic()
    //{
    //    AudioHandler.instance.Stop("Menu Music", false);
    //}
    //public void OnPAKToStartSFX()
    //{
    //    AudioHandler.instance.Play("PAKTS", null, false, Vector3.zero, Quaternion.identity);
    //}
    //public void PlayRobotAmbienceSFX(GameObject player)
    //{
    //    audioHandler.Play("Robot Ambience", player, false, Vector3.zero, Quaternion.identity);
    //}
    //public void CleatAllAudio()
    //{
    //    audioHandler.Stop("Robot Ambience", true);
    //    audioHandler.Stop("Robot Ambience 2", true);
    //    audioHandler.Stop("Thrust Loop", true);
    //    audioHandler.Stop("Thrust Start", true);
    //    audioHandler.Stop("Thrust Stop", true);
    //}
    //public void StopRobotAmbienceSFX()
    //{
    //    audioHandler.Stop("Robot Ambience", false);
    //}
    //public void PlayRobotAmbience2SFX(GameObject player)
    //{
    //    audioHandler.Play("Robot Ambience 2", player, false, Vector3.zero, Quaternion.identity);
    //}
    //public void StopRobotAmbience2SFX()
    //{

    //    audioHandler.Stop("Robot Ambience 2", false);
    //}
    //public void StartThrustLoop(GameObject player)
    //{
    //    audioHandler.Play("Thrust Loop", player, false, Vector3.zero, Quaternion.identity);
    //}
    //public void StopThrustloop()
    //{
    //    audioHandler.Stop("Thrust Loop", false);
    //}
    //public void ThrustStart(GameObject player)
    //{
    //    audioHandler.Play("Thrust Start", player, false, Vector3.zero, Quaternion.identity);
    //}
    //public void ThrustStop(GameObject player)
    //{
    //    audioHandler.Play("Thrust Stop", player, false, Vector3.zero, Quaternion.identity);
    //}
    //public void Shoot(GameObject player, bool specifiedPos, Vector3 sPos, Quaternion sRot)
    //{
    //    audioHandler.Play("Shoot", player, specifiedPos, sPos, sRot);
    //}
    //public void ShootReset(GameObject player, bool specifiedPos, Vector3 sPos, Quaternion sRot)
    //{
    //    audioHandler.Play("Shoot Reset", player, specifiedPos, sPos, sRot);
    //}
    //public void NoBullets(GameObject player, bool specifiedPos, Vector3 sPos, Quaternion sRot)
    //{
    //    audioHandler.Play("No Bullets", player, specifiedPos, sPos, sRot);
    //}
    //public void Reload(GameObject player, bool specifiedPos, Vector3 sPos, Quaternion sRot)
    //{
    //    audioHandler.Play("Reload", player, specifiedPos, sPos, sRot);
    //}
    //public void EnvironmentBulletImpact(GameObject collisionObj, bool specifiedPos, Vector3 sPos, Quaternion sRot)
    //{
    //    audioHandler.Play("Environment Bullet Impact", collisionObj, specifiedPos, sPos, sRot);
    //}
    //public void MetalBulletImpact(GameObject collisionObj, bool specifiedPos, Vector3 sPos, Quaternion sRot)
    //{
    //    audioHandler.Play("Metal Bullet Impact", collisionObj, specifiedPos, sPos, sRot);
    //}
    //public void Spawn(GameObject Obj, bool specifiedPos, Vector3 sPos, Quaternion sRot)
    //{
    //    audioHandler.Play("Spawn", Obj, specifiedPos, sPos, sRot);
    //}
    //public void Explosion(GameObject Obj, bool specifiedPos, Vector3 sPos, Quaternion sRot)
    //{
    //    audioHandler.Play("Explosion", Obj, specifiedPos, sPos, sRot);
    //}
}
