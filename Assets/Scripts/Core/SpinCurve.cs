using UnityEngine;

namespace SlotGame.Core
{
    /// <summary>
    /// Distance curve for a reel spin, as a function of normalised time.
    ///
    /// A real reel cannot start at full speed and stop instantly, so the curve is a
    /// constant-speed cruise followed by a cubic ease-out tail. The two sections are
    /// joined so that both the value *and* the velocity are continuous (C1), which is
    /// what stops the deceleration from visibly "kicking" as the reel lands.
    ///
    /// Returned values are fractions of the total distance to travel, so
    /// <see cref="Evaluate"/> always starts at 0 and ends at exactly 1.
    /// </summary>
    public static class SpinCurve
    {
        /// <summary>Portion of the spin duration spent decelerating (0-1).</summary>
        public const float DecelerationFraction = 0.45f;

        public static float Evaluate(float normalizedTime)
        {
            float t = Mathf.Clamp01(normalizedTime);
            const float d = DecelerationFraction;

            // With a constant cruise speed v for (1-d) of the time and a v*(1-u)^2
            // tail for the remaining d, integrating the velocity gives a total
            // distance of v * (1 - 2d/3). Scaling by that keeps the travel equal to 1.
            float cruiseSpeed = 1f / (1f - 2f * d / 3f);

            if (t <= 1f - d)
            {
                return cruiseSpeed * t;
            }

            float u = (t - (1f - d)) / d;
            float tail = (1f - Mathf.Pow(1f - u, 3f)) / 3f;
            return cruiseSpeed * (1f - d) + cruiseSpeed * d * tail;
        }
    }
}
