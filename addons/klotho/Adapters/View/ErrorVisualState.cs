// Per-view desync (rollback) error-blending pipeline. Accumulates the engine-reported rollback delta,
// snaps on teleport/zero bounds, applies magnitude-proportional exponential decay, then exp-blend
// smooths the result for the view to consume.
using global::Godot;

namespace xpTURN.Klotho.Godot
{
    /// <summary>
    /// Struct that bundles the state and tuning parameters of the per-view error visual pipeline.
    /// Operates independently from the engine-side delta filters (ErrorCorrectionSettings.PosMinCorrection, RotMinCorrectionDeg).
    ///
    /// Pipeline performed in Tick:
    ///   1. Accumulate rollback delta
    ///   2. Reset immediately if Teleport-snap upper bound (PosTeleportDistance / RotTeleportDeg) is exceeded
    ///   3. Snap to zero if below the Zero-snap lower bound (PosZeroSnapThreshold / RotZeroSnapThresholdDeg)
    ///   4. Apply variable decay rate proportional to accumulated magnitude (pos/rot independent)
    ///   5. Exp-blend smoothing based on SmoothingRate
    /// </summary>
    public struct ErrorVisualState
    {
        // ── Decay rate (1, 4) ──

        /// <summary>Decay rate lower bound. Applied when the error is at or below PosBlendStart (RotBlendStartDeg for Rot).</summary>
        public float MinRate;

        /// <summary>Decay rate upper bound. Applied when the error is at or above PosBlendEnd (RotBlendEndDeg for Rot).</summary>
        public float MaxRate;

        // ── Position pipeline ──

        /// <summary>Position decay rate interpolation start (m). MinRate is used at or below this value.</summary>
        public float PosBlendStart;

        /// <summary>Position decay rate interpolation end (m). MaxRate is used at or above this value.</summary>
        public float PosBlendEnd;

        /// <summary>
        /// View-side zero-snap threshold (m). Snaps to zero when the accumulated error drops at or below this value during decay.
        /// Can be tuned independently from the engine filter value.
        /// </summary>
        public float PosZeroSnapThreshold;

        /// <summary>Position teleport-snap threshold (m). Resets immediately when the accumulated error reaches or exceeds this value.</summary>
        public float PosTeleportDistance;

        // ── Rotation pipeline ──

        /// <summary>Rotation decay rate interpolation start (deg). MinRate is applied at or below this value.</summary>
        public float RotBlendStartDeg;

        /// <summary>Rotation decay rate interpolation end (deg). MaxRate is applied at or above this value.</summary>
        public float RotBlendEndDeg;

        /// <summary>View-side rotation zero-snap threshold (deg). Operates independently of the engine filter value.</summary>
        public float RotZeroSnapThresholdDeg;

        /// <summary>Rotation teleport-snap threshold (deg).</summary>
        public float RotTeleportDeg;

        // ── Smoothing (5) ──

        /// <summary>View interpolation rate. blend = 1 - exp(-SmoothingRate * dt).</summary>
        public float SmoothingRate;

        // ── Runtime state ──

        private Vector3 _accumulatedPosError;
        private float   _accumulatedYawError;
        private Vector3 _smoothedPosError;
        private float   _smoothedYawError;

        /// <summary>Default values. Uses the same initial thresholds as the engine filter.</summary>
        public static ErrorVisualState Default => new()
        {
            MinRate                 = 3f,
            MaxRate                 = 10f,
            PosBlendStart           = 0.01f,
            PosBlendEnd             = 0.2f,
            PosZeroSnapThreshold    = 0.001f,
            PosTeleportDistance     = 1f,
            RotBlendStartDeg        = 0.573f,   // ≈ 0.01 rad
            RotBlendEndDeg          = 11.46f,   // ≈ 0.2 rad
            RotZeroSnapThresholdDeg = 0.05f,
            RotTeleportDeg          = 90f,
            SmoothingRate           = 200f,
        };

        /// <summary>Final view output — added directly to the interpolated position.</summary>
        public Vector3 SmoothedPosError => _smoothedPosError;

        /// <summary>Final view output — Y-axis radians. Used directly via Quaternion.FromEuler (radians).</summary>
        public float SmoothedYawError => _smoothedYawError;

        /// <summary>
        /// Per-frame refresh. Consumes the entity rollback delta and teleport intent.
        /// </summary>
        /// <param name="rollbackDelta">PuP frame - Predicted frame position difference (m).</param>
        /// <param name="rollbackYawDelta">Same as above, Y-axis radians.</param>
        /// <param name="deltaTime">Per-frame delta time (seconds).</param>
        /// <param name="teleported">Engine-confirmed teleport. Resets immediately when true.</param>
        public void Tick(Vector3 rollbackDelta, float rollbackYawDelta, float deltaTime, bool teleported)
        {
            // Engine-confirmed teleport — highest priority
            if (teleported)
            {
                Reset();
                return;
            }

            // Stage ① delta accumulation
            _accumulatedPosError += rollbackDelta;
            _accumulatedYawError += rollbackYawDelta;

            // Threshold A. Teleport-snap upper bound — excessive accumulation → reset immediately
            float posMag = _accumulatedPosError.Length();
            if (posMag >= PosTeleportDistance)
            {
                Reset();
                return;
            }
            float yawAbs = Mathf.Abs(_accumulatedYawError);
            if (yawAbs >= Mathf.DegToRad(RotTeleportDeg))
            {
                Reset();
                return;
            }

            // Zero-snap lower bound — snaps to zero when the accumulated error is tiny.
            if (posMag > 0f && posMag <= PosZeroSnapThreshold)
                _accumulatedPosError = Vector3.Zero;

            float yawZeroSnapRad = Mathf.DegToRad(RotZeroSnapThresholdDeg);
            if (yawAbs > 0f && yawAbs <= yawZeroSnapRad)
                _accumulatedYawError = 0f;

            // Variable-rate decay. pos/rot are handled independently.
            // A linear approximation like (1 - rate*dt) can flip sign and oscillate when rate*dt > 1, so exp is used.
            float posMagAfter = _accumulatedPosError.Length();
            if (posMagAfter > 0f)
            {
                float rate = ComputeDecayRatePos(posMagAfter);
                _accumulatedPosError *= Mathf.Exp(-rate * deltaTime);
            }

            float yawAbsAfter = Mathf.Abs(_accumulatedYawError);
            if (yawAbsAfter > 0f)
            {
                float rate = ComputeDecayRateRot(yawAbsAfter);
                _accumulatedYawError *= Mathf.Exp(-rate * deltaTime);
            }

            // Exp-blend smoothing. Interpolates the accumulated error to produce the smoothed value.
            float blend = 1f - Mathf.Exp(-SmoothingRate * deltaTime);
            _smoothedPosError = _smoothedPosError.Lerp(_accumulatedPosError, blend);
            _smoothedYawError = Mathf.Lerp(_smoothedYawError, _accumulatedYawError, blend);
        }

        /// <summary>Immediately initializes accumulation/interpolation state. Configuration fields are preserved.</summary>
        public void Reset()
        {
            _accumulatedPosError = Vector3.Zero;
            _accumulatedYawError = 0f;
            _smoothedPosError    = Vector3.Zero;
            _smoothedYawError    = 0f;
        }

        // ── Independent position/rotation decay rate calculation ──

        private readonly float ComputeDecayRatePos(float errorMag_m)
        {
            if (errorMag_m <= PosBlendStart) return MinRate;
            if (errorMag_m >= PosBlendEnd)   return MaxRate;
            float t = (errorMag_m - PosBlendStart) / (PosBlendEnd - PosBlendStart);
            return MinRate + t * (MaxRate - MinRate);
        }

        private readonly float ComputeDecayRateRot(float errorMag_rad)
        {
            float startRad = Mathf.DegToRad(RotBlendStartDeg);
            float endRad   = Mathf.DegToRad(RotBlendEndDeg);
            if (errorMag_rad <= startRad) return MinRate;
            if (errorMag_rad >= endRad)   return MaxRate;
            float t = (errorMag_rad - startRad) / (endRad - startRad);
            return MinRate + t * (MaxRate - MinRate);
        }
    }
}
