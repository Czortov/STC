using UnityEngine;

public sealed class CannonProjectile : MonoBehaviour
{
    private const string HitEffectResourcePath = "HitEffect";
    private const string OverviewHighlightLayerName = "OverviewHighlight";
    private const float OverviewHighlightScale = 8f;

    private static Texture2D projectileTexture;
    private static Sprite projectileSprite;
    private static ProjectileHitEffect hitEffectPrefab;
    private static bool attemptedToLoadHitEffect;

    private ShipRoomRuntime targetRoom;
    private CannonAmmoDefinition ammo;

    private float remainingLifetime;
    private float impactDistance;
    private float flightDuration;
    private float elapsedFlightTime;
    private Vector3 startPosition;

    private bool initialized;
    private bool hasImpacted;

    public static CannonProjectile Create(
        Vector3 position,
        ShipRoomRuntime targetRoom,
        CannonAmmoDefinition ammo)
    {
        if (targetRoom == null)
        {
            Debug.LogError(
                "Невозможно создать снаряд: " +
                "целевой отсек не назначен."
            );

            return null;
        }

        if (ammo == null)
        {
            Debug.LogError(
                "Невозможно создать снаряд: " +
                "тип боеприпаса не назначен."
            );

            return null;
        }

        GameObject projectileObject =
            new GameObject(
                $"Projectile_{ammo.DisplayName}"
            );

        projectileObject.transform.position = position;

        float projectileSize =
            ammo.ProjectileSize;

        projectileObject.transform.localScale =
            new Vector3(
                projectileSize,
                projectileSize,
                1f
            );

        SpriteRenderer spriteRenderer =
            projectileObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite =
            GetProjectileSprite();

        spriteRenderer.color =
            ammo.ProjectileColor;

        spriteRenderer.sortingOrder = 30;

        CreateOverviewHighlight(
            projectileObject.transform,
            spriteRenderer.sprite,
            ammo.ProjectileColor
        );

        /*
         * Физические компоненты не создаём.
         * Снаряд должен игнорировать все промежуточные
         * коллайдеры и долететь до выбранного отсека.
         */

        CannonProjectile projectile =
            projectileObject.AddComponent<CannonProjectile>();

        projectile.Initialize(
            targetRoom,
            ammo
        );

        return projectile;
    }

    private void Initialize(
        ShipRoomRuntime newTargetRoom,
        CannonAmmoDefinition newAmmo)
    {
        targetRoom = newTargetRoom;
        ammo = newAmmo;

        float distanceToTarget = Vector3.Distance(
            transform.position,
            GetRoomWorldCenter(targetRoom)
        );

        float travelTime =
            distanceToTarget / ammo.ProjectileSpeed;

        startPosition = transform.position;
        flightDuration = Mathf.Max(0.01f, travelTime);
        elapsedFlightTime = 0f;

        remainingLifetime = Mathf.Max(
            ammo.ProjectileLifetime,
            travelTime + 1f
        );

        impactDistance =
            Mathf.Max(
                0.03f,
                ammo.ProjectileSize * 0.4f
            );

        initialized = true;
    }

    private void Update()
    {
        if (!initialized || hasImpacted)
        {
            return;
        }

        if (targetRoom == null || ammo == null)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 targetPosition =
            GetRoomWorldCenter(targetRoom);

        targetPosition.z =
            transform.position.z;

        elapsedFlightTime += Time.deltaTime;

        float flightProgress = Mathf.Clamp01(
            elapsedFlightTime / flightDuration
        );

        float arcOffset =
            4f * ammo.ProjectileArcHeight *
            flightProgress * (1f - flightProgress);

        transform.position = Vector3.Lerp(
            startPosition,
            targetPosition,
            flightProgress
        );
        transform.position += Vector3.up * arcOffset;

        float distanceSquared =
            (transform.position - targetPosition)
            .sqrMagnitude;

        if (flightProgress >= 1f ||
            distanceSquared <= impactDistance * impactDistance)
        {
            Impact();
            return;
        }

        remainingLifetime -= Time.deltaTime;

        if (remainingLifetime <= 0f)
        {
            Debug.LogWarning(
                $"{ammo.DisplayName}: снаряд исчез " +
                "до достижения выбранного отсека.",
                this
            );

            Destroy(gameObject);
        }
    }

    private void Impact()
    {
        if (hasImpacted)
        {
            return;
        }

        hasImpacted = true;

        if (targetRoom != null &&
            ammo != null)
        {
            ammo.ApplyImpact(
                targetRoom,
                transform.position
            );

            CreateHitEffect(transform.position);
        }

        Destroy(gameObject);
    }

    private void CreateHitEffect(Vector3 impactPosition)
    {
        ProjectileHitEffect prefab = GetHitEffectPrefab();

        if (prefab == null)
        {
            Debug.LogError(
                $"{name}: HitEffect Prefab is not assigned. " +
                $"Place it at Resources/{HitEffectResourcePath}.prefab.",
                this
            );
            return;
        }

        Instantiate(
            prefab,
            impactPosition,
            Quaternion.identity
        );
    }

    private static ProjectileHitEffect GetHitEffectPrefab()
    {
        if (!attemptedToLoadHitEffect)
        {
            attemptedToLoadHitEffect = true;
            hitEffectPrefab =
                Resources.Load<ProjectileHitEffect>(
                    HitEffectResourcePath
                );
        }

        return hitEffectPrefab;
    }

    private static Vector3 GetRoomWorldCenter(
        ShipRoomRuntime room)
    {
        Collider2D roomCollider =
            room.GetComponent<Collider2D>();

        if (roomCollider != null &&
            roomCollider.enabled)
        {
            return roomCollider.bounds.center;
        }

        return room.transform.position;
    }

    private static Sprite GetProjectileSprite()
    {
        if (projectileSprite != null)
        {
            return projectileSprite;
        }

        const int textureSize = 32;

        projectileTexture =
            new Texture2D(
                textureSize,
                textureSize,
                TextureFormat.RGBA32,
                false
            );

        projectileTexture.name =
            "RuntimeCannonballTexture";

        projectileTexture.filterMode =
            FilterMode.Bilinear;

        float center =
            (textureSize - 1) * 0.5f;

        float radius =
            textureSize * 0.46f;

        float radiusSquared =
            radius * radius;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float offsetX = x - center;
                float offsetY = y - center;

                float distanceSquared =
                    offsetX * offsetX +
                    offsetY * offsetY;

                Color pixelColor =
                    distanceSquared <= radiusSquared
                        ? Color.white
                        : Color.clear;

                projectileTexture.SetPixel(
                    x,
                    y,
                    pixelColor
                );
            }
        }

        projectileTexture.Apply();

        projectileTexture.hideFlags =
            HideFlags.HideAndDontSave;

        projectileSprite = Sprite.Create(
            projectileTexture,
            new Rect(
                0f,
                0f,
                textureSize,
                textureSize
            ),
            new Vector2(0.5f, 0.5f),
            textureSize
        );

        projectileSprite.name =
            "RuntimeCannonballSprite";

        projectileSprite.hideFlags =
            HideFlags.HideAndDontSave;

        return projectileSprite;
    }

    private static void CreateOverviewHighlight(
        Transform projectileTransform,
        Sprite sprite,
        Color projectileColor)
    {
        int highlightLayer =
            LayerMask.NameToLayer(OverviewHighlightLayerName);

        if (highlightLayer < 0)
        {
            Debug.LogWarning(
                $"Layer {OverviewHighlightLayerName} is not configured."
            );
            return;
        }

        GameObject highlightObject =
            new GameObject("OverviewHighlight");

        highlightObject.layer = highlightLayer;
        highlightObject.transform.SetParent(projectileTransform, false);
        highlightObject.transform.localScale =
            Vector3.one * OverviewHighlightScale;

        SpriteRenderer highlightRenderer =
            highlightObject.AddComponent<SpriteRenderer>();

        highlightRenderer.sprite = sprite;
        highlightRenderer.color = new Color(
            Mathf.Max(0.85f, projectileColor.r),
            Mathf.Max(0.65f, projectileColor.g),
            0.15f,
            0.78f
        );
        highlightRenderer.sortingOrder = 29;
    }
}
