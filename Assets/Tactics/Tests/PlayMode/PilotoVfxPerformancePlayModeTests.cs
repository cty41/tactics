using System.Collections;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Tactics.AssetPipeline;
using Tactics.Common.Skills.Graph;
using Tactics.Common.Testing.Gameplay;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tactics.Tests.PlayMode
{
    public sealed class PilotoVfxPerformancePlayModeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameTimeService.ForceResume();
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal);
            Task<GameAssetManager> initializeTask = TestGameAssetHelper.EnsureInitialized();
            yield return WaitForTask(initializeTask, 10d, "Initialize GameAssetManager");
            Assert.That(initializeTask.IsFaulted, Is.False);
            Assert.That(initializeTask.Result, Is.Not.Null);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameTimeService.ForceResume();
            GameTimeService.SetPlaybackSpeed(GamePlaybackSpeed.Normal);
            ResetTransientVfxPoolForTests();
            TestGameAssetHelper.Cleanup();
            yield return null;
        }
        [Explicit("Profiler sampling harness; run by exact test name.")]
        [UnityTest]
        public System.Collections.IEnumerator PilotoLightning_EightConcurrentInstances_ProvidesProfilerWindow()
        {
            var profile = GameAssetManager.Instance.Load<VisualCueProfile>(
                "Assets/Tactics/Arts/PureRun/VFX/PilotoAdapted/Profiles/LightningLv1.asset");
            var instances = new GameObject[8];
            var cameraObject = new GameObject("PilotoVfxProfilerCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            var renderTarget = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32);
            renderTarget.Create();
            camera.targetTexture = renderTarget;
            try
            {
                RenderMetrics baseline;
                using (var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 256))
                using (var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 256))
                using (var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 256))
                using (var triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 256))
                using (var vertices = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count", 256))
                {
                    double baselineDeadline = Time.realtimeSinceStartupAsDouble + 1d;
                    while (Time.realtimeSinceStartupAsDouble < baselineDeadline)
                    {
                        camera.Render();
                        yield return null;
                    }
                    baseline = ReadRenderMetrics(drawCalls, batches, setPass, triangles, vertices);
                }

                for (int index = 0; index < instances.Length; index++)
                {
                    var position = new Vector3(index % 4 - 1.5f, index / 4 - 0.5f, 0f);
                    instances[index] = TransientVfxPool.Rent(
                        profile.Prefab,
                        position,
                        Quaternion.identity,
                        profile.Scale,
                        0,
                        profile.SortingOrderOffset);
                    for (int previous = 0; previous < index; previous++)
                        Assert.That(instances[index], Is.Not.SameAs(instances[previous]));
                }

                yield return null;
                GameTimeService.Pause();
                RenderMetrics lightning;
                using (var drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 512))
                using (var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 512))
                using (var setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 512))
                using (var triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 512))
                using (var vertices = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count", 512))
                {
                    double sampleDeadline = Time.realtimeSinceStartupAsDouble + 5d;
                    while (Time.realtimeSinceStartupAsDouble < sampleDeadline)
                    {
                        camera.Render();
                        yield return null;
                    }
                    lightning = ReadRenderMetrics(drawCalls, batches, setPass, triangles, vertices);
                }

                long drawCallDelta = System.Math.Max(0L, lightning.DrawCalls - baseline.DrawCalls);
                long batchDelta = System.Math.Max(0L, lightning.Batches - baseline.Batches);
                long setPassDelta = System.Math.Max(0L, lightning.SetPassCalls - baseline.SetPassCalls);
                string metrics =
                    $"baseline={baseline}{System.Environment.NewLine}" +
                    $"lightning={lightning}{System.Environment.NewLine}" +
                    $"drawCallDelta={drawCallDelta}, batchDelta={batchDelta}, setPassDelta={setPassDelta}";
                File.WriteAllText("Temp/PilotoVfxProfiler.metrics.txt", metrics);
                TestContext.Progress.WriteLine(metrics);
                Assert.That(lightning.SampleCount, Is.GreaterThan(0));
                Assert.That(batchDelta, Is.LessThanOrEqualTo(20), metrics);
            }
            finally
            {
                GameTimeService.ForceResume();
                foreach (GameObject instance in instances)
                    TransientVfxPool.Return(instance);
                camera.targetTexture = null;
                renderTarget.Release();
                Object.Destroy(renderTarget);
                Object.Destroy(cameraObject);
            }
        }

        [Explicit("Profiler sampling harness; run by exact test name.")]
        [UnityTest]
        public System.Collections.IEnumerator PilotoEmptyCamera_ProvidesProfilerBaselineWindow()
        {
            var cameraObject = new GameObject("PilotoVfxBaselineCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            var renderTarget = new RenderTexture(640, 360, 24, RenderTextureFormat.ARGB32);
            renderTarget.Create();
            camera.targetTexture = renderTarget;
            try
            {
                double sampleDeadline = Time.realtimeSinceStartupAsDouble + 5d;
                while (Time.realtimeSinceStartupAsDouble < sampleDeadline)
                {
                    camera.Render();
                    yield return null;
                }
            }
            finally
            {
                camera.targetTexture = null;
                renderTarget.Release();
                Object.Destroy(renderTarget);
                Object.Destroy(cameraObject);
            }
        }

        private static RenderMetrics ReadRenderMetrics(
            ProfilerRecorder drawCalls,
            ProfilerRecorder batches,
            ProfilerRecorder setPass,
            ProfilerRecorder triangles,
            ProfilerRecorder vertices)
        {
            return new RenderMetrics(
                MaxRecorderValue(drawCalls),
                MaxRecorderValue(batches),
                MaxRecorderValue(setPass),
                MaxRecorderValue(triangles),
                MaxRecorderValue(vertices),
                drawCalls.Count);
        }

        private static long MaxRecorderValue(ProfilerRecorder recorder)
        {
            return recorder.Count == 0 ? 0 : recorder.ToArray().Max(sample => sample.Value);
        }

        private readonly struct RenderMetrics
        {
            public RenderMetrics(
                long drawCalls,
                long batches,
                long setPassCalls,
                long triangles,
                long vertices,
                int sampleCount)
            {
                DrawCalls = drawCalls;
                Batches = batches;
                SetPassCalls = setPassCalls;
                Triangles = triangles;
                Vertices = vertices;
                SampleCount = sampleCount;
            }

            public long DrawCalls { get; }
            public long Batches { get; }
            public long SetPassCalls { get; }
            public long Triangles { get; }
            public long Vertices { get; }
            public int SampleCount { get; }

            public override string ToString()
            {
                return $"drawCalls={DrawCalls}, batches={Batches}, setPass={SetPassCalls}, " +
                    $"triangles={Triangles}, vertices={Vertices}, samples={SampleCount}";
            }
        }

        private static System.Collections.IEnumerator WaitForTask(
            Task task,
            double timeoutSeconds,
            string label)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            int frameCount = 0;
            while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline)
            {
                frameCount++;
                yield return null;
            }

            Assert.That(task.IsCompleted, Is.True,
                $"{label} timed out after {timeoutSeconds:F1}s and {frameCount} frames; status={task.Status}.");
        }

        private static void ResetTransientVfxPoolForTests()
        {
            var resetMethod = typeof(TransientVfxPool).GetMethod(
                "ResetStatics",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            if (resetMethod != null)
            {
                resetMethod.Invoke(null, null);
                return;
            }

            object cache = GetTransientVfxCache();
            cache?.GetType().GetMethod("Clear")?.Invoke(cache, null);
            Transform root = GetTransientVfxPoolRoot();
            if (root != null)
                Object.Destroy(root.gameObject);
            typeof(TransientVfxPool).GetField(
                    "_poolRoot",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(null, null);
        }

        private static object GetTransientVfxCache()
        {
            return typeof(TransientVfxPool).GetField(
                    "Available",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(null);
        }

        private static Transform GetTransientVfxPoolRoot()
        {
            return typeof(TransientVfxPool).GetField(
                    "_poolRoot",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                ?.GetValue(null) as Transform;
        }
    }
}
