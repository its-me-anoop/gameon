import simd

/// The mechanic the whole game rests on.
///
/// A freely rotating body that dissipates energy internally ends up spinning
/// about its axis of *maximum* moment of inertia. For point masses on a sphere
/// the inertia tensor is `r²(Σm)E − r²C`, where `C = Σ mᵢ pᵢpᵢᵀ` — so the
/// maximum-inertia axis is the eigenvector of `C` with the *smallest*
/// eigenvalue, i.e. the direction along which the player's mass is least
/// spread out.
///
/// In play that reduces to one sentence: **mass drifts to the equator,
/// emptiness drifts to the poles.**
public enum AxisDynamics {
    /// Mass-distribution covariance of the built world.
    public static func covariance(
        masses: some Sequence<(position: Vec3, mass: Double)>
    ) -> Symmetric3 {
        var c = Symmetric3.zero
        for entry in masses where entry.mass > 0 {
            c.addOuterProduct(of: normalize(entry.position), weight: entry.mass)
        }
        return c
    }

    /// The axis this mass distribution wants to spin about, signed into the
    /// same hemisphere as `currentAxis` so the planet never flips end-over-end.
    ///
    /// Degeneracy is the interesting case and it is common: a single pile of
    /// mass leaves *every* perpendicular direction equally preferred. When the
    /// smallest eigenvalue is a repeated one, the world takes the shortest path
    /// — the direction inside that eigenspace closest to where it already
    /// spins. `nil` means there is genuinely nothing to steer toward: an empty
    /// world, or a mass sitting exactly on the pole, which is a pencil balanced
    /// on its tip.
    public static func targetAxis(covariance c: Symmetric3, currentAxis: Vec3) -> Vec3? {
        let scale = c.trace
        guard scale > 1e-9 else { return nil }

        let (values, vectors) = c.eigen()
        let tolerance = 1e-6 * scale
        let firstIsRepeated = (values[1] - values[0]) <= tolerance
        let secondIsRepeated = (values[2] - values[1]) <= tolerance

        if firstIsRepeated && secondIsRepeated { return nil }

        if firstIsRepeated {
            let projected = currentAxis - vectors[2] * dot(currentAxis, vectors[2])
            guard length(projected) > 1e-4 else { return nil }
            return normalize(projected)
        }

        let candidate = vectors[0]
        return dot(candidate, currentAxis) >= 0 ? candidate : -candidate
    }

    /// Exponential approach toward the target axis. `rate` is the reciprocal of
    /// the settling time constant, so a rate of `1/90` settles most of the way
    /// in about a minute and a half — slow enough that steering is a deliberate
    /// act the player watches happen.
    public static func settle(
        axis: Vec3, toward target: Vec3, rate: Double, dt: Double
    ) -> Vec3 {
        guard dt > 0, rate > 0 else { return axis }
        let blend = 1 - exp(-rate * dt)
        return normalize(slerp(axis, target, min(max(blend, 0), 1)))
    }

    /// How far the world is from where it wants to spin, in degrees. This is
    /// the Wobble gauge: high while you are steering, relaxing as it settles,
    /// and the source of quakes past 25°.
    public static func wobbleDegrees(axis: Vec3, target: Vec3?) -> Double {
        guard let target else { return 0 }
        return angleBetween(axis, target) * 180 / .pi
    }
}
