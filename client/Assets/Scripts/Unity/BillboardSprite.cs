using UnityEngine;

namespace Qqtang.UnityClient
{
    /// <summary>
    /// Keeps 2D character/effect sprites facing the 3D camera cleanly (2.5D paper-sprite style).
    /// </summary>
    public sealed class BillboardSprite : MonoBehaviour
    {
        private Camera _cachedCamera;

        private void LateUpdate()
        {
            if (_cachedCamera == null) _cachedCamera = Camera.main;
            if (_cachedCamera == null) return;
            transform.forward = _cachedCamera.transform.forward;
        }
    }
}
