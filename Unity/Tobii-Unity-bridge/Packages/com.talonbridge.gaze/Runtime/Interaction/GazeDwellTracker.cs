using System;
using System.Collections.Generic;
using UnityEngine;

namespace TalonBridge.Gaze
{
    /// <summary>
    /// Turns a per-frame set of gaze targets into enter, progress, exit and activation notifications.
    ///
    /// Dwell state is keyed by interactable rather than stored on the interactable itself, meaning:
    /// 1) Several pointers can track the same element 
    /// 2) A set of simultaneous targets is handled the same way as a single one 
    ///
    /// Progress only accumulates while gaze is valid. 
    /// Owned by <see cref="GazePointer"/> and driven from its Update.
    /// </summary>
    public sealed class GazeDwellTracker
    {
        private struct DwellState
        {
            public float Elapsed;
            public bool HasActivated;
            public float CooldownUntil;
        }

        private readonly Dictionary<GazeInteractable, DwellState> states =
            new Dictionary<GazeInteractable, DwellState>();

        private readonly List<GazeInteractable> current = new List<GazeInteractable>();
        private readonly List<GazeInteractable> scratch = new List<GazeInteractable>();

        /// <summary>Raised when a target completes its dwell.</summary>
        public event Action<GazeInteractable> Activated;

        /// <summary>Seconds of dwell needed to activate, unless the target overrides it.</summary>
        public float DwellDuration { get; set; } = 0.6f;

        /// <summary>Seconds after an activation before the same target may fire again.</summary>
        public float Cooldown { get; set; } = 0.5f;

        /// <summary>
        /// Fraction of dwell progress shed per second while a target is not gazed at.
        /// 0 discards progress the moment gaze leaves.
        /// A small value forgives gaze jitter across a target's edge.
        /// </summary>
        public float DecayRate { get; set; } = 0f;

        /// <summary>Targets gazed at as of the last <see cref="Tick"/>.</summary>
        public IReadOnlyList<GazeInteractable> CurrentTargets => current;

        /// <summary>Progress of the first current target, 0 to 1.</summary>
        public float PrimaryProgress =>
            current.Count > 0 && states.TryGetValue(current[0], out DwellState state)
                ? Mathf.Clamp01(state.Elapsed / DurationFor(current[0]))
                : 0f;

        /// <summary>
        /// Advances dwell for this frame.
        /// </summary>
        /// <param name="targets">
        /// Targets under the gaze, nearest first. Pass an empty list when gaze is invalid or hit nothing.
        /// </param>
        /// <param name="gazeValid">
        /// False when gaze is unusable. Every target is exited and no progress accumulates.
        /// </param>
        /// <param name="deltaTime">Seconds since the last tick.</param>
        public void Tick(IReadOnlyList<GazeInteractable> targets, bool gazeValid, float deltaTime)
        {
            PruneDeadTargets();

            if (!gazeValid || targets == null)
            {
                ExitAll();
                Decay(deltaTime);
                return;
            }

            // Exit anything no longer targeted 
            for (int i = current.Count - 1; i >= 0; i--)
            {
                GazeInteractable existing = current[i];
                if (Contains(targets, existing))
                {
                    continue;
                }

                current.RemoveAt(i);
                existing.NotifyExit();
            }

            for (int i = 0; i < targets.Count; i++)
            {
                GazeInteractable target = targets[i];
                if (target == null || !target.CanInteract)
                {
                    continue;
                }

                if (!current.Contains(target))
                {
                    current.Add(target);
                    if (!states.ContainsKey(target))
                    {
                        states[target] = default;
                    }

                    target.NotifyEnter();
                }

                Advance(target, deltaTime);
            }

            Decay(deltaTime);
        }

        /// <summary>Exits every target and clears all dwell state.</summary>
        public void Reset()
        {
            ExitAll();
            states.Clear();
        }

        private void Advance(GazeInteractable target, float deltaTime)
        {
            DwellState state = states.TryGetValue(target, out DwellState existing)
                ? existing
                : default;

            float duration = DurationFor(target);
            state.Elapsed += deltaTime;

            float progress = Mathf.Clamp01(state.Elapsed / duration);

            if (state.Elapsed >= duration
                && !state.HasActivated
                && Time.unscaledTime >= state.CooldownUntil)
            {
                state.HasActivated = true;
                state.CooldownUntil = Time.unscaledTime + CooldownFor(target);
                states[target] = state;

                target.NotifyProgress(1f);
                target.NotifyActivated();
                Activated?.Invoke(target);

                if (target.RepeatWhileHeld)
                {
                    // Restart the dwell; the cooldown gates the next activation.
                    state.Elapsed = 0f;
                    state.HasActivated = false;
                    states[target] = state;
                }

                return;
            }

            states[target] = state;
            target.NotifyProgress(progress);
        }

        /// <summary>
        /// Sheds progress from targets that are no longer gazed at (avoid completely restarting dwell due to a brief gaze wander) 
        /// </summary>
        private void Decay(float deltaTime)
        {
            if (states.Count == 0)
            {
                return;
            }

            // Snapshot the keys before touching the dictionary 
            scratch.Clear();
            foreach (KeyValuePair<GazeInteractable, DwellState> entry in states)
            {
                if (entry.Key != null && !current.Contains(entry.Key))
                {
                    scratch.Add(entry.Key);
                }
            }

            for (int i = 0; i < scratch.Count; i++)
            {
                GazeInteractable target = scratch[i];
                if (!states.TryGetValue(target, out DwellState state))
                {
                    continue;
                }

                // An entry still cooling down is kept even at zero progress, so a target cannot be re-fired by glancing away and back.
                bool cooling = Time.unscaledTime < state.CooldownUntil;

                state.Elapsed = DecayRate > 0f
                    ? state.Elapsed - (DecayRate * DurationFor(target) * deltaTime)
                    : 0f;

                if (state.Elapsed <= 0f && !cooling)
                {
                    states.Remove(target);
                    continue;
                }

                state.Elapsed = Mathf.Max(0f, state.Elapsed);
                state.HasActivated = false;
                states[target] = state;
            }
        }

        private void ExitAll()
        {
            for (int i = 0; i < current.Count; i++)
            {
                GazeInteractable target = current[i];
                if (target != null)
                {
                    target.NotifyExit();
                }
            }

            current.Clear();
        }

        /// <summary>
        /// Drops destroyed or disabled targets
        /// </summary>
        private void PruneDeadTargets()
        {
            for (int i = current.Count - 1; i >= 0; i--)
            {
                GazeInteractable target = current[i];
                if (target == null || !target.CanInteract)
                {
                    current.RemoveAt(i);
                }
            }

            scratch.Clear();
            foreach (KeyValuePair<GazeInteractable, DwellState> entry in states)
            {
                if (entry.Key == null)
                {
                    scratch.Add(entry.Key);
                }
            }

            for (int i = 0; i < scratch.Count; i++)
            {
                states.Remove(scratch[i]);
            }
        }

        private float DurationFor(GazeInteractable target)
        {
            float duration = target != null && target.DwellDurationOverride >= 0f
                ? target.DwellDurationOverride
                : DwellDuration;
            return Mathf.Max(0.01f, duration);
        }

        private float CooldownFor(GazeInteractable target)
        {
            float cooldown = target != null && target.CooldownOverride >= 0f
                ? target.CooldownOverride
                : Cooldown;
            return Mathf.Max(0f, cooldown);
        }

        private static bool Contains(IReadOnlyList<GazeInteractable> list, GazeInteractable target)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == target)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
