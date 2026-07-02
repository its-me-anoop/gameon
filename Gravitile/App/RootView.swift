import SwiftUI
import GravitileKit

struct RootView: View {
    var body: some View {
        GameScreen(game: GameState(mode: .endless, seed: UInt64.random(in: UInt64.min...UInt64.max)))
            .preferredColorScheme(.dark)
    }
}

#Preview {
    RootView()
}
