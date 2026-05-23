using UnityEngine;

[CreateAssetMenu(fileName = "NewAbilityData", menuName = "DoubleForward/Ability Data")]
public class AbilityData : ScriptableObject
{
    [Header("Basic Info")]
    public string abilityName;
    [TextArea] public string description;
    public Sprite icon;

    [Header("Stats")]
    public float cooldown = 2f;
    public float duration = 3f;
    public float range = 5f;
    public float force = 8f;

    [Header("Visual")]
    public GameObject effectPrefab;
    public AudioClip activateSound;
    public Color abilityColor = Color.white;
}
