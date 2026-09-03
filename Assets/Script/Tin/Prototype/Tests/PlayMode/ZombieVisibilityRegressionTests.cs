using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class ZombieVisibilityRegressionTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<GameObject> objects = new List<GameObject>();
    GameObject Make(string name, Vector2 position)
    {
        var go = new GameObject(name); go.transform.position = position; objects.Add(go); return go;
    }
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static ContactFilter2D Obstacles()
    {
        var filter=new ContactFilter2D {useLayerMask=true,useTriggers=false};
        filter.SetLayerMask(1<<LayerMask.NameToLayer("Obstacle")); return filter;
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        foreach(var go in objects) if(go!=null) UnityEngine.Object.Destroy(go);
        objects.Clear(); yield return null;
    }

    [UnityTest] public IEnumerator NearAwareness_RequiresSameIndoorArea_AndKeepsLitSourcePresentation()
    {
        var origin=new Vector2(1000,1000);
        var cameraObject=Make("Near awareness camera",origin);
        cameraObject.tag="MainCamera";
        var camera=cameraObject.AddComponent<Camera>(); camera.orthographic=true; camera.orthographicSize=5;
        cameraObject.transform.position=new Vector3(origin.x,origin.y,-10);
        var player=Make("Near observer",origin);
        Type visionType=Type.GetType("PlayerVision, Assembly-CSharp");
        var vision=(Behaviour)player.AddComponent(visionType); vision.enabled=false;
        Type roofType=Type.GetType("RoofDetector, Assembly-CSharp");
        var roof=(Behaviour)player.AddComponent(roofType); roof.enabled=false;
        var volumeObject=Make("Observer indoor volume",origin);
        var volume=volumeObject.AddComponent<BoxCollider2D>(); volume.isTrigger=true; volume.size=new Vector2(2.4f,2.4f);
        Type stableAreaType=Type.GetType("IndoorFogSurfaceMap, Assembly-CSharp"); Assert.That(stableAreaType,Is.Not.Null);
        var stableArea=(Behaviour)volumeObject.AddComponent(stableAreaType);
        stableAreaType.GetField("indoorVolume").SetValue(stableArea,volume);
        var containsIndoorPoint=stableAreaType.GetMethod("ContainsIndoorPoint");
        Set(roof,"currentIndoorCollider",volume); Set(vision,"roofDetector",roof);
        var filter=new ContactFilter2D {useLayerMask=true,useTriggers=false}; filter.SetLayerMask(1<<LayerMask.NameToLayer("Enemy"));
        Set(vision,"zombieFilter",filter); Set(vision,"obstacleFilter",Obstacles());
        var wallObject=Make("Near wall",origin+new Vector2(.5f,0)); wallObject.layer=LayerMask.NameToLayer("Obstacle");
        var wall=wallObject.AddComponent<BoxCollider2D>(); wall.size=new Vector2(.1f,4);
        var zombie=Make("Near living zombie",origin+Vector2.right); zombie.layer=LayerMask.NameToLayer("Enemy");
        zombie.AddComponent<CircleCollider2D>().radius=.08f;
        var renderer=zombie.AddComponent<SpriteRenderer>(); renderer.color=Color.white; renderer.enabled=false;
        renderer.sortingLayerName="Gameplay"; renderer.sortingOrder=7;
        var texture=new Texture2D(2,2); texture.SetPixels(new[]{Color.white,Color.white,Color.white,Color.white}); texture.Apply();
        var sprite=Sprite.Create(texture,new Rect(0,0,2,2),new Vector2(.5f,.5f),2); renderer.sprite=sprite;
        Shader litShader=Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        Assert.That(litShader,Is.Not.Null);
        var litMaterial=new Material(litShader); renderer.sharedMaterial=litMaterial;
        Physics2D.SyncTransforms();
        var update=visionType.GetMethod("UpdateZombieVisibility",Private);
        var fillMasks=visionType.GetMethod("FillNearAwarenessFogMasks",BindingFlags.Instance|BindingFlags.NonPublic);
        var clearMasks=visionType.GetMethod("ClearNearAwarenessMasksImmediate",Private);
        var bounds=new Vector4[16]; var strengths=new float[16];
        Assert.That((bool)containsIndoorPoint.Invoke(stableArea,new object[]{(Vector2)zombie.transform.position}),Is.True);
        Assert.That(Physics2D.Linecast(origin,zombie.transform.position,1<<LayerMask.NameToLayer("Obstacle")).collider,Is.EqualTo(wall));
        yield return null;
        update.Invoke(vision,new object[]{140f});
        Assert.That(renderer.enabled,Is.True,"Same-indoor near reveal must ignore internal LOS.");
        Assert.That(renderer.color.a,Is.GreaterThanOrEqualTo(.18f));
        Assert.That(GameObject.Find("Local Zombie Near Awareness Overlay"),Is.Null,
            "Near awareness must not create an unlit duplicate presentation.");
        Assert.That(renderer.sharedMaterial,Is.SameAs(litMaterial));
        Assert.That(renderer.sortingLayerName,Is.EqualTo("Gameplay")); Assert.That(renderer.sortingOrder,Is.EqualTo(7));
        Assert.That(renderer.color.r,Is.EqualTo(1)); Assert.That(renderer.color.g,Is.EqualTo(1)); Assert.That(renderer.color.b,Is.EqualTo(1));
        int maskCount=(int)fillMasks.Invoke(vision,new object[]{bounds,strengths});
        Assert.That(maskCount,Is.EqualTo(1)); Assert.That(strengths[0],Is.GreaterThan(0));
        float previous=renderer.color.a;
        for(int i=0;i<60 && renderer.color.a<.999f;i++) {
            yield return null; update.Invoke(vision,new object[]{140f});
            Assert.That(renderer.color.a,Is.GreaterThanOrEqualTo(previous)); previous=renderer.color.a;
        }
        Assert.That(renderer.color.a,Is.EqualTo(1).Within(.001f));
        // Still within 1.5, but outside the stable indoor volume: no near bypass.
        zombie.transform.position=origin+Vector2.right*1.35f; Physics2D.SyncTransforms();
        Assert.That((bool)containsIndoorPoint.Invoke(stableArea,new object[]{(Vector2)zombie.transform.position}),Is.False);
        for(int i=0;i<60 && renderer.enabled;i++) {
            yield return null; update.Invoke(vision,new object[]{140f});
            Assert.That(renderer.color.a,Is.LessThanOrEqualTo(previous)); previous=renderer.color.a;
        }
        Assert.That(renderer.enabled,Is.False); Assert.That(renderer.color.a,Is.EqualTo(0).Within(.001f));
        for(int i=0;i<20;i++){yield return null;update.Invoke(vision,new object[]{140f});}
        Assert.That((int)fillMasks.Invoke(vision,new object[]{bounds,strengths}),Is.EqualTo(0));
        // Behind the observer, independent of facing.
        zombie.transform.position=origin+Vector2.down; Physics2D.SyncTransforms();
        update.Invoke(vision,new object[]{140f}); Assert.That(renderer.enabled,Is.True);
        clearMasks.Invoke(vision,null);
        Assert.That((int)fillMasks.Invoke(vision,new object[]{bounds,strengths}),Is.EqualTo(0));
        // Outdoor near awareness remains unchanged, including behind an obstacle.
        Set(roof,"currentIndoorCollider",null);
        zombie.transform.position=origin+Vector2.right; Physics2D.SyncTransforms();
        update.Invoke(vision,new object[]{140f}); Assert.That(renderer.enabled,Is.True);
        Assert.That((int)fillMasks.Invoke(vision,new object[]{bounds,strengths}),Is.EqualTo(0));
        renderer.color=new Color(.8f,.25f,.3f,renderer.color.a);
        update.Invoke(vision,new object[]{140f});
        Assert.That(renderer.color.r,Is.EqualTo(.8f)); Assert.That(renderer.color.g,Is.EqualTo(.25f));
        UnityEngine.Object.Destroy(litMaterial);
        UnityEngine.Object.Destroy(sprite); UnityEngine.Object.Destroy(texture);
    }

}
