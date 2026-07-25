import Testing
import CoreGraphics
import Foundation
import OrbitKit
import simd
@testable import Gravitile

@Suite @MainActor struct CameraRigTests {
    private let size = CGSize(width: 390, height: 844)

    @Test func theCameraAlwaysLooksAtTheWorld() {
        for yaw in stride(from: 0.0, to: 6.0, by: 0.7) {
            for pitch in [-1.3, -0.4, 0.0, 0.6, 1.3] {
                let rig = CameraRig(yaw: yaw, pitch: pitch, distance: 3.4)
                #expect(abs(length(rig.position) - 3.4) < 1e-9)
                let (right, up, forward) = rig.basis
                #expect(abs(dot(forward, normalize(-rig.position)) - 1) < 1e-9)
                #expect(abs(dot(right, up)) < 1e-9)
                #expect(abs(dot(right, forward)) < 1e-9)
                #expect(abs(length(cross(right, up)) - 1) < 1e-9)
            }
        }
    }

    /// A tap in the middle of the screen must hit the middle of the world, or
    /// every placement in the game lands on the wrong tile.
    @Test func aTapInTheCenterPointsAtTheOrigin() {
        let rig = CameraRig()
        let ray = rig.ray(at: CGPoint(x: size.width / 2, y: size.height / 2), in: size)
        let hit = raySphereHit(origin: ray.origin, direction: ray.direction, radius: 1)
        #expect(hit != nil)
        // The center of the sphere as seen from the camera.
        #expect(abs(dot(normalize(hit!), normalize(rig.position)) - 1) < 1e-6)
    }

    @Test func projectionInvertsPicking() {
        let rig = CameraRig(yaw: 1.1, pitch: -0.3, distance: 3.0)
        let geometry = PlanetGeometry.generate(frequency: 4)
        var checked = 0
        for tile in geometry.tiles {
            // Only tiles on the near side project back sensibly.
            guard dot(tile.center, normalize(rig.position)) > 0.6 else { continue }
            guard let point = rig.project(tile.center, in: size) else { continue }
            let ray = rig.ray(at: point, in: size)
            guard let hit = raySphereHit(origin: ray.origin, direction: ray.direction, radius: 1)
            else { continue }
            #expect(geometry.nearestTile(to: hit) == tile.index)
            checked += 1
        }
        #expect(checked > 10, "the test should have exercised a real number of tiles")
    }

    @Test func pointsBehindTheCameraDoNotProject() {
        let rig = CameraRig(yaw: 0, pitch: 0, distance: 3)
        #expect(rig.project(rig.position * 2, in: size) == nil)
    }

    @Test func orbitingIsClampedAwayFromThePoles() {
        var rig = CameraRig()
        for _ in 0..<50 {
            rig.orbit(byX: 0, y: 400, viewHeight: 844)
        }
        #expect(rig.pitch <= CameraRig.pitchLimit + 1e-9)
        for _ in 0..<100 {
            rig.orbit(byX: 0, y: -400, viewHeight: 844)
        }
        #expect(rig.pitch >= -CameraRig.pitchLimit - 1e-9)
    }

    @Test func zoomStaysWithinRange() {
        var rig = CameraRig()
        for _ in 0..<30 { rig.zoom(by: 1.4) }
        #expect(rig.distance == CameraRig.minDistance)
        for _ in 0..<60 { rig.zoom(by: 0.7) }
        #expect(rig.distance == CameraRig.maxDistance)
    }
}
