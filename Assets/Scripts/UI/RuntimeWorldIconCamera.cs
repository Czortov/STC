using UnityEngine;
using UnityEngine.UI;

internal sealed class RuntimeWorldIconCamera : MonoBehaviour
{
    private const int TextureSize = 128;
    private const float CameraDistance = 10f;

    private Camera iconCamera;
    private RenderTexture renderTexture;
    private RawImage output;
    private Transform target;

    internal static RuntimeWorldIconCamera Create(
        string cameraName,
        RawImage targetOutput,
        Transform followedTarget,
        float orthographicSize)
    {
        GameObject cameraObject = new GameObject(cameraName);
        cameraObject.hideFlags = HideFlags.HideAndDontSave;

        Camera camera = cameraObject.AddComponent<Camera>();
        RuntimeWorldIconCamera follower =
            cameraObject.AddComponent<RuntimeWorldIconCamera>();

        follower.Initialize(
            camera,
            targetOutput,
            followedTarget,
            orthographicSize
        );

        return follower;
    }

    internal void SetTarget(Transform followedTarget)
    {
        target = followedTarget;

        if (iconCamera != null)
        {
            iconCamera.enabled = target != null;
        }
    }

    internal void Dispose()
    {
        if (Application.isPlaying)
        {
            Destroy(gameObject);
        }
        else
        {
            DestroyImmediate(gameObject);
        }
    }

    private void Initialize(
        Camera camera,
        RawImage targetOutput,
        Transform followedTarget,
        float orthographicSize)
    {
        iconCamera = camera;
        output = targetOutput;
        target = followedTarget;

        renderTexture = new RenderTexture(
            TextureSize,
            TextureSize,
            16,
            RenderTextureFormat.ARGB32
        )
        {
            name = $"{name}_Texture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        renderTexture.Create();

        iconCamera.orthographic = true;
        iconCamera.orthographicSize = Mathf.Max(0.01f, orthographicSize);
        iconCamera.clearFlags = CameraClearFlags.SolidColor;
        iconCamera.backgroundColor = Color.clear;
        iconCamera.nearClipPlane = 0.01f;
        iconCamera.farClipPlane = 100f;
        iconCamera.allowHDR = false;
        iconCamera.allowMSAA = false;
        iconCamera.targetTexture = renderTexture;

        int uiLayer = LayerMask.NameToLayer("UI");
        iconCamera.cullingMask = uiLayer >= 0
            ? ~(1 << uiLayer)
            : ~0;

        iconCamera.enabled = target != null;

        if (output != null)
        {
            output.texture = renderTexture;
            output.color = Color.white;
            output.raycastTarget = false;
        }

        FollowTarget();
    }

    private void LateUpdate()
    {
        FollowTarget();
    }

    private void FollowTarget()
    {
        if (iconCamera == null || target == null)
        {
            if (iconCamera != null)
            {
                iconCamera.enabled = false;
            }

            return;
        }

        iconCamera.enabled = true;

        Vector3 targetPosition = target.position;
        transform.position = new Vector3(
            targetPosition.x,
            targetPosition.y,
            targetPosition.z - CameraDistance
        );
        transform.rotation = Quaternion.identity;
    }

    private void OnDestroy()
    {
        if (output != null && output.texture == renderTexture)
        {
            output.texture = null;
        }

        if (iconCamera != null)
        {
            iconCamera.targetTexture = null;
        }

        if (renderTexture == null)
        {
            return;
        }

        renderTexture.Release();

        if (Application.isPlaying)
        {
            Destroy(renderTexture);
        }
        else
        {
            DestroyImmediate(renderTexture);
        }
    }
}

internal static class RuntimeUiVisuals
{
    internal static void ApplySlicedSprite(Image image, Sprite sprite)
    {
        if (image == null || sprite == null)
        {
            return;
        }

        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
    }

    internal static void ApplyUndimmedButton(
        Button button,
        Image background,
        Sprite sprite)
    {
        if (background == null || sprite == null)
        {
            return;
        }

        ApplySlicedSprite(background, sprite);

        if (button == null)
        {
            return;
        }

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;
        colors.colorMultiplier = 1f;
        button.colors = colors;
        button.targetGraphic = background;
    }
}
