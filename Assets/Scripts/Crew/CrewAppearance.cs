using UnityEngine;

[DisallowMultipleComponent]
public sealed class CrewAppearance : MonoBehaviour
{
    [Header("Renderers")]
    [SerializeField] private SpriteRenderer eyesRenderer;
    [SerializeField] private SpriteRenderer mouthRenderer;
    [SerializeField] private SpriteRenderer clothesRenderer;
    [SerializeField] private SpriteRenderer hatRenderer;

    [Header("Shared")]
    [SerializeField] private Sprite[] eyes;
    [SerializeField] private Sprite[] mouths;

    [Header("Player — Pirates")]
    [SerializeField] private Sprite[] pirateClothes;
    [SerializeField] private Sprite[] pirateHats;

    [Header("Enemy — British")]
    [SerializeField] private Sprite[] britishClothes;
    [SerializeField] private Sprite[] britishHats;

    private void Awake()
    {
        ShipIdentity ship = GetComponentInParent<ShipIdentity>();

        if (ship == null)
        {
            Debug.LogWarning($"{name}: cannot choose crew appearance because ShipIdentity was not found.", this);
            return;
        }

        SetRandomSprite(eyesRenderer, eyes);
        SetRandomSprite(mouthRenderer, mouths);

        bool isPlayer = ship.Team == ShipTeam.Player;
        SetRandomSprite(clothesRenderer, isPlayer ? pirateClothes : britishClothes);
        SetRandomSprite(hatRenderer, isPlayer ? pirateHats : britishHats);
    }

    private static void SetRandomSprite(SpriteRenderer renderer, Sprite[] options)
    {
        if (renderer == null || options == null || options.Length == 0)
        {
            return;
        }

        renderer.sprite = options[Random.Range(0, options.Length)];
    }
}
