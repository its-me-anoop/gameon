import SwiftUI
import GravitileKit

/// First-launch coaching. Each step waits for the player to actually perform
/// the taught action (watched via game state) rather than tapping "next".
struct TutorialOverlay: View {
    let game: GameState
    var onFinished: () -> Void

    @State private var step = 0
    @State private var baselineMoves = 0
    @State private var baselineCascades = 0

    private struct Step {
        let icon: String
        let text: String
    }

    private let steps = [
        Step(icon: "hand.draw", text: "Swipe anywhere — tiles slide and equal tiles merge."),
        Step(icon: "arrow.trianglehead.clockwise.rotate.90", text: "Gravity just turned! It rotates after every move — the compass shows what's next."),
        Step(icon: "sparkles", text: "When tiles tumble, matching ones merge on their own. Chain cascades multiply your points."),
        Step(icon: "flag.checkered", text: "That's everything. One free undo per game — make the tumble work for you."),
    ]

    var body: some View {
        card
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .bottom)
            .onAppear {
                baselineMoves = game.moveCount
                baselineCascades = game.cascadeCount
            }
            .onChange(of: game.moveCount) { _, newCount in
                advanceIfNeeded(moves: newCount)
            }
            .animation(.easeOut(duration: 0.25), value: step)
            .accessibilityIdentifier("tutorialOverlay")
    }

    private var card: some View {
        HStack(alignment: .top, spacing: 12) {
                Image(systemName: steps[step].icon)
                    .font(.system(size: 22, weight: .semibold))
                    .foregroundStyle(Theme.accent)
                    .frame(width: 30)
                Text(steps[step].text)
                    .font(.system(size: 15, weight: .medium))
                    .foregroundStyle(Theme.textPrimary)
                    .fixedSize(horizontal: false, vertical: true)
                Spacer()
                if step == steps.count - 1 {
                    Button("Done") { onFinished() }
                        .font(.system(size: 15, weight: .bold))
                        .foregroundStyle(Theme.accent)
                        .accessibilityIdentifier("tutorialDone")
                }
            }
        .padding(16)
        .background(
            RoundedRectangle(cornerRadius: 16, style: .continuous)
                .fill(Theme.cellWell)
                .shadow(color: .black.opacity(0.4), radius: 16, y: 6)
        )
        .padding(.horizontal, 20)
        .padding(.bottom, 90)
        // The card only intercepts touches on the final step (for Done);
        // during play-along steps swipes pass straight through to the board.
        .allowsHitTesting(step == steps.count - 1)
    }

    private func advanceIfNeeded(moves: Int) {
        let made = moves - baselineMoves
        switch step {
        case 0 where made >= 1: step = 1
        case 1 where made >= 2: step = 2
        case 2 where game.cascadeCount > baselineCascades || made >= 4: step = 3
        default: break
        }
    }
}
