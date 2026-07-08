/// The Math Pop curriculum (v1.3 spec §3.2): number-bond targets that climb,
/// then loop below the single-digit ceiling (9+9 = 18 is the largest sum two
/// spawnable tiles can make, and an 18-stage would be a 9+9-only grind).
public enum MathProgression {
    public static let targets = [5, 10, 12, 14, 16]
    public static let bondsPerStage = 6
    /// Fresh tiles dealt at game start and after every stage sweep.
    public static let starterCount = 6

    public static func target(forStage stage: Int) -> Int {
        guard stage >= 0 else { return targets[0] }
        if stage < targets.count { return targets[stage] }
        // Loop 10 → 16 forever; stage 5 lands back on 10.
        return targets[1 + (stage - 1) % (targets.count - 1)]
    }

    /// Spawnable values for a target. Closed under complements: for every v in
    /// the range, target − v is also in the range, so no unpairable tile can
    /// ever spawn.
    public static func spawnRange(for target: Int) -> ClosedRange<Int> {
        max(1, target - 9)...min(9, target - 1)
    }
}
