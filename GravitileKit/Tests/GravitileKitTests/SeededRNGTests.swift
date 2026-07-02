import Testing
import Foundation
@testable import GravitileKit

@Suite struct SeededRNGTests {
    @Test func matchesSplitMix64ReferenceVectors() {
        // Reference outputs for seed 0 from the canonical SplitMix64 implementation.
        var rng = SplitMix64(seed: 0)
        #expect(rng.next() == 0xE220_A839_7B1D_CDAF)
        #expect(rng.next() == 0x6E78_9E6A_A1B9_65F4)
        #expect(rng.next() == 0x06C4_5D18_8009_454F)
    }

    @Test func sameSeedProducesSameSequence() {
        var a = SplitMix64(seed: 42)
        var b = SplitMix64(seed: 42)
        for _ in 0..<100 {
            #expect(a.next() == b.next())
        }
    }

    @Test func differentSeedsDiverge() {
        var a = SplitMix64(seed: 1)
        var b = SplitMix64(seed: 2)
        #expect(a.next() != b.next())
    }

    @Test func codableRoundTripPreservesStreamPosition() throws {
        var rng = SplitMix64(seed: 7)
        _ = rng.next()
        _ = rng.next()
        let data = try JSONEncoder().encode(rng)
        var restored = try JSONDecoder().decode(SplitMix64.self, from: data)
        #expect(restored.next() == rng.next())
    }
}

@Suite struct DailySeedTests {
    private func date(_ iso: String) -> Date {
        let formatter = ISO8601DateFormatter()
        return formatter.date(from: iso)!
    }

    /// Independent FNV-1a 64 implementation to cross-check the production one.
    private func fnv1a(_ string: String) -> UInt64 {
        var hash: UInt64 = 0xCBF2_9CE4_8422_2325
        for byte in Array(string.utf8) {
            hash ^= UInt64(byte)
            hash = hash &* 0x0000_0100_0000_01B3
        }
        return hash
    }

    @Test func seedIsFNV1aOfDateKey() {
        #expect(DailySeed.seed(for: date("2026-07-01T00:00:00Z")) == fnv1a("gravitile-2026-07-01"))
        #expect(DailySeed.seed(for: date("2026-07-01T23:59:59Z")) == fnv1a("gravitile-2026-07-01"))
    }

    @Test func seedUsesUTCDayBoundary() {
        let lateJuly1 = date("2026-07-01T23:59:59Z")
        let earlyJuly2 = date("2026-07-02T00:00:01Z")
        #expect(DailySeed.seed(for: lateJuly1) != DailySeed.seed(for: earlyJuly2))
        #expect(DailySeed.seed(for: earlyJuly2) == fnv1a("gravitile-2026-07-02"))
    }

    @Test func puzzleNumbersStartAtOneAndIncrementDaily() {
        #expect(DailySeed.puzzleNumber(for: date("2026-07-01T12:00:00Z")) == 1)
        #expect(DailySeed.puzzleNumber(for: date("2026-07-02T12:00:00Z")) == 2)
        #expect(DailySeed.puzzleNumber(for: date("2026-07-31T12:00:00Z")) == 31)
        #expect(DailySeed.puzzleNumber(for: date("2026-08-01T12:00:00Z")) == 32)
    }

    @Test func puzzleNumberChangesAtUTCMidnight() {
        #expect(DailySeed.puzzleNumber(for: date("2026-07-02T23:59:59Z")) == 2)
        #expect(DailySeed.puzzleNumber(for: date("2026-07-03T00:00:01Z")) == 3)
    }

    @Test func seedForPuzzleNumberMatchesSeedForDate() {
        #expect(DailySeed.seed(forPuzzleNumber: 2) == fnv1a("gravitile-2026-07-02"))
        #expect(DailySeed.seed(forPuzzleNumber: 1) == fnv1a("gravitile-2026-07-01"))
    }
}
