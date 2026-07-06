import Testing
import GravitileKit
@testable import Gravitile

@MainActor
@Suite struct StasisArmingTests {
    private func swipeOnce(_ vm: GameViewModel) -> Bool {
        for direction in Direction.allCases {
            let before = vm.game.moveCount
            vm.handleSwipe(direction)
            if vm.game.moveCount > before { return true }
        }
        return false
    }

    @Test func armedSwipeHoldsGravityAndDisarms() {
        let vm = GameViewModel(game: GameState(mode: .zen, seed: 7), reduceMotion: { true })
        let gravityBefore = vm.game.gravity
        vm.toggleStasis()
        #expect(vm.stasisArmed)

        #expect(swipeOnce(vm))
        #expect(vm.game.gravity == gravityBefore, "armed swipe holds the world")
        #expect(!vm.stasisArmed, "stasis is one-shot")
    }

    @Test func armingIsRefusedWhereTheEngineForbidsIt() {
        for mode in [GameMode.daily(puzzleNumber: 3), .sprint] {
            let vm = GameViewModel(game: GameState(mode: mode, seed: 7), reduceMotion: { true })
            vm.toggleStasis()
            #expect(!vm.stasisArmed, "\(mode) must not arm")
        }
        // Endless without charges also refuses.
        let endless = GameViewModel(game: GameState(mode: .endless, seed: 7), reduceMotion: { true })
        endless.toggleStasis()
        #expect(!endless.stasisArmed)
    }

    @Test func togglingTwiceDisarms() {
        let vm = GameViewModel(game: GameState(mode: .zen, seed: 7), reduceMotion: { true })
        vm.toggleStasis()
        vm.toggleStasis()
        #expect(!vm.stasisArmed)
    }
}
