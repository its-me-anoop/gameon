import simd

/// Unit-sphere and rigid-body math for the world simulation. Everything here
/// is pure and allocation-light: the axis solver runs every tick, and the
/// geometry builder runs on every collapse.
public typealias Vec3 = SIMD3<Double>

public extension Vec3 {
    /// Two unit vectors perpendicular to `self`, forming a right-handed basis
    /// with it (`e1 × e2 == self`). Used to sort tile corners into winding
    /// order and to place props on the surface.
    func tangentBasis() -> (e1: Vec3, e2: Vec3) {
        // Pick the world axis least aligned with self, so the cross product
        // never degenerates.
        let helper: Vec3 = abs(x) < 0.9 ? Vec3(1, 0, 0) : Vec3(0, 1, 0)
        let e1 = normalize(cross(helper, self))
        let e2 = cross(self, e1)
        return (e1, e2)
    }
}

/// Great-circle angle between two unit vectors, numerically stable near 0 and π
/// where `acos(dot)` loses most of its precision.
public func angleBetween(_ a: Vec3, _ b: Vec3) -> Double {
    atan2(length(cross(a, b)), dot(a, b))
}

/// Spherical interpolation along the shorter arc. Falls back to linear blending
/// when the vectors are nearly parallel, which is the common case once the spin
/// axis has settled.
public func slerp(_ a: Vec3, _ b: Vec3, _ t: Double) -> Vec3 {
    let cosine = min(max(dot(a, b), -1), 1)
    let theta = acos(cosine)
    guard theta > 1e-6 else { return normalize(a + (b - a) * t) }
    let sine = sin(theta)
    return a * (sin((1 - t) * theta) / sine) + b * (sin(t * theta) / sine)
}

/// Rotate `v` about a unit `axis` by `angle` radians (Rodrigues).
public func rotate(_ v: Vec3, about axis: Vec3, by angle: Double) -> Vec3 {
    let c = cos(angle), s = sin(angle)
    return v * c + cross(axis, v) * s + axis * (dot(axis, v) * (1 - c))
}

// MARK: - Picking

/// Nearest intersection of a ray with the unit sphere scaled to `radius`,
/// centered at the origin. Returns `nil` when the ray misses — the renderer
/// uses this instead of physics colliders so tile picking stays testable
/// without a running scene.
public func raySphereHit(
    origin: Vec3, direction: Vec3, radius: Double
) -> Vec3? {
    let d = normalize(direction)
    let b = dot(origin, d)
    let c = dot(origin, origin) - radius * radius
    let discriminant = b * b - c
    guard discriminant >= 0 else { return nil }
    let root = discriminant.squareRoot()
    let near = -b - root
    let far = -b + root
    let t = near >= 0 ? near : far
    guard t >= 0 else { return nil }
    return origin + d * t
}
