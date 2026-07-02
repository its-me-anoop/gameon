import Testing
@testable import GravitileKit

@Test func gravityRotatesClockwiseThroughFullCycle() {
    #expect(Direction.down.rotatedClockwise == .left)
    #expect(Direction.left.rotatedClockwise == .up)
    #expect(Direction.up.rotatedClockwise == .right)
    #expect(Direction.right.rotatedClockwise == .down)
}

@Test func oppositesArePaired() {
    #expect(Direction.up.opposite == .down)
    #expect(Direction.down.opposite == .up)
    #expect(Direction.left.opposite == .right)
    #expect(Direction.right.opposite == .left)
}

@Test func gridStepsMatchScreenCoordinates() {
    // Row 0 is the top of the board, so .down increases row.
    #expect(Direction.down.step == (1, 0))
    #expect(Direction.up.step == (-1, 0))
    #expect(Direction.left.step == (0, -1))
    #expect(Direction.right.step == (0, 1))
}

@Test func fourRotationsReturnToStart() {
    for d in Direction.allCases {
        #expect(d.rotatedClockwise.rotatedClockwise.rotatedClockwise.rotatedClockwise == d)
    }
}
