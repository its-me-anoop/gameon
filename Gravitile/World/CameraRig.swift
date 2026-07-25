import CoreGraphics
import Foundation
import OrbitKit
import simd

/// Orbit camera around a world at the origin.
///
/// The rig owns its own projection math rather than asking RealityKit to
/// unproject taps: the camera is entirely ours, the arithmetic is eight lines,
/// and being pure means tile picking can be tested without a running scene.
struct CameraRig: Equatable, Sendable {
    /// Radians around the world's vertical.
    var yaw: Double = 0.55
    /// Radians above the world's equator.
    var pitch: Double = 0.32
    var distance: Double = 4.55
    /// Vertical field of view.
    var fieldOfViewDegrees: Double = 45

    static let minDistance = 2.4
    static let maxDistance = 10.0
    /// Just under vertical: at the poles the up-vector degenerates and the
    /// view rolls unpleasantly.
    static let pitchLimit = 1.4

    var position: Vec3 {
        Vec3(
            cos(pitch) * sin(yaw),
            sin(pitch),
            cos(pitch) * cos(yaw)
        ) * distance
    }

    /// Right-handed camera basis looking at the origin, `forward` down -Z the
    /// way RealityKit expects.
    var basis: (right: Vec3, up: Vec3, forward: Vec3) {
        let forward = normalize(-position)
        let right = normalize(cross(forward, Vec3(0, 1, 0)))
        return (right, cross(right, forward), forward)
    }

    mutating func orbit(byX dx: Double, y dy: Double, viewHeight: Double) {
        // A full drag across the view is a bit more than half a turn, which
        // keeps the world feeling like an object in the hand.
        let scale = .pi / max(viewHeight, 1) * 1.2
        yaw -= dx * scale
        pitch = min(max(pitch + dy * scale, -Self.pitchLimit), Self.pitchLimit)
    }

    mutating func zoom(by factor: Double) {
        distance = min(max(distance / max(factor, 0.01), Self.minDistance), Self.maxDistance)
    }

    /// Where a world-space point lands on screen, or `nil` when it is behind
    /// the camera. The inverse of `ray(at:in:)`, and how the interface puts a
    /// tappable target over the meteor.
    func project(_ point: Vec3, in size: CGSize) -> CGPoint? {
        let (right, up, forward) = basis
        let relative = point - position
        let depth = dot(relative, forward)
        guard depth > 0.01 else { return nil }

        let width = max(Double(size.width), 1)
        let height = max(Double(size.height), 1)
        let tanHalf = tan(fieldOfViewDegrees * .pi / 360)
        let ndcX = (dot(relative, right) / depth) / (tanHalf * width / height)
        let ndcY = (dot(relative, up) / depth) / tanHalf
        return CGPoint(x: (ndcX + 1) / 2 * width, y: (1 - ndcY) / 2 * height)
    }

    /// World-space ray through a point in view coordinates (origin top-left).
    func ray(at point: CGPoint, in size: CGSize) -> (origin: Vec3, direction: Vec3) {
        let width = max(Double(size.width), 1)
        let height = max(Double(size.height), 1)
        let ndcX = 2 * Double(point.x) / width - 1
        let ndcY = 1 - 2 * Double(point.y) / height
        let tanHalf = tan(fieldOfViewDegrees * .pi / 360)
        let (right, up, forward) = basis
        let direction = normalize(
            forward + right * (ndcX * tanHalf * width / height) + up * (ndcY * tanHalf)
        )
        return (position, direction)
    }
}
