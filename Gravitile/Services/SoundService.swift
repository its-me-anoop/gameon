import AVFoundation

/// Plays the synthesized effects. Uses the `.ambient` category so the game
/// respects the ring/silent switch and never interrupts the user's music.
@MainActor
final class SoundService {
    private var players: [String: AVAudioPlayer] = [:]
    var isEnabled = true

    init() {
        try? AVAudioSession.sharedInstance().setCategory(.ambient, options: [.mixWithOthers])
        for name in ["merge1", "merge2", "merge3", "merge4", "merge5", "slide", "gameover", "fanfare"] {
            if let url = Bundle.main.url(forResource: name, withExtension: "wav"),
               let player = try? AVAudioPlayer(contentsOf: url) {
                player.prepareToPlay()
                player.volume = 0.7
                players[name] = player
            }
        }
    }

    func merge(round: Int) {
        play("merge\(min(max(round, 1), 5))")
    }

    func slide() { play("slide") }
    func gameOver() { play("gameover") }
    func fanfare() { play("fanfare") }

    private func play(_ name: String) {
        guard isEnabled, let player = players[name] else { return }
        player.currentTime = 0
        player.play()
    }
}
