using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class GameplayVisualRegressionTests
{
    [Test]
    public void ObstaclePrefab_UsesTriggerContactInsteadOfPushingThePlayer()
    {
        GameObject obstacle = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Obstacle.prefab");

        Assert.That(obstacle, Is.Not.Null);
        Assert.That(obstacle.GetComponent<Collider>(), Is.Not.Null);
        Assert.That(obstacle.GetComponent<Collider>().isTrigger, Is.True);
    }

    [Test]
    public void ApproachingRocks_UseHighDetailFacetedMeshes()
    {
        for (int variant = 0; variant < ProceduralRockMeshes.RockVariantCount; variant++)
        {
            Mesh rock = ProceduralRockMeshes.Rock(variant);

            Assert.That(rock, Is.Not.Null);
            Assert.That(rock.triangles.Length / 3, Is.GreaterThanOrEqualTo(1200));
        }
    }

    [Test]
    public void ObstacleWarship_UsesLayeredMeshWithoutAddingColliders()
    {
        var parent = new GameObject("WarshipVisualTest");
        var hullMaterial = new Material(Shader.Find("Standard"));
        try
        {
            Transform warship = WarshipObstacleVisual.Create(parent.transform, hullMaterial);
            Mesh mesh = warship.GetComponent<MeshFilter>().sharedMesh;

            Assert.That(mesh.subMeshCount, Is.EqualTo(3));
            Assert.That(mesh.vertexCount, Is.GreaterThan(500));
            Assert.That(parent.GetComponentsInChildren<Collider>(), Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(parent);
            Object.DestroyImmediate(hullMaterial);
        }
    }

    [Test]
    public void PlayerShipDetails_AddFighterSilhouetteWithoutChangingCollision()
    {
        var visual = new GameObject("PlayerShipVisualTest");
        try
        {
            ShipDetailing detailing = visual.AddComponent<ShipDetailing>();
            typeof(ShipDetailing).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(detailing, null);

            Transform wings = visual.transform.Find("SweptArmorWings");
            Assert.That(wings, Is.Not.Null);
            Assert.That(wings.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThanOrEqualTo(16));
            Assert.That(visual.GetComponentsInChildren<Transform>(true).Count(t => t.name == "ForwardCannon"), Is.EqualTo(2));
            Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty);
        }
        finally
        {
            Object.DestroyImmediate(visual);
        }
    }
}
