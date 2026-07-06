import GameKit
import SwiftUI

/// Apple's Game Center dashboard (leaderboards page) as a SwiftUI sheet.
struct GameCenterView: UIViewControllerRepresentable {
    @Environment(\.dismiss) private var dismiss

    func makeUIViewController(context: Context) -> GKGameCenterViewController {
        let controller = GKGameCenterViewController(state: .leaderboards)
        controller.gameCenterDelegate = context.coordinator
        return controller
    }

    func updateUIViewController(_ controller: GKGameCenterViewController, context: Context) {}

    func makeCoordinator() -> Coordinator { Coordinator(dismiss: dismiss) }

    final class Coordinator: NSObject, GKGameCenterControllerDelegate {
        private let dismiss: DismissAction

        init(dismiss: DismissAction) {
            self.dismiss = dismiss
        }

        func gameCenterViewControllerDidFinish(_ controller: GKGameCenterViewController) {
            dismiss()
        }
    }
}
