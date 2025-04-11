using Fusion;
using UnityEngine;
using System.Collections;
using TMPro;

[RequireComponent(typeof(Renderer))]
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(NetworkObject))]
public class AutoTextureTiling : NetworkBehaviour
{
    public Vector2 tileSize = new Vector2(1, 1);

    private bool alreadyProcessed = false;

    public override void Spawned()
    {
        Debug.Log($"Trigger tag1: {gameObject.tag}");
        ApplyTiling();

        // StateAuthority（ホストなど）がタグ変更処理を行う
        if (Object.HasStateAuthority)
        {
            StartCoroutine(ChangeTagAfterDelay(0.1f));
        }
    }

    void ApplyTiling()
    {
        Renderer renderer = GetComponent<Renderer>();
        Vector3 scale = transform.lossyScale;
        Vector2 tiling = new Vector2(scale.x / tileSize.x, scale.z / tileSize.y);
        renderer.material.mainTextureScale = tiling;
    }

    private void OnTriggerStay(Collider other)
    {
        if (alreadyProcessed)
            return;

        if (other.CompareTag("Contents"))
        {
            if (Object.HasStateAuthority)
            {
                alreadyProcessed = true;
                Runner.Despawn(Object);
            }
        }
    }

    // 0.1秒待ってからタグを変更するコルーチン
    private IEnumerator ChangeTagAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        gameObject.tag = "Contents";
        Debug.Log($"Trigger tag2: {gameObject.tag}");
        this.alreadyProcessed = true;
    }
}
