using Fusion;
using UnityEngine;
using System.Collections;
using TMPro;

[RequireComponent(typeof(Renderer))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(NetworkObject))]
public class AutoTextureTilingForPath : NetworkBehaviour
{
    public Vector2 tileSize = new Vector2(1, 1);

    private bool alreadyProcessed = false;

    public override void Spawned()
    {
        ApplyTiling();
    }

    void ApplyTiling()
    {
        Renderer renderer = GetComponent<Renderer>();
        Vector3 scale = transform.lossyScale;
        Vector2 tiling = new Vector2(scale.x / tileSize.x, scale.z / tileSize.y);
        renderer.material.mainTextureScale = tiling;
    }
}
