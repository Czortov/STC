using UnityEngine;

[RequireComponent(typeof(Camera))]
public sealed class StaticShipCamera : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform enemyShipTarget;

    [Header("Framing")]
    [SerializeField] private Vector3 enemyCameraOffset = new Vector3(0f, 0f, -10f);
    [SerializeField, Min(0.1f)] private float enemyOrthographicSize = 6f;

    private void Start()
    {
        if (enemyShipTarget == null)
        {
            Debug.LogError(
                "StaticShipCamera: Enemy Ship Target is not assigned.",
                this
            );
            enabled = false;
            return;
        }

        transform.position = enemyShipTarget.position + enemyCameraOffset;

        Camera shipCamera = GetComponent<Camera>();
        shipCamera.orthographic = true;
        shipCamera.orthographicSize = enemyOrthographicSize;
    }

    private void OnValidate()
    {
        enemyOrthographicSize = Mathf.Max(0.1f, enemyOrthographicSize);
    }
}
