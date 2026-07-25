import Testing
import Foundation
import simd
@testable import OrbitKit

@Suite struct AxisDynamicsTests {
    @Test func eigenDecomposesADiagonalMatrix() {
        let matrix = Symmetric3(xx: 3, xy: 0, xz: 0, yy: 1, yz: 0, zz: 2)
        let (values, vectors) = matrix.eigen()
        #expect(abs(values[0] - 1) < 1e-12)
        #expect(abs(values[1] - 2) < 1e-12)
        #expect(abs(values[2] - 3) < 1e-12)
        #expect(abs(abs(dot(vectors[0], Vec3(0, 1, 0))) - 1) < 1e-12)
        #expect(abs(abs(dot(vectors[2], Vec3(1, 0, 0))) - 1) < 1e-12)
    }

    @Test func eigenvectorsSatisfyTheEigenEquation() {
        let matrix = Symmetric3(xx: 4, xy: 1, xz: 0.5, yy: 2, yz: -0.3, zz: 1.5)
        let (values, vectors) = matrix.eigen()
        for (value, vector) in zip([values[0], values[1], values[2]], vectors) {
            let residual = matrix.multiplied(by: vector) - vector * value
            #expect(length(residual) < 1e-9)
        }
    }

    @Test func anEmptyWorldHasNoPreferredAxis() {
        let covariance = AxisDynamics.covariance(masses: [(position: Vec3(0, 1, 0), mass: 0)])
        #expect(AxisDynamics.targetAxis(covariance: covariance, currentAxis: Vec3(0, 1, 0)) == nil)
    }

    /// The mechanic in one assertion: a single pile of mass wants to sit on the
    /// equator, so the spin axis it produces is perpendicular to the pile.
    @Test func aSinglePilePushesTheAxisPerpendicularToIt() {
        let pile = normalize(Vec3(0, 0, 1))
        let covariance = AxisDynamics.covariance(masses: [(position: pile, mass: 12)])
        let target = AxisDynamics.targetAxis(covariance: covariance, currentAxis: Vec3(0, 1, 0))
        #expect(target != nil)
        #expect(abs(dot(target!, pile)) < 1e-6)
    }

    @Test func twoOpposingPilesBalanceOntoTheSameAxisAsOne() {
        let pile = normalize(Vec3(1, 0, 0))
        let covariance = AxisDynamics.covariance(masses: [
            (position: pile, mass: 6), (position: -pile, mass: 6),
        ])
        let target = AxisDynamics.targetAxis(covariance: covariance, currentAxis: Vec3(0, 1, 0))
        #expect(target != nil)
        #expect(abs(dot(target!, pile)) < 1e-6)
    }

    @Test func aBalancedRingHoldsTheCurrentAxis() {
        // Mass spread evenly around the equator already spins the way it wants
        // to: the axis through the ring is the unique minimum.
        var masses: [(position: Vec3, mass: Double)] = []
        for step in 0..<12 {
            let angle = Double(step) / 12 * 2 * .pi
            masses.append((Vec3(cos(angle), 0, sin(angle)), 3))
        }
        let covariance = AxisDynamics.covariance(masses: masses)
        let target = AxisDynamics.targetAxis(covariance: covariance, currentAxis: Vec3(0, 1, 0))
        #expect(target != nil)
        #expect(AxisDynamics.wobbleDegrees(axis: Vec3(0, 1, 0), target: target) < 1e-6)
    }

    @Test func aMassExactlyOnThePoleIsAPencilOnItsTip() {
        // Perfectly balanced and perfectly unstable: no direction to fall in,
        // so the world holds until something breaks the symmetry.
        let axis = normalize(Vec3(0, 1, 0))
        let covariance = AxisDynamics.covariance(masses: [(position: axis, mass: 20)])
        #expect(AxisDynamics.targetAxis(covariance: covariance, currentAxis: axis) == nil)
    }

    @Test func degenerateMinimumTakesTheShortestPath() {
        // One pile: every perpendicular axis is equally good, so the world
        // picks the perpendicular direction it is already closest to.
        let pile = normalize(Vec3(0, 0, 1))
        let covariance = AxisDynamics.covariance(masses: [(position: pile, mass: 9)])
        let tilted = normalize(Vec3(0.3, 1, 0.2))
        let target = AxisDynamics.targetAxis(covariance: covariance, currentAxis: tilted)!
        #expect(abs(dot(target, pile)) < 1e-9)
        #expect(angleBetween(target, tilted) < angleBetween(target, Vec3(1, 0, 0)) + 1e-9)
    }

    @Test func targetKeepsTheHemisphereOfTheCurrentAxis() {
        let pile = normalize(Vec3(0, 0, 1))
        let covariance = AxisDynamics.covariance(masses: [(position: pile, mass: 5)])
        let up = AxisDynamics.targetAxis(covariance: covariance, currentAxis: Vec3(0, 1, 0))!
        let down = AxisDynamics.targetAxis(covariance: covariance, currentAxis: Vec3(0, -1, 0))!
        #expect(dot(up, Vec3(0, 1, 0)) >= 0)
        #expect(dot(down, Vec3(0, -1, 0)) >= 0)
    }

    @Test func settlingApproachesTheTargetWithoutOvershooting() {
        let start = normalize(Vec3(0, 1, 0))
        let target = normalize(Vec3(1, 0, 0))
        var axis = start
        var previous = angleBetween(axis, target)
        for _ in 0..<50 {
            axis = AxisDynamics.settle(axis: axis, toward: target, rate: 1.0 / 90, dt: 5)
            let angle = angleBetween(axis, target)
            #expect(angle <= previous + 1e-12)
            previous = angle
        }
        #expect(previous < 0.15)
        #expect(abs(length(axis) - 1) < 1e-12)
    }

    @Test func wobbleIsZeroWithoutATarget() {
        #expect(AxisDynamics.wobbleDegrees(axis: Vec3(0, 1, 0), target: nil) == 0)
        let ninety = AxisDynamics.wobbleDegrees(axis: Vec3(0, 1, 0), target: Vec3(1, 0, 0))
        #expect(abs(ninety - 90) < 1e-9)
    }
}
