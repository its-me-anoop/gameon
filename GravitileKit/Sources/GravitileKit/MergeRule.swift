/// What makes two tiles combine. The classic modes double equal pairs; Math
/// Pop bonds pairs that add up to the stage target (v1.3 spec §3.1). Every
/// resolver entry point defaults to `.doubling`, so the classic pipeline is
/// untouched by the rule's existence.
public enum MergeRule: Equatable, Sendable {
    case doubling
    /// Two tiles merge iff their values sum to the target; the result tile
    /// (value == target) immediately clears as a bond pop.
    case sumTarget(Int)

    func merges(_ a: Int, _ b: Int) -> Bool {
        switch self {
        case .doubling: a == b
        case let .sumTarget(target): a + b == target
        }
    }

    func mergedValue(_ a: Int, _ b: Int) -> Int {
        switch self {
        case .doubling: a * 2
        case .sumTarget: a + b
        }
    }
}
