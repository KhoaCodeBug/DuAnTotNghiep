using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Isolated follower + production motor tests. No Main scene, player input,
// perception, steering or Fusion tick: these cases do not claim end-to-end QA.
public class ZombieNavigationRecoveryTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<GameObject> objects = new List<GameObject>();

    static Type FindType(string name)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(name);
            if (type != null) return type;
        }
        throw new InvalidOperationException("Missing type: " + name);
    }

    GameObject Make(string name, Vector2 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        objects.Add(go);
        return go;
    }

    static void Set(object instance, string name, object value) =>
        instance.GetType().GetField(name, Private).SetValue(instance, value);

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (var go in objects)
            if (go != null) UnityEngine.Object.Destroy(go);
        objects.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator Thai_ReachPlayerAroundClearCorner() => FollowClearCorner(true);

    [UnityTest]
    public IEnumerator Khoa_ReachPlayerAroundClearCorner() => FollowClearCorner(false);

    [UnityTest]
    public IEnumerator Thai_ClearRouteDoesNotStall() => FollowClearCorner(true, false);

    [UnityTest]
    public IEnumerator Khoa_ClearRouteDoesNotStall() => FollowClearCorner(false, false);

    [UnityTest]
    public IEnumerator Thai_FollowReplacementRoute() => FollowClearCorner(true, true, true);

    [UnityTest]
    public IEnumerator Khoa_FollowReplacementRoute() => FollowClearCorner(false, true, true);

    [UnityTest]
    public IEnumerator Thai_RecoverBlockedSimplifiedRoute() => FollowClearCorner(true, true, false, true);

    [UnityTest]
    public IEnumerator Khoa_RecoverBlockedSimplifiedRoute() => FollowClearCorner(false, true, false, true);

    [UnityTest]
    public IEnumerator Thai_RecoverSubdividedShortcut() => FollowClearCorner(true, true, false, true, true);

    [UnityTest]
    public IEnumerator Khoa_RecoverSubdividedShortcut() => FollowClearCorner(false, true, false, true, true);

    [UnityTest]
    public IEnumerator Thai_OffsetFootprintStopsAtWall()
    {
        var origin = new Vector2(1000f, 1000f);
        var actor = Make("Thai offset footprint", origin);
        var body = actor.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        var foot = actor.AddComponent<CapsuleCollider2D>();
        foot.size = new Vector2(.092514716f, .19389847f);
        foot.offset = new Vector2(.02890889f, -.18735819f);
        var brainType = FindType("ZombieAI");
        var brain = (Behaviour)actor.AddComponent(brainType);
        brain.enabled = false;
        Set(brain, "movementObstacleFilter", ObstacleFilter());
        var wallObject = Make("Wall at feet", origin + new Vector2(.5f, -.187f));
        wallObject.layer = LayerMask.NameToLayer("Obstacle");
        var wall = wallObject.AddComponent<BoxCollider2D>();
        wall.size = new Vector2(.1f, .2f);
        Physics2D.SyncTransforms();
        var motor = brainType.GetMethod("MoveWithObstacleSweep", Private);
        float moved = (float)motor.Invoke(brain, new object[] { Vector2.right });
        yield return new WaitForFixedUpdate();
        Assert.That(moved, Is.InRange(.30f, .37f));
        Assert.That(Physics2D.Distance(foot, wall).distance, Is.GreaterThan(0f));
        motor.Invoke(brain, new object[] { new Vector2(1f, .05f) });
        yield return new WaitForFixedUpdate();
        Assert.That(Physics2D.Distance(foot, wall).isOverlapped, Is.False);
        Assert.That(body.position.x, Is.LessThan(origin.x + .4f));
    }

    [UnityTest]
    public IEnumerator Thai_MotorIgnoresTriggerHitbox()
    {
        var origin = new Vector2(1000f, 1000f);
        var actor = Make("Thai trigger footprint", origin);
        var body = actor.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        var foot = actor.AddComponent<CapsuleCollider2D>();
        foot.size = new Vector2(.1f, .2f);
        foot.offset = new Vector2(0f, -.2f);
        var hitbox = Make("Damage trigger", origin);
        hitbox.transform.SetParent(actor.transform);
        var trigger = hitbox.AddComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = Vector2.one;
        var brainType = FindType("ZombieAI");
        var brain = (Behaviour)actor.AddComponent(brainType);
        brain.enabled = false;
        Set(brain, "movementObstacleFilter", ObstacleFilter());
        var wallObject = Make("Wall above foot", origin + new Vector2(.4f, .4f));
        wallObject.layer = LayerMask.NameToLayer("Obstacle");
        wallObject.AddComponent<BoxCollider2D>().size = new Vector2(.1f, .2f);
        Physics2D.SyncTransforms();
        float moved = (float)brainType.GetMethod("MoveWithObstacleSweep", Private)
            .Invoke(brain, new object[] { new Vector2(.8f, 0f) });
        yield return new WaitForFixedUpdate();
        Assert.That(moved, Is.EqualTo(.8f).Within(.001f));
        Assert.That(body.position.x, Is.EqualTo(origin.x + .8f).Within(.001f));
    }

    [Test]
    public void Thai_LinkReservationIsExclusiveAndReleasesOnDisable()
    {
        var brainType = FindType("ZombieAI");
        var link = Make("Reservation link", new Vector2(1000f, 1000f)).AddComponent(FindType("Pathfinding.NodeLink2"));
        var first = (Behaviour)Make("First waiter", new Vector2(1000f, 1000f)).AddComponent(brainType);
        var second = (Behaviour)Make("Second waiter", new Vector2(1000f, 1000f)).AddComponent(brainType);
        var reserve = brainType.GetMethod("TryReserveNavigationLink", Private);
        Assert.That((bool)reserve.Invoke(first, new object[] { link }), Is.True);
        Assert.That((bool)reserve.Invoke(second, new object[] { link }), Is.False);
        first.enabled = false;
        Assert.That((bool)reserve.Invoke(second, new object[] { link }), Is.True);
        second.enabled = false;
    }

    [Test]
    public void Thai_CloseDirectFallbackTracksNewDestination()
    {
        var brainType = FindType("ZombieAI");
        var brain = (Behaviour)Make("Close direct fallback", new Vector2(1000f, 1000f)).AddComponent(brainType);
        brain.enabled = false;
        var destination = new Vector2(1000.1f, 1000.1f);
        var shouldRequest = brainType.GetMethod("ShouldRequestPath", Private);
        Assert.That((bool)shouldRequest.Invoke(brain, new object[] { destination, .5f }), Is.False);
        Assert.That((Vector2)brainType.GetField("requestedPathTarget", Private).GetValue(brain), Is.EqualTo(destination));
        Assert.That((bool)shouldRequest.Invoke(brain, new object[] { destination + Vector2.one * 2f, .5f }), Is.True);
    }

    [UnityTest]
    public IEnumerator Thai_GrazingWallDoesNotPenetrate() => GrazeWall(true);

    [UnityTest]
    public IEnumerator Khoa_GrazingWallDoesNotPenetrate() => GrazeWall(false);

    IEnumerator GrazeWall(bool thai)
    {
        var origin = new Vector2(1000f, 1000f);
        var actor = Make("Grazing capsule", origin);
        var body = actor.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        var foot = actor.AddComponent<CapsuleCollider2D>();
        foot.size = thai ? new Vector2(.092514716f, .19389847f) : new Vector2(.13788795f, .15211296f);
        foot.offset = thai ? new Vector2(.02890889f, -.18735819f) : new Vector2(-.022027016f, -.17303038f);
        var brainType = FindType(thai ? "ZombieAI" : "ZombieAIKhoaRebuilt");
        var brain = (Behaviour)actor.AddComponent(brainType);
        brain.enabled = false;
        var filter = new ContactFilter2D { useLayerMask = true, useTriggers = false };
        filter.SetLayerMask(1 << LayerMask.NameToLayer("Obstacle"));
        Set(brain, thai ? "movementObstacleFilter" : "obstacleMovementFilter", filter);
        var wallObject = Make("Shallow grazing edge", origin + new Vector2(.4f, 0));
        wallObject.layer = LayerMask.NameToLayer("Obstacle");
        var wall = wallObject.AddComponent<BoxCollider2D>();
        wall.size = new Vector2(.4f, .2f);
        // The capsule's bottom crosses the top edge by 0.005 m: an invalid
        // straight segment even when a shape cast's contact tolerance misses it.
        body.position = origin + new Vector2(0, .1f + foot.size.y * .5f - foot.offset.y - .005f);
        Physics2D.SyncTransforms();
        Assert.That(Physics2D.Distance(foot, wall).isOverlapped, Is.False);
        var motor = brainType.GetMethod("MoveWithObstacleSweep", Private);
        for (int tick = 0; tick < 80; tick++)
        {
            motor.Invoke(brain, new object[] { Vector2.right * .015f });
            yield return new WaitForFixedUpdate();
            Assert.That(Physics2D.Distance(foot, wall).isOverlapped, Is.False,
                $"Grazing movement penetrated at tick {tick}: {Physics2D.Distance(foot, wall).distance}");
        }
        Assert.That(body.position.x, Is.LessThan(origin.x + .3f), "Motor must stop before the grazing edge");
    }

    static ContactFilter2D ObstacleFilter()
    {
        var filter = new ContactFilter2D { useLayerMask = true, useTriggers = false };
        filter.SetLayerMask(1 << LayerMask.NameToLayer("Obstacle"));
        return filter;
    }

    IEnumerator FollowClearCorner(bool thai, bool cornerBlocked = true, bool replaceRoute = false,
        bool simplifiedRoute = false, bool subdividedRoute = false)
    {
        // FakePath uses the normal path pool, whose Reset needs AstarPath.active.
        // Configure before Awake: no graph scan or worker thread for this fixture.
        var astarType = FindType("AstarPath");
        Assert.That((UnityEngine.Object)astarType.GetField("active").GetValue(null), Is.Null,
            "The isolated fixture must not replace an existing AstarPath");
        var astarObject = Make("Fixture AstarPath", Vector2.zero);
        astarObject.SetActive(false);
        var astar = astarObject.AddComponent(astarType);
        astarType.GetField("scanOnStartup").SetValue(astar, false);
        var threadCount = astarType.GetField("threadCount");
        threadCount.SetValue(astar, Enum.Parse(threadCount.FieldType, "None"));
        astarObject.SetActive(true);

        var origin = new Vector2(1000f, 1000f);
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");
        Assert.That(obstacleLayer, Is.GreaterThanOrEqualTo(0));
        var filter = new ContactFilter2D { useLayerMask = true, useTriggers = false };
        filter.SetLayerMask(1 << obstacleLayer);

        var actor = Make(thai ? "Thai follower" : "Khoa follower", origin);
        var body = actor.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        var foot = actor.AddComponent<CapsuleCollider2D>();
        foot.size = thai ? new Vector2(.092514716f, .19389847f) : new Vector2(.13788795f, .15211296f);
        foot.offset = thai ? new Vector2(.02890889f, -.18735819f) : new Vector2(-.022027016f, -.17303038f);
        var brainType = FindType(thai ? "ZombieAI" : "ZombieAIKhoaRebuilt");
        var brain = (Behaviour)actor.AddComponent(brainType);
        brain.enabled = false;
        Set(brain, thai ? "movementObstacleFilter" : "obstacleMovementFilter", filter);
        if (thai) brainType.GetField("nextWaypointDistance").SetValue(brain, .5f);
        else Set(brain, "nextWaypointDistance", 1f);

        var wallObject = Make("Corner wall below the clear route", origin +
            (cornerBlocked ? new Vector2(.5f, -.05f) : new Vector2(5f, -5f)));
        wallObject.layer = obstacleLayer;
        var wall = wallObject.AddComponent<BoxCollider2D>();
        wall.size = new Vector2(.5f, .3f);
        var route = new List<Vector3> { origin, origin + new Vector2(0f, .4f),
            origin + new Vector2(1f, .4f), origin + new Vector2(1f, 1f) };

        // Prove the authored route is physically traversable for this footprint.
        // Setup-only positioning is not used to recover the moving actor below.
        var hits = new RaycastHit2D[8];
        float skin = thai ? .04f : .02f;
        for (int i = 0; i < route.Count - 1; i++)
        {
            body.position = route[i];
            Physics2D.SyncTransforms();
            Vector2 segment = route[i + 1] - route[i];
            int count = foot.Cast(segment.normalized, filter, hits, segment.magnitude + skin);
            Assert.That(count, Is.Zero, "Fixture route must clear the real foot, segment " + i);
        }
        body.position = origin;
        Physics2D.SyncTransforms();
        object rawNodes = null;
        if (simplifiedRoute)
        {
            var nodeList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(FindType("Pathfinding.GraphNode")));
            // Register a real graph and create its nodes through the supported
            // work-item lifecycle. AstarPath owns/destroys them during teardown.
            Action createNodes = () =>
            {
                var data = astarType.GetField("data").GetValue(astar);
                var graph = data.GetType().GetMethod("AddGraph", new[] { typeof(Type) })
                    .Invoke(data, new object[] { FindType("Pathfinding.PointGraph") });
                var int3 = FindType("Pathfinding.Int3");
                var addNode = graph.GetType().GetMethod("AddNode", new[] { int3 });
                foreach (var point in route)
                    nodeList.Add(addNode.Invoke(graph, new[] { Activator.CreateInstance(int3, new object[] { point }) }));
            };
            astarType.GetMethod("AddWorkItem", new[] { typeof(Action) }).Invoke(astar, new object[] { createNodes });
            astarType.GetMethod("FlushWorkItems", Type.EmptyTypes).Invoke(astar, null);
            rawNodes = nodeList;
        }
        var vectorRoute = simplifiedRoute ? new List<Vector3> { route[0], route[route.Count - 1] } : route;
        if (subdividedRoute)
        {
            vectorRoute = new List<Vector3>();
            for (int i = 0; i <= 5; i++) vectorRoute.Add(Vector3.Lerp(route[0], route[route.Count - 1], i / 5f));
        }
        var path = FindType("Pathfinding.ABPath").GetMethod("FakePath")
            .Invoke(null, new object[] { vectorRoute, rawNodes });
        Set(brain, "path", path);
        string indexName = thai ? "currentWaypoint" : "waypointIndex";
        Set(brain, indexName, 1);
        var advance = brainType.GetMethod("AdvancePathWaypoint", Private);
        var motor = brainType.GetMethod("MoveWithObstacleSweep", Private);
        Assert.That(advance, Is.Not.Null);
        Assert.That(motor, Is.Not.Null);
        Vector2 goal = route[route.Count - 1];
        float speed = thai ? 2.5f : 1.5f;
        int ticks = 0;
        for (; ticks < 180 && Vector2.Distance(body.position, goal) > .05f; ticks++)
        {
            if (replaceRoute && ticks == 12)
            {
                // Simulate a newly accepted route after a moving goal. This
                // covers following/recovery, not Seeker callbacks or perception.
                goal = origin + new Vector2(-.5f, 1.1f);
                route = new List<Vector3> { body.position, origin + new Vector2(0f, .8f), goal };
                path = FindType("Pathfinding.ABPath").GetMethod("FakePath")
                    .Invoke(null, new object[] { route, null });
                Set(brain, "path", path);
                Set(brain, indexName, 1);
            }
            advance.Invoke(brain, null);
            int index = (int)brainType.GetField(indexName, Private).GetValue(brain);
            // Equivalent to the final swept approach; never teleport to the goal.
            var activeRoute = (List<Vector3>)path.GetType().GetField("vectorPath").GetValue(path);
            Vector2 target = activeRoute[Mathf.Min(index, activeRoute.Count - 1)];
            Vector2 remaining = target - body.position;
            motor.Invoke(brain, new object[] {
                remaining.normalized * Mathf.Min(speed * Time.fixedDeltaTime, remaining.magnitude) });
            yield return new WaitForFixedUpdate();
            Assert.That(Physics2D.Distance(foot, wall).isOverlapped, Is.False,
                $"Follower must never pass through the wall: tick={ticks}, position={body.position - origin}");
        }
        Debug.Log($"Corner follower {(thai ? "Thai" : "Khoa")}: ticks={ticks}, " +
            $"position={body.position - origin}, remaining={Vector2.Distance(body.position, goal):F4}");
        Assert.That(Vector2.Distance(body.position, goal), Is.LessThanOrEqualTo(.05f),
            "A physically valid corner route must make progress all the way to the goal");
    }
}
