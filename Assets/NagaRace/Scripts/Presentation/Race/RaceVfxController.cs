using UnityEngine;

namespace NagaRace.Presentation.Race
{
    /// <summary>
    /// 水しぶきの仮演出（09 M2 Splash placeholder）。イベント位置に白い粒をバースト放出する。
    /// 正式な水表現（06 §13）は M9。
    /// </summary>
    public sealed class RaceVfxController : MonoBehaviour
    {
        private ParticleSystem _splash;

        public void Build(Material particleMaterial)
        {
            var go = new GameObject("Splash");
            go.transform.SetParent(transform, false);
            _splash = go.AddComponent<ParticleSystem>();
            _splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _splash.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startColor = new Color(0.95f, 0.98f, 1f, 0.9f);
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 600;

            var emission = _splash.emission;
            emission.enabled = false;

            var shape = _splash.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.6f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
        }

        public void Splash(Vector3 position, int count)
        {
            if (_splash == null) return;
            var emit = new ParticleSystem.EmitParams
            {
                position = position,
                applyShapeToPosition = true,
            };
            _splash.Emit(emit, count);
        }
    }
}
