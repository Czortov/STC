using UnityEngine;

public sealed class CannonProjectile : MonoBehaviour
{
    private static Texture2D projectileTexture;
    private static Sprite projectileSprite;

    private ShipRoomRuntime targetRoom;
    private CannonAmmoDefinition ammo;

    private float remainingLifetime;
    private float impactDistance;

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

        remainingLifetime =
            ammo.ProjectileLifetime;

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

        transform.position =
            Vector3.MoveTowards(
                transform.position,
                targetPosition,
                ammo.ProjectileSpeed *
                Time.deltaTime
            );

        float distanceSquared =
            (transform.position - targetPosition)
            .sqrMagnitude;

        if (distanceSquared <=
            impactDistance * impactDistance)
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
            ammo.ApplyImpact(targetRoom);
        }

        Destroy(gameObject);
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
}