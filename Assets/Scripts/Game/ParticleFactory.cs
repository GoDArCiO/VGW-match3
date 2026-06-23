using UnityEngine;

namespace Proto.Game
{
    /// <summary>
    /// One-shot particle bursts configured entirely from code — no prefab assets to wire (headless pillar).
    /// Each burst GameObject self-destructs after its lifetime. The two presets cover most casual beats:
    /// a small <see cref="Burst"/> for a pickup/impact and a bigger <see cref="Confetti"/> spray for a win.
    /// </summary>
    public sealed class ParticleFactory
    {
        private readonly MaterialFactory _materials;

        public ParticleFactory(MaterialFactory materials)
        {
            _materials = materials;
        }

        /// <summary>Small radial pop in a single colour (pickup, deposit, impact).</summary>
        public void Burst(Vector3 position, Color colour, int count)
        {
            Spawn(position, colour, count, speed: 2.5f, size: 0.12f, lifetime: 0.5f, gravity: 0.4f);
        }

        /// <summary>Bigger celebratory spray (win, milestone).</summary>
        public void Confetti(Vector3 position, Color colour, int count)
        {
            Spawn(position, colour, count, speed: 5f, size: 0.16f, lifetime: 1.2f, gravity: 1f);
        }

        private void Spawn(Vector3 position, Color colour, int count, float speed, float size, float lifetime, float gravity)
        {
            var go = new GameObject("Burst");
            go.transform.position = position;

            var system = go.AddComponent<ParticleSystem>();
            // A freshly added ParticleSystem starts in the playing state; duration (and other MainModule
            // fields) can only be set while stopped, so halt it before configuring.
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.startColor = colour;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size);
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.5f, lifetime);
            main.gravityModifier = gravity;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = lifetime;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _materials.Get(colour);

            system.Play();
            Object.Destroy(go, lifetime + 0.5f);
        }
    }
}
