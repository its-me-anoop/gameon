import Testing
@testable import Gravitile

@Suite struct JuiceLogicTests {
    @Test func milestoneFiresOncePerValue() {
        var tracker = MilestoneTracker()
        #expect(tracker.newlyReached(bestTile: 128) == nil)
        #expect(tracker.newlyReached(bestTile: 256) == 256)
        #expect(tracker.newlyReached(bestTile: 256) == nil)
        #expect(tracker.newlyReached(bestTile: 512) == 512)
    }

    @Test func resumingAGameDoesNotReCelebrate() {
        var tracker = MilestoneTracker(alreadyReached: 512)
        #expect(tracker.newlyReached(bestTile: 512) == nil)
        #expect(tracker.newlyReached(bestTile: 1024) == 1024)
    }

    @Test func crossingSeveralAtOnceCelebratesOnlyTheBiggest() {
        var tracker = MilestoneTracker()
        #expect(tracker.newlyReached(bestTile: 1024) == 1024)
        // The skipped-over values are also spent.
        #expect(tracker.newlyReached(bestTile: 512) == nil)
        #expect(tracker.newlyReached(bestTile: 256) == nil)
    }

    @Test func shakeRestsCentredOnWholeNumbers() {
        let atRest = ShakeEffect(travel: 3, magnitude: 8)
        let transform = atRest.effectValue(size: .init(width: 100, height: 100))
        #expect(abs(transform.m31) < 0.0001)
    }
}
