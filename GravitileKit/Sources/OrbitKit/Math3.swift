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

// MARK: - Symmetric 3×3

/// A symmetric 3×3 matrix stored as its six distinct entries — enough for the
/// mass-distribution covariance, and small enough to keep the Jacobi solver
/// free of allocations.
public struct Symmetric3: Equatable, Sendable {
    public var xx: Double, xy: Double, xz: Double
    public var yy: Double, yz: Double
    public var zz: Double

    public static let zero = Symmetric3(xx: 0, xy: 0, xz: 0, yy: 0, yz: 0, zz: 0)

    public init(xx: Double, xy: Double, xz: Double, yy: Double, yz: Double, zz: Double) {
        self.xx = xx; self.xy = xy; self.xz = xz
        self.yy = yy; self.yz = yz; self.zz = zz
    }

    /// Accumulate `weight · (E − aaᵀ)` — mass spread evenly around the equator
    /// of `axis`, which is what a spinning body's own bulge is.
    public mutating func addOblateness(axis a: Vec3, weight: Double) {
        xx += weight * (1 - a.x * a.x)
        xy += weight * (-a.x * a.y)
        xz += weight * (-a.x * a.z)
        yy += weight * (1 - a.y * a.y)
        yz += weight * (-a.y * a.z)
        zz += weight * (1 - a.z * a.z)
    }

    /// Accumulate `weight · vv ᵀ` — one point mass's contribution to the
    /// distribution covariance.
    public mutating func addOuterProduct(of v: Vec3, weight: Double) {
        xx += weight * v.x * v.x
        xy += weight * v.x * v.y
        xz += weight * v.x * v.z
        yy += weight * v.y * v.y
        yz += weight * v.y * v.z
        zz += weight * v.z * v.z
    }

    public func multiplied(by v: Vec3) -> Vec3 {
        Vec3(
            xx * v.x + xy * v.y + xz * v.z,
            xy * v.x + yy * v.y + yz * v.z,
            xz * v.x + yz * v.y + zz * v.z
        )
    }

    public var trace: Double { xx + yy + zz }

    /// Eigenvalues and eigenvectors by cyclic Jacobi rotation, sorted ascending
    /// by eigenvalue. Symmetric 3×3 converges in a handful of sweeps, so this
    /// is cheap enough to run every simulation tick.
    public func eigen() -> (values: SIMD3<Double>, vectors: [Vec3]) {
        var a = [[xx, xy, xz], [xy, yy, yz], [xz, yz, zz]]
        var v: [[Double]] = [[1, 0, 0], [0, 1, 0], [0, 0, 1]]

        for _ in 0..<24 {
            let off = a[0][1] * a[0][1] + a[0][2] * a[0][2] + a[1][2] * a[1][2]
            if off < 1e-24 { break }
            for p in 0..<2 {
                for q in (p + 1)..<3 where abs(a[p][q]) > 1e-18 {
                    let theta = (a[q][q] - a[p][p]) / (2 * a[p][q])
                    let sign: Double = theta >= 0 ? 1 : -1
                    let t = sign / (abs(theta) + (theta * theta + 1).squareRoot())
                    let c = 1 / (t * t + 1).squareRoot()
                    let s = t * c

                    for k in 0..<3 {
                        let akp = a[k][p], akq = a[k][q]
                        a[k][p] = c * akp - s * akq
                        a[k][q] = s * akp + c * akq
                    }
                    for k in 0..<3 {
                        let apk = a[p][k], aqk = a[q][k]
                        a[p][k] = c * apk - s * aqk
                        a[q][k] = s * apk + c * aqk
                    }
                    for k in 0..<3 {
                        let vkp = v[k][p], vkq = v[k][q]
                        v[k][p] = c * vkp - s * vkq
                        v[k][q] = s * vkp + c * vkq
                    }
                }
            }
        }

        var pairs = (0..<3).map { i in
            (value: a[i][i], vector: normalize(Vec3(v[0][i], v[1][i], v[2][i])))
        }
        pairs.sort { $0.value < $1.value }
        return (
            SIMD3(pairs[0].value, pairs[1].value, pairs[2].value),
            pairs.map(\.vector)
        )
    }
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
